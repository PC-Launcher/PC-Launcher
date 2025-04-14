using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;

namespace PCStreamerLauncher
{
    public class NavigationManager : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<NavigationManager>();

        private string _navigationMode = ConfigKeys.NavigationModes.Keyboard;
        private readonly GamepadHandler _gamepadHandler;
        private readonly bool _ownsGamepadHandler = false;
        
        // Expose the GamepadHandler for external components
        public GamepadHandler GamepadHandler => _gamepadHandler;
        private readonly UniformGrid _appGrid;
        private readonly SoundManager _soundManager;
        private readonly LauncherService _launcherService;
        private readonly Window _parentWindow;
        private readonly FocusManager _focusManager;
        
        // Control whether navigation is enabled
        private bool _navigationEnabled = true;

        public event EventHandler<CommandInfo> ButtonActivated;

        private DateTime _lastNavigationTime = DateTime.MinValue;
        private DateTime _lastTerminateRequestTime = DateTime.MinValue;
        private const int TerminateThrottleMs = 3000;
        private readonly KeyEventHandler _windowKeyDownHandler;

        public NavigationManager(
            UniformGrid appGrid,
            SoundManager soundManager,
            LauncherService launcherService,
            System.Collections.Generic.Dictionary<string, string> navigationConfig,
            GamepadHandler gamepadHandler,
            Window parentWindow)
        {
            _appGrid = appGrid ?? throw new ArgumentNullException(nameof(appGrid));
            _soundManager = soundManager ?? throw new ArgumentNullException(nameof(soundManager));
            _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));

            // Create the focus manager
            _focusManager = new FocusManager(appGrid, parentWindow);

            if (navigationConfig != null && navigationConfig.ContainsKey(ConfigKeys.Navigation.NavigationMode))
            {
                _navigationMode = navigationConfig[ConfigKeys.Navigation.NavigationMode];
            }
            _logger.Info($"Navigation mode set to {_navigationMode}.");

            if (_navigationMode.Equals(ConfigKeys.NavigationModes.Gamepad, StringComparison.OrdinalIgnoreCase) && gamepadHandler != null)
            {
                _gamepadHandler = gamepadHandler;
                _gamepadHandler.GamepadButtonPressed += GamepadHandler_GamepadButtonPressed;
                _gamepadHandler.StartButtonPressed += GamepadHandler_StartButtonPressed;
                _logger.Info("Using externally provided GamepadHandler");
            }
            else if (_navigationMode.Equals(ConfigKeys.NavigationModes.Gamepad, StringComparison.OrdinalIgnoreCase))
            {
                _gamepadHandler = new GamepadHandler();
                _gamepadHandler.GamepadButtonPressed += GamepadHandler_GamepadButtonPressed;
                _gamepadHandler.StartButtonPressed += GamepadHandler_StartButtonPressed;
                _ownsGamepadHandler = true;
                _logger.Info("Created default GamepadHandler");
            }

            _windowKeyDownHandler = new KeyEventHandler(Window_PreviewKeyDown);
            AttachKeyboardHandlers(_parentWindow);
        }
        public string GetNavigationMode()
        {
            ThrowIfDisposed();
            return _navigationMode;
        }

        public void DebugKeyboardEvents()
        {
            _logger.Info("Debugging keyboard events.");
            DebugFocusState();
        }

        public void AttachKeyboardHandlers(Window window)
        {
            ThrowIfDisposed();
            if (window != null)
            {
                window.PreviewKeyDown -= _windowKeyDownHandler;
                window.PreviewKeyDown += _windowKeyDownHandler;
                _logger.Info("Keyboard handlers attached.");
            }
        }

        private void Window_PreviewKeyDown(object sender, RoutedEventArgs e)
        {
            if (!(e is KeyEventArgs keyEvent))
                return;

            // Ignore repeated key events; we rely on physical press/release.
            if (keyEvent.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            _logger.Info($"Key pressed - {keyEvent.Key}");

            if (keyEvent.Key == Key.Escape)
            {
                _soundManager.PlaySound("Return");
                if (sender is Window window)
                    window.Close();
                e.Handled = true;
            }
            else if (keyEvent.Key == Key.Y)
            {
                if (_launcherService.CurrentState == LauncherState.AppRunning && CanProcessTerminateRequest())
                {
                    ProcessTerminateRequest();
                    e.Handled = true;
                }
                else
                {
                    _logger.Info($"Ignoring Y key press while in {_launcherService.CurrentState} state or too soon after previous request");
                    e.Handled = true;
                }
            }
            // Only process navigation keys if navigation is enabled
            else if (_navigationEnabled)
            {
                if (keyEvent.Key == Key.Up)
                {
                    MoveFocus(NavigationDirection.Up);
                    e.Handled = true;
                }
                else if (keyEvent.Key == Key.Down)
                {
                    MoveFocus(NavigationDirection.Down);
                    e.Handled = true;
                }
                else if (keyEvent.Key == Key.Left)
                {
                    MoveFocus(NavigationDirection.Left);
                    e.Handled = true;
                }
                else if (keyEvent.Key == Key.Right)
                {
                    MoveFocus(NavigationDirection.Right);
                    e.Handled = true;
                }
                else if (keyEvent.Key == Key.Enter)
                {
                    Button focusedButton = _focusManager.GetFocusedButton();
                    if (focusedButton != null)
                    {
                        _soundManager.PlaySound("Launch");
                        ActivateButton(focusedButton).FireAndForgetWithLogging(_logger);
                        e.Handled = true;
                    }
                }
            }
            else
            {
                // Navigation is disabled, but we still handle the key to prevent unwanted behavior
                if (keyEvent.Key == Key.Up || keyEvent.Key == Key.Down || 
                    keyEvent.Key == Key.Left || keyEvent.Key == Key.Right || 
                    keyEvent.Key == Key.Enter)
                {
                    _logger.Info("Navigation key ignored - navigation disabled");
                    e.Handled = true;
                }
            }
        }

        private void GamepadHandler_GamepadButtonPressed(object sender, GamepadEventArgs e)
        {
        EventHandlingHelper.SafelyHandleEvent(
            nameof(NavigationManager),
        $"GamepadButtonPressed - {e.Button}",
            () =>
        {
            _logger.Info("Gamepad button pressed: " + e.Button);

        if (e.Button == "Y")
            {
                    HandleTerminateButton();
                return;
            }

        // Start button is now handled in its own method
        if (e.Button == "Start")
        {
            return; // Skip processing here as we handle it in GamepadHandler_StartButtonPressed
        }

        // Only process navigation buttons if navigation is enabled
        if (_navigationEnabled)
        {
            if (e.Button == "A")
            {
                _soundManager.PlaySound("Launch");
            Button focusedButton = _focusManager.GetFocusedButton();
                if (focusedButton != null)
                    {
                        ActivateButton(focusedButton).FireAndForgetWithLogging(_logger);
                    }
            }
                else if (e.Button == "Up")
                {
                    MoveFocus(NavigationDirection.Up);
            }
                else if (e.Button == "Down")
                {
                    MoveFocus(NavigationDirection.Down);
            }
                else if (e.Button == "Left")
                {
                    MoveFocus(NavigationDirection.Left);
            }
                else if (e.Button == "Right")
                    {
                        MoveFocus(NavigationDirection.Right);
                    }
            }
            else
            {
                _logger.Info($"Gamepad navigation ignored - navigation disabled for button: {e.Button}");
            }
        },
        _logger);
        }
                private void GamepadHandler_StartButtonPressed(object sender, GamepadEventArgs e)
        {
            EventHandlingHelper.SafelyHandleEvent(
                nameof(NavigationManager),
                "StartButtonPressed",
                () =>
                {
                    _logger.Info("StartButtonPressed event received - toggling help overlay");
                    _soundManager.PlaySound("Navigate");
                    
                    if (_parentWindow is MainWindow mainWindow)
                    {
                        mainWindow.ToggleHelpOverlay();
                    }
                },
                _logger);
        }
        
        private void HandleTerminateButton()
        {
            if (_launcherService.CurrentState == LauncherState.AppRunning && CanProcessTerminateRequest())
            {
                ProcessTerminateRequest();
            }
            else
            {
                _logger.Info($"Ignoring Y button press while in {_launcherService.CurrentState} state or too soon after previous request");
            }
        }

        private bool CanProcessTerminateRequest()
        {
            return (DateTime.Now - _lastTerminateRequestTime).TotalMilliseconds > TerminateThrottleMs;
        }

        private void ProcessTerminateRequest()
        {
            _lastTerminateRequestTime = DateTime.Now;
            _launcherService.TerminateApplication();
            _logger.Info("Terminate request processed and throttled");
        }

        /// <summary>
        /// Moves focus in the specified direction.
        /// </summary>
        private bool MoveFocus(NavigationDirection direction)
        {
            // Don't move focus if navigation is disabled
            if (!_navigationEnabled)
            {
                _logger.Info($"MoveFocus called but navigation is disabled - direction: {direction}");
                return false;
            }

            // Throttle navigation to prevent too-rapid movement
            TimeSpan minInterval = TimeSpan.FromMilliseconds(150);
            if ((DateTime.Now - _lastNavigationTime) < minInterval)
                return false;

            _lastNavigationTime = DateTime.Now;

            bool focusMoved = _focusManager.MoveFocus(direction);
            if (focusMoved)
            {
                _soundManager.PlaySound("Navigate");
            }
            return focusMoved;
        }

        public void SetInitialFocus()
        {
        ThrowIfDisposed();
        ErrorHelper.ExecuteWithLogging(
            "Setting initial focus",
        () =>
        {
                // Only set focus if navigation is enabled
                if (_navigationEnabled)
                {
                    bool focusSet = _focusManager.SetInitialFocus();
                    _logger.Info($"Initial focus set: {focusSet}");
                }
                else
                {
                    _logger.Info("Skipping initial focus - navigation disabled");
                }
            },
        _logger);
        }

        public void DebugFocusState()
        {
            ThrowIfDisposed();
            _focusManager.LogFocusState();
            _logger.Info($"Navigation enabled: {_navigationEnabled}");
        }
        
        /// <summary>
        /// Enables or disables navigation functionality.
        /// </summary>
        /// <param name="enabled">True to enable navigation, false to disable it.</param>
        public void SetNavigationEnabled(bool enabled)
        {
            ThrowIfDisposed();
            ErrorHelper.ExecuteWithLogging(
                $"Setting navigation {(enabled ? "enabled" : "disabled")}",
                () =>
                {
                    _navigationEnabled = enabled;
                    _logger.Info($"Navigation {(enabled ? "enabled" : "disabled")}");
                },
                _logger);
        }

        /// <summary>
        /// Notifies the NavigationManager that a modal overlay is about to be shown, 
        /// so focus should be temporarily redirected.
        /// </summary>
        public void NotifyOverlayShowing()
        {
            ThrowIfDisposed();
            _focusManager.BeginFocusRedirection();
        }

        /// <summary>
        /// Notifies the NavigationManager that a modal overlay has been hidden,
        /// so focus should be restored.
        /// </summary>
        public void NotifyOverlayHidden()
        {
            ThrowIfDisposed();
            _focusManager.EndFocusRedirection();
        }

        private async Task ActivateButton(Button button)
        {
            if (button == null)
            {
                _logger.Warning("ActivateButton called with null button");
                return;
            }

            _logger.Info($"Activating button at index {_focusManager.CurrentFocusIndex}");

            if (button.Tag is CommandInfo commandInfo)
            {
                _logger.Info("Button has CommandInfo - invoking ButtonActivated event");
                ButtonActivated?.Invoke(this, commandInfo);
                await _launcherService.LaunchApplicationAsync(commandInfo);
            }
            else
            {
                _logger.Info("Button doesn't have CommandInfo - raising Click event");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
        protected override void ReleaseManagedResources()
        {
        ErrorHelper.ExecuteWithLogging(
            "Releasing managed resources in NavigationManager",
        () =>
            {
            _logger.Info("Beginning resource cleanup");

            // Remove event handlers from window
        ErrorHelper.SafelyCleanupResource(
            nameof(NavigationManager),
        "window event handlers",
        () =>
            {
                    if (_parentWindow != null)
                    {
                        _parentWindow.PreviewKeyDown -= _windowKeyDownHandler;
                    _logger.Info("Removed window event handlers");
                    }
                    });

            // Clean up gamepad handler
            ErrorHelper.SafelyCleanupResource(
            nameof(NavigationManager),
            "gamepad handler",
        () =>
        {
                        if (_gamepadHandler != null)
            {
                    // Unsubscribe from gamepad events directly
                    _gamepadHandler.GamepadButtonPressed -= GamepadHandler_GamepadButtonPressed;
                    _gamepadHandler.StartButtonPressed -= GamepadHandler_StartButtonPressed;
                    _logger.Info("Unsubscribed from gamepad events");
                    
                        if (_ownsGamepadHandler)
                        {
                            _gamepadHandler.Dispose();
                        _logger.Info("Disposed owned gamepad handler");
                        }
                        }
                });

            // Dispose focus manager
        ErrorHelper.SafelyCleanupResource(
            nameof(NavigationManager),
        "focus manager",
        () =>
            {
                    if (_focusManager != null)
                    {
                        _focusManager.Dispose();
                    _logger.Info("Disposed focus manager");
                    }
                    });

            // Clear event handlers
            CollectionHelper.SafelyRemoveEventHandler<CommandInfo>(
            ref ButtonActivated,
            "ButtonActivated",
            _logger);
        },
        _logger);
        }
    }
}