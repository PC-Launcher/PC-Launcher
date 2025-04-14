using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;

namespace PCStreamerLauncher
{
    public enum LauncherState
    {
        Idle,
        Launching,
        WaitingForProcess,
        AppRunning,
        Terminating,
        Reappearing
    }

    public class LauncherService : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<LauncherService>();

        public event EventHandler<LauncherState> StateChanged;
        public event EventHandler<string> PlaySoundRequested;
        public event EventHandler<bool> LauncherVisibilityChangeRequested;
        public event EventHandler<CommandInfo> LaunchAppRequested;
        public event EventHandler<Process> AppLaunched;
        public event EventHandler TerminateAppRequested;
        public event EventHandler ButtonsEnableRequested;
        public event EventHandler ButtonsDisableRequested;
        public event EventHandler<bool> NavigationEnableRequested;

        // Track the time each state was entered for duration calculation
        private Dictionary<LauncherState, DateTime> _stateEntryTimes = new Dictionary<LauncherState, DateTime>();

        private LauncherState _currentState = LauncherState.Idle;
        public LauncherState CurrentState
        {
            get { return _currentState; }
            private set
            {
                if (_currentState != value)
                {
                    _logger.Info($"State transitioning from {_currentState} to {value}");

                    // Log additional context about the transition
                    string contextInfo = GetStateTransitionContext(value);
                    if (!string.IsNullOrEmpty(contextInfo))
                    {
                        _logger.Info($"State transition context - {contextInfo}");
                    }

                    ExitState(_currentState);
                    _currentState = value;
                    EnterState(value);

                    // Only raise event if not disposed
                    if (!IsDisposed)
                    {
                        EventHandlingHelper.SafelyHandleEvent(
                            nameof(LauncherService),
                            "StateChanged event",
                            () => StateChanged?.Invoke(this, _currentState),
                            _logger);
                    }
                }
            }
        }

        private int _launchSoundDelay = 250;
        private int _webAppHideDelay = 3000;
        private int _appHideDelay = 1000;
        private int _reappearDelay = 500;

        private Process _launchedProcess;
        private string _currentLaunchedUrl;
        private bool _isTransitioning = false;
        private readonly SoundManager _soundManager;
        private bool _isShuttingDown = false;

        // Task completion source for pending operations
        private TaskCompletionSource<bool> _pendingLaunchCompletion;
        private TaskCompletionSource<bool> _pendingReappearCompletion;

        public LauncherService(SoundManager soundManager, System.Collections.Generic.Dictionary<string, string> browserConfig)
        {
            _soundManager = soundManager ?? throw new ArgumentNullException(nameof(soundManager));
            _logger.Info("Initializing");
            LoadConfiguration(browserConfig);
            _logger.Info($"Initialized with hide delays - Web: {_webAppHideDelay}ms, App: {_appHideDelay}ms, Reappear: {_reappearDelay}ms");

            // Initialize the timer for the initial state
            _stateEntryTimes[LauncherState.Idle] = DateTime.Now;
        }

        private void LoadConfiguration(System.Collections.Generic.Dictionary<string, string> browserConfig)
        {
            if (browserConfig != null && browserConfig.ContainsKey(ConfigKeys.Browser.HideDelay) &&
                int.TryParse(browserConfig[ConfigKeys.Browser.HideDelay], out int delay))
            {
                _webAppHideDelay = delay;
                _logger.Info($"Web app hide delay set to {_webAppHideDelay} ms from configuration");
            }
        }
        private void EnterState(LauncherState state)
        {
            _stateEntryTimes[state] = DateTime.Now;

            switch (state)
            {
                case LauncherState.Idle:
                    _logger.Info("Entered Idle state - Ready to accept launch commands");
                    break;
                case LauncherState.Launching:
                    _logger.Info("Entered Launching state - Initiating application launch");
                    break;
                case LauncherState.WaitingForProcess:
                    _logger.Info("Entered WaitingForProcess state - Waiting for process to start");
                    break;
                case LauncherState.AppRunning:
                    _logger.Info("Entered AppRunning state - Application is now running");
                    break;
                case LauncherState.Terminating:
                    _logger.Info("Entered Terminating state - Application shutdown initiated");
                    break;
                case LauncherState.Reappearing:
                    _logger.Info("Entered Reappearing state - Launcher is becoming visible again");
                    break;
                default:
                    _logger.Warning($"Entered unknown state: {state}");
                    break;
            }
        }

        private void ExitState(LauncherState state)
        {
            string stateDuration = CalculateStateDuration(state);

            switch (state)
            {
                case LauncherState.Idle:
                    _logger.Info($"Exiting Idle state after {stateDuration}");
                    break;
                case LauncherState.Launching:
                    _logger.Info($"Exiting Launching state after {stateDuration}");
                    break;
                case LauncherState.WaitingForProcess:
                    _logger.Info($"Exiting WaitingForProcess state after {stateDuration}");
                    break;
                case LauncherState.AppRunning:
                    _logger.Info($"Exiting AppRunning state after {stateDuration}");
                    break;
                case LauncherState.Terminating:
                    _logger.Info($"Exiting Terminating state after {stateDuration}");
                    break;
                case LauncherState.Reappearing:
                    _logger.Info($"Exiting Reappearing state after {stateDuration}");
                    break;
                default:
                    _logger.Warning($"Exiting unknown state: {state} after {stateDuration}");
                    break;
            }
        }

        private string CalculateStateDuration(LauncherState state)
        {
            if (_stateEntryTimes.TryGetValue(state, out DateTime entryTime))
            {
                TimeSpan duration = DateTime.Now - entryTime;
                return $"{duration.TotalSeconds:F1}s";
            }
            return "unknown duration";
        }

        private string GetStateTransitionContext(LauncherState newState)
        {
            // Add contextual information based on the new state
            switch (newState)
            {
                case LauncherState.Launching:
                    return $"URL: {_currentLaunchedUrl ?? "N/A"}";
                case LauncherState.AppRunning:
                    return $"Process: {(_launchedProcess != null ? $"{_launchedProcess.ProcessName} (ID: {_launchedProcess.Id})" : "N/A")}";
                case LauncherState.Terminating:
                    return $"Process being terminated: {(_launchedProcess != null ? $"{_launchedProcess.ProcessName} (ID: {_launchedProcess.Id})" : "N/A")}";
                default:
                    return string.Empty;
            }
        }
        public async Task LaunchApplicationAsync(CommandInfo commandInfo)
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            await ErrorHelper.ExecuteAsyncWithLogging(
                $"[Operation:{operationId}] Launching application",
                async () =>
                {
                    _logger.Info($"[Operation:{operationId}] Starting application launch: {commandInfo.RawCommand}, IsWeb: {commandInfo.IsWeb}");

                    if (CurrentState != LauncherState.Idle)
                    {
                        _logger.Info($"[Operation:{operationId}] Ignoring launch request while in {CurrentState} state");
                        return;
                    }

                    // Cancel any existing launch completion
                    if (_pendingLaunchCompletion != null)
                    {
                        _logger.Info($"[Operation:{operationId}] Cancelling existing pending launch operation");
                        _pendingLaunchCompletion.TrySetCanceled();
                    }

                    // Create a new task completion source for this launch
                    _pendingLaunchCompletion = new TaskCompletionSource<bool>();

                    // Set state after storing entry time to ensure proper duration calculation
                    _logger.Info($"[Operation:{operationId}] Transitioning to Launching state");
                    CurrentState = LauncherState.Launching;

                    ButtonsDisableRequested?.Invoke(this, EventArgs.Empty);
                    _logger.Info($"[Operation:{operationId}] Disabled launcher buttons");

                    if (!IsDisposed)
                    {
                        PlaySoundRequested?.Invoke(this, "Launch");
                        _logger.Info($"[Operation:{operationId}] Launch sound requested");
                    }

                    await Task.Delay(_launchSoundDelay);
                    _logger.Info($"[Operation:{operationId}] Completed launch sound delay ({_launchSoundDelay}ms)");

                    // Check if we've been disposed during the delay
                    if (IsDisposed)
                    {
                        _logger.Info($"[Operation:{operationId}] Service disposed during launch delay, aborting launch");
                        return;
                    }

                    if (commandInfo.IsWeb)
                    {
                        _currentLaunchedUrl = commandInfo.RawCommand;
                        _logger.Info($"[Operation:{operationId}] Launching web app with URL: {_currentLaunchedUrl}");
                    }
                    else
                    {
                        _currentLaunchedUrl = null;
                        _logger.Info($"[Operation:{operationId}] Launching desktop application: {commandInfo.RawCommand}");
                    }

                    if (!IsDisposed)
                    {
                        _logger.Info($"[Operation:{operationId}] Invoking LaunchAppRequested event");
                        LaunchAppRequested?.Invoke(this, commandInfo);
                    }

                    _logger.Info($"[Operation:{operationId}] Transitioning to WaitingForProcess state");
                    CurrentState = LauncherState.WaitingForProcess;

                    // Wait for the launch to complete or timeout after 30 seconds
                    _logger.Info($"[Operation:{operationId}] Waiting for process launch to complete (max 30s)");
                    Task timeoutTask = Task.Delay(30000);
                    Task completedTask = await Task.WhenAny(_pendingLaunchCompletion.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        _logger.Warning($"[Operation:{operationId}] Launch operation timed out after 30 seconds");
                        _pendingLaunchCompletion.TrySetCanceled();
                    }
                    else
                    {
                        _logger.Info($"[Operation:{operationId}] Launch operation completed before timeout");
                    }
                },
                _logger);
            
            _logger.Info($"[Operation:{operationId}] Application launch sequence completed - State: {CurrentState}");
        }

        public async Task NotifyProcessLaunchedAsync(Process process)
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            if (CurrentState != LauncherState.WaitingForProcess)
            {
                _logger.Info($"[Operation:{operationId}] Ignoring process launched notification while in {CurrentState} state");
                return;
            }

            await ErrorHelper.ExecuteAsyncWithLogging(
                $"[Operation:{operationId}] Notifying process launch",
                async () =>
                {
                    _launchedProcess = process;
                    _logger.Info($"[Operation:{operationId}] Process launched - ID: {process.Id}, Name: {process.ProcessName}");

                    if (!IsDisposed)
                    {
                        AppLaunched?.Invoke(this, process);
                    }

                    int hideDelay = !string.IsNullOrEmpty(_currentLaunchedUrl) ? _webAppHideDelay : _appHideDelay;
                    _logger.Info($"[Operation:{operationId}] Waiting {hideDelay}ms before hiding launcher");

                    await Task.Delay(hideDelay);

                    // Complete the pending launch operation
                    if (_pendingLaunchCompletion != null)
                    {
                        _logger.Info($"[Operation:{operationId}] Setting launch completion to successful");
                        _pendingLaunchCompletion.TrySetResult(true);
                    }

                    // Check if disposed during the delay
                    if (IsDisposed)
                    {
                        _logger.Info($"[Operation:{operationId}] Service disposed during hide delay, aborting hide operation");
                        return;
                    }

                    if (CurrentState == LauncherState.WaitingForProcess)
                    {
                        _logger.Info($"[Operation:{operationId}] Disabling navigation and hiding launcher window");
                        NavigationEnableRequested?.Invoke(this, false);
                        LauncherVisibilityChangeRequested?.Invoke(this, false);
                        CurrentState = LauncherState.AppRunning;
                    }
                },
                _logger);
        }

        public void NotifyProcessRunning(bool isRunning)
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            if (_isTransitioning || _isShuttingDown)
            {
                _logger.Info($"[Operation:{operationId}] Ignoring process state change during transition or shutdown");
                return;
            }

            ErrorHelper.ExecuteWithLogging(
                $"[Operation:{operationId}] Processing running state change to {isRunning}",
                () =>
                {
                    if (isRunning && CurrentState != LauncherState.AppRunning)
                    {
                        if (CurrentState == LauncherState.WaitingForProcess)
                        {
                            _isTransitioning = true;
                            _logger.Info($"[Operation:{operationId}] Process detected as running, hiding launcher");

                            if (!IsDisposed)
                            {
                                // Disable navigation before hiding the launcher
                                UIOperationHelper.TryInvokeOnUIThread(
                                    () => NavigationEnableRequested?.Invoke(this, false),
                                    "Disabling navigation while app is running",
                                    _logger);
                                
                                UIOperationHelper.TryInvokeOnUIThread(
                                    () => LauncherVisibilityChangeRequested?.Invoke(this, false),
                                    "Changing launcher visibility",
                                    _logger);
                            }

                            CurrentState = LauncherState.AppRunning;

                            // FIXED: Changed from ContinueWith to async method with await
                            HandleTransitionCooldownAsync(operationId).FireAndForgetWithLogging(_logger);
                        }
                    }
                    else if (!isRunning && CurrentState == LauncherState.AppRunning)
                    {
                        _logger.Info($"[Operation:{operationId}] Process detected as stopped");
                        HandleProcessTermination();
                    }
                },
                _logger);
        }

        // NEW METHOD: Created async method to handle transition cooldown
        private async Task HandleTransitionCooldownAsync(string operationId)
        {
            await ErrorHelper.ExecuteAsyncWithLogging(
                $"[Operation:{operationId}] Transition cooldown",
                async () =>
                {
                    await Task.Delay(1000);
                    if (!IsDisposed)
                    {
                        _isTransitioning = false;
                        _logger.Info($"[Operation:{operationId}] Transition cooldown complete");
                    }
                },
                _logger);
        }

        public void TerminateApplication()
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            if (CurrentState != LauncherState.AppRunning)
            {
                _logger.Info($"[Operation:{operationId}] Ignoring termination request while in {CurrentState} state");
                return;
            }

            ErrorHelper.ExecuteWithLogging(
                $"[Operation:{operationId}] Terminating application",
                () =>
                {
                    _logger.Info($"[Operation:{operationId}] User requested application termination");
                    CurrentState = LauncherState.Terminating;

                    if (!IsDisposed)
                    {
                        PlaySoundRequested?.Invoke(this, "Return");
                        _logger.Info($"[Operation:{operationId}] Return sound requested");

                        TerminateAppRequested?.Invoke(this, EventArgs.Empty);
                    }

                    _currentLaunchedUrl = null;

                    if (_launchedProcess != null)
                    {
                        _logger.Info($"[Operation:{operationId}] Clearing process reference to {_launchedProcess.ProcessName} (ID: {_launchedProcess.Id})");
                        _launchedProcess = null;
                    }

                    // Let's use the async method but call it in a way that's compatible with the 
                    // synchronous method signature
                    HandleReappearAsync().FireAndForgetWithLogging(_logger);
                },
                _logger);
        }

        private void HandleProcessTermination()
        {
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            if (_isTransitioning || CurrentState == LauncherState.Terminating ||
                CurrentState == LauncherState.Reappearing || _isShuttingDown)
            {
                _logger.Info($"[Operation:{operationId}] Ignoring process termination while in {CurrentState} state");
                return;
            }

            ErrorHelper.ExecuteWithLogging(
                $"[Operation:{operationId}] Handling process termination",
                () =>
                {
                    _logger.Info($"[Operation:{operationId}] Handling process termination");
                    CurrentState = LauncherState.Terminating;
                    _isTransitioning = true;

                    if (!IsDisposed)
                    {
                        PlaySoundRequested?.Invoke(this, "Return");
                        _logger.Info($"[Operation:{operationId}] Return sound requested for termination");
                    }

                    _currentLaunchedUrl = null;

                    if (_launchedProcess != null)
                    {
                        _logger.Info($"[Operation:{operationId}] Clearing process reference for terminated application");
                        _launchedProcess = null;
                    }

                    // Use the async method in a fire-and-forget pattern with logging
                    HandleReappearAsync().FireAndForgetWithLogging(_logger);
                },
                _logger);
        }

        // FIXED: Changed from async void to async Task
        private async Task HandleReappearAsync()
        {
            if (IsDisposed) return;
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            await ErrorHelper.ExecuteAsyncWithLogging(
                $"[Operation:{operationId}] Handling reappear sequence",
                async () =>
                {
                    // Cancel any existing reappear completion
                    if (_pendingReappearCompletion != null)
                    {
                        _pendingReappearCompletion.TrySetCanceled();
                    }

                    // Create a new task completion source for this reappear operation
                    _pendingReappearCompletion = new TaskCompletionSource<bool>();

                    CurrentState = LauncherState.Reappearing;
                    _logger.Info($"[Operation:{operationId}] Starting reappear sequence with {_reappearDelay}ms delay");

                    await Task.Delay(_reappearDelay);

                    if (IsDisposed || _isShuttingDown)
                    {
                        _logger.Info($"[Operation:{operationId}] Service disposed or shutting down during reappear delay, aborting reappear");
                        return;
                    }

                    _logger.Info($"[Operation:{operationId}] Requesting launcher visibility");
                    UIOperationHelper.TryInvokeOnUIThread(
                        () => LauncherVisibilityChangeRequested?.Invoke(this, true),
                        "Changing launcher visibility to visible",
                        _logger);

                    _logger.Info($"[Operation:{operationId}] Enabling launcher buttons and navigation");
                    ButtonsEnableRequested?.Invoke(this, EventArgs.Empty);
                    
                    _logger.Info($"[Operation:{operationId}] Re-enabling navigation");
                    UIOperationHelper.TryInvokeOnUIThread(
                        () => NavigationEnableRequested?.Invoke(this, true),
                        "Re-enabling navigation",
                        _logger);

                    CurrentState = LauncherState.Idle;
                    _isTransitioning = false;

                    // Complete the pending reappear operation
                    if (_pendingReappearCompletion != null)
                    {
                        _pendingReappearCompletion.TrySetResult(true);
                    }

                    _logger.Info($"[Operation:{operationId}] Reappear sequence complete, launcher is in Idle state");
                },
                _logger);
        }

        /// <summary>
        /// Prepares the service for shutdown, canceling pending operations
        /// </summary>
        public void PrepareForShutdown()
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            ErrorHelper.ExecuteWithLogging(
                $"[Operation:{operationId}] Preparing for shutdown",
                () =>
                {
                    _logger.Info($"[Operation:{operationId}] Preparing for shutdown");
                    _isShuttingDown = true;

                    // Cancel pending operations
                    if (_pendingLaunchCompletion != null)
                    {
                        _pendingLaunchCompletion.TrySetCanceled();
                    }

                    if (_pendingReappearCompletion != null)
                    {
                        _pendingReappearCompletion.TrySetCanceled();
                    }

                    // Force transition to idle state to prevent further operations
                    _isTransitioning = false;
                    CurrentState = LauncherState.Idle;
                },
                _logger);
        }

        protected override void ReleaseManagedResources()
        {
            ErrorHelper.ExecuteWithLogging(
                "Releasing managed resources in LauncherService",
                () =>
                {
                    _logger.Info("Beginning resource cleanup");

                    // Prepare for shutdown to cancel pending operations
                    ErrorHelper.SafelyCleanupResource(
                        nameof(LauncherService),
                        "pending operations",
                        () => 
                        {
                            PrepareForShutdown();
                        });

                    // Clear all event handlers in a thread-safe way
                    // Use EventHandlingHelper's SafelyDetachEventHandler instead for custom event types
                    EventHandlingHelper.SafelyDetachEventHandler(
                        ref StateChanged,
                        nameof(LauncherService),
                        "StateChanged",
                        _logger);

                    EventHandlingHelper.SafelyDetachEventHandler(
                        ref PlaySoundRequested,
                        nameof(LauncherService),
                        "PlaySoundRequested",
                        _logger);

                    EventHandlingHelper.SafelyDetachEventHandler(
                        ref LauncherVisibilityChangeRequested,
                        nameof(LauncherService),
                        "LauncherVisibilityChangeRequested",
                        _logger);

                    EventHandlingHelper.SafelyDetachEventHandler(
                        ref LaunchAppRequested,
                        nameof(LauncherService),
                        "LaunchAppRequested",
                        _logger);

                    EventHandlingHelper.SafelyDetachEventHandler(
                        ref AppLaunched,
                        nameof(LauncherService),
                        "AppLaunched",
                        _logger);

                    CollectionHelper.SafelyRemoveEventHandler(
                        ref TerminateAppRequested,
                        "TerminateAppRequested",
                        _logger);

                    CollectionHelper.SafelyRemoveEventHandler(
                        ref ButtonsEnableRequested,
                        "ButtonsEnableRequested",
                        _logger);

                    CollectionHelper.SafelyRemoveEventHandler(
                        ref ButtonsDisableRequested,
                        "ButtonsDisableRequested",
                        _logger);
                        
                    CollectionHelper.SafelyRemoveEventHandler(
                        ref NavigationEnableRequested,
                        "NavigationEnableRequested",
                        _logger);

                    _logger.Info("Cleared all event handlers");

                    // Clean up process references
                    ErrorHelper.SafelyCleanupResource(
                        nameof(LauncherService),
                        "process references",
                        () =>
                        {
                            if (_launchedProcess != null)
                            {
                                _logger.Info("Cleaning up reference to launched process");
                                ProcessOperationHelper.SafelyDisposeProcess(_launchedProcess, "LauncherService._launchedProcess", _logger);
                                _launchedProcess = null;
                            }
                            _currentLaunchedUrl = null;
                            _logger.Info("Cleared process references");
                        });

                    // Clean up task completion sources
                    ErrorHelper.SafelyCleanupResource(
                        nameof(LauncherService),
                        "task completion sources",
                        () =>
                        {
                            if (_pendingLaunchCompletion != null)
                            {
                                _pendingLaunchCompletion.TrySetCanceled();
                                _pendingLaunchCompletion = null;
                            }

                            if (_pendingReappearCompletion != null)
                            {
                                _pendingReappearCompletion.TrySetCanceled();
                                _pendingReappearCompletion = null;
                            }

                            _logger.Info("Cleaned up pending task completion sources");
                        });

                    // Reset state variables
                    ErrorHelper.SafelyCleanupResource(
                        nameof(LauncherService),
                        "state variables",
                        () =>
                        {
                            _isTransitioning = false;
                            _isShuttingDown = true;
                            
                            CollectionHelper.SafelyClearCollection(
                                _stateEntryTimes,
                                _logger,
                                "_stateEntryTimes");
                                
                            // Don't change CurrentState here as it might trigger events
                            _logger.Info("Reset state variables");
                        });
                },
                _logger);
        }
    }

    // Extension method to provide clean fire-and-forget with error logging
    public static class TaskExtensions
    {
        public static void FireAndForgetWithLogging(this Task task, ContextLogger logger)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    logger.Error("Error in fire-and-forget task", t.Exception.Flatten().InnerException);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}