using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;

namespace PCStreamerLauncher
{
    public class ProcessMonitor : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<ProcessMonitor>();

        public event EventHandler<bool> ProcessStateChanged;
        private readonly List<string> _monitoredProcesses = new List<string>();
        private readonly List<string> _mediaPlayerProcessNames = new List<string>();
        private IDisposable _processStateSubscription;
        private string _currentLaunchedUrl;

        // Semaphore to prevent overlapping process checks.
        private readonly SemaphoreSlim _processCheckLock = new SemaphoreSlim(1, 1);

        public ProcessMonitor(string configPath)
        {
            try
            {
                _logger.Info("Initializing...");
                var mediaPlayers = ConfigParser.LoadMediaPlayers(configPath);
                var browserConfig = ConfigParser.LoadBrowserConfig(configPath);

                LoadMonitoredProcesses(mediaPlayers, browserConfig);
            }
            catch (Exception ex)
            {
                // Using standard error logging
                _logger.Error("Error during initialization", ex);
            }
        }

        private void LoadMonitoredProcesses(Dictionary<string, string> mediaPlayers, Dictionary<string, string> browserConfig)
        {
            ErrorHelper.ExecuteWithLogging(
                "Loading monitored processes", 
                () =>
                {
                    foreach (var kvp in mediaPlayers)
                    {
                        string exePath = SanitizePath(kvp.Value);
                        string procName = Path.GetFileNameWithoutExtension(exePath);
                        if (!string.IsNullOrWhiteSpace(procName) &&
                            !_monitoredProcesses.Contains(procName, StringComparer.OrdinalIgnoreCase))
                        {
                            _monitoredProcesses.Add(procName);
                            _mediaPlayerProcessNames.Add(procName);
                            _logger.Info($"Added media player process '{procName}' to monitoring list.");
                        }
                    }

                    if (browserConfig.ContainsKey(ConfigKeys.Browser.DefaultBrowserExecutable))
                    {
                        string defaultBrowserExecutable = SanitizePath(browserConfig[ConfigKeys.Browser.DefaultBrowserExecutable]);
                        string browserProc = Path.GetFileNameWithoutExtension(defaultBrowserExecutable);
                        if (!string.IsNullOrWhiteSpace(browserProc))
                        {
                            _monitoredProcesses.Add(browserProc);
                            _logger.Info($"Added default browser process '{browserProc}' to monitoring list.");
                        }
                    }

                    if (browserConfig.ContainsKey(ConfigKeys.Browser.AlternateBrowserExecutable))
                    {
                        string alternateBrowserExecutable = SanitizePath(browserConfig[ConfigKeys.Browser.AlternateBrowserExecutable]);
                        string altBrowserProc = Path.GetFileNameWithoutExtension(alternateBrowserExecutable);
                        if (!string.IsNullOrWhiteSpace(altBrowserProc))
                        {
                            _monitoredProcesses.Add(altBrowserProc);
                            _logger.Info($"Added alternate browser process '{altBrowserProc}' to monitoring list.");
                        }
                    }
                },
                _logger);
        }

        private string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            return path.Replace("\"", "").Trim();
        }

        public void Start()
        {
            ThrowIfDisposed();
            ErrorHelper.ExecuteWithLogging(
                "Starting process monitoring",
                () =>
                {
                    Stop();
                    InitializeProcessMonitoringRx();
                },
                _logger);
        }

        public void SetCurrentLaunchedUrl(string url)
        {
            ThrowIfDisposed();

            ErrorHelper.ExecuteWithLogging(
                "Setting current launched URL",
                () =>
                {
                    _currentLaunchedUrl = url;
                    _logger.Info($"Current launched URL set to: {url ?? "null"}");
                },
                _logger);
        }

        private void InitializeProcessMonitoringRx()
        {
            _logger.Info("Starting Rx-based process monitoring.");

            ErrorHelper.ExecuteWithLogging(
                "Setting up process monitoring",
                () =>
                {
                    var processStateObservable = Observable.Interval(TimeSpan.FromMilliseconds(500))
                        .Select(_ => 
                        {
                            // Return a new observable that wraps the async operation
                            return Observable.FromAsync(async () =>
                            {
                                if (IsDisposed)
                                    return false;

                                // Use a timeout to avoid locking up the monitoring thread
                                if (!await _processCheckLock.WaitAsync(1000))
                                {
                                    _logger.Debug("Process check lock timeout - skipping check");
                                    return false;
                                }

                                try
                                {
                                    // Create a new cancellation token source that will auto-cancel if the check takes too long
                                    using (var cts = new CancellationTokenSource(2000))
                                    {
                                        bool result = await IsMonitoredAppRunningAsync(cts.Token);
                                        return result; // Explicitly returning the result to avoid lambda return value issues
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.Debug("Process check was cancelled due to timeout");
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error("Error checking process state", ex);
                                    return false;
                                }
                                finally
                                {
                                    _processCheckLock.Release();
                                }
                            });
                        })
                        .Concat()
                        .DistinctUntilChanged()
                        .Throttle(TimeSpan.FromMilliseconds(200))
                        .ObserveOn(Application.Current.Dispatcher);

                    _processStateSubscription = processStateObservable.Subscribe(
                        // OnNext
                        isRunning => {
                            EventHandlingHelper.SafelyHandleEvent(
                            nameof(ProcessMonitor),
                            "ProcessStateChanged event handler",
                            () =>
                            {
                            if (!IsDisposed)
                            {
                            _logger.Info($"Process running state changed: {isRunning}");
                            ProcessStateChanged?.Invoke(this, isRunning);
                            }
                            },
                _logger);
                        },
                        // OnError
                        ex => {
                            _logger.Error("Error in observable", ex);
                        }
                    );
                },
                _logger);
        }

        // Updated to check media player processes even if they lack a visible window.
        private async Task<bool> IsMonitoredAppRunningAsync(CancellationToken token)
        {
            if (IsDisposed) return false;

            // First, check media player processes directly
            foreach (string procName in _mediaPlayerProcessNames)
            {
                Process[] procs = null;
                bool isRunning = await ErrorHelper.ExecuteAsyncWithLogging<bool>(
                    $"Checking if media player '{procName}' is running",
                    async () =>
                    {
                        // Use await Task.Run to make this truly async and avoid warning
                        procs = null;
                        await Task.Run(() => 
                        {
                            procs = Process.GetProcessesByName(procName);
                        }, token);
                        
                        bool foundRunning = procs.Any();
                        if (foundRunning)
                        {
                            _logger.Info($"Found running media player process {procName}");
                            return true;
                        }
                        return false;
                    },
                    _logger,
                    false);
                
                // Always release processes regardless of result
                SafeReleaseProcessArray(procs);
                
                if (isRunning) return true;
            }

            // If a URL is launched, check browser processes.
            if (!string.IsNullOrEmpty(_currentLaunchedUrl))
            {
                bool browserRunning = await CheckBrowserProcessesAsync(token);
                if (browserRunning) return true;
            }

            // Finally, check any monitored process with a visible window.
            foreach (string procName in _monitoredProcesses)
            {
                Process[] processes = null;
                bool isRunning = await ErrorHelper.ExecuteAsyncWithLogging<bool>(
                    $"Checking if process '{procName}' is running with visible window",
                    async () =>
                    {
                        // Use await Task.Run to make this truly async and avoid warning
                        processes = null;
                        await Task.Run(() => 
                        {
                            processes = Process.GetProcessesByName(procName);
                        }, token);
                        
                        bool result = false;
                        foreach (var process in processes)
                        {
                            if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                            {
                                result = true;
                                break;
                            }
                        }
                        return result;
                    },
                    _logger,
                    false);
                
                // Always release processes regardless of result
                SafeReleaseProcessArray(processes);
                
                if (isRunning) return true;
            }

            return false;
        }

        private async Task<bool> CheckBrowserProcessesAsync(CancellationToken token)
        {
            bool result = false;
            bool success = await ErrorHelper.ExecuteAsyncWithCancellationHandling(
                "Checking browser processes",
                async () =>
                {
                    foreach (string procName in _monitoredProcesses.Where(p => !_mediaPlayerProcessNames.Contains(p)))
                    {
                        Process[] browserProcesses = null;
                        try
                        {
                            // Wrap in Task.Run to make it truly asynchronous
                            browserProcesses = null;
                            await Task.Run(() => 
                            {
                                browserProcesses = Process.GetProcessesByName(procName);
                            }, token);
                            
                            foreach (var process in browserProcesses)
                            {
                                token.ThrowIfCancellationRequested();
                                
                                // Use await Task.Run for window visibility check
                                bool isVisible = false;
                                await Task.Run(() => 
                                {
                                    isVisible = process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle);
                                }, token);
                                    
                                if (isVisible)
                                {
                                    string commandLine = await ProcessOperationHelper.GetProcessCommandLineAsync(process.Id, token, _logger);
                                    if (!string.IsNullOrEmpty(commandLine))
                                    {
                                        _logger.Debug($"Found browser process with command line: {commandLine}");
                                        if (commandLine.Contains("--app=") && commandLine.Contains(_currentLaunchedUrl))
                                        {
                                            _logger.Info("Browser process is running in app mode with the target URL.");
                                            result = true;
                                            return; // Exit the forEach loop early
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            SafeReleaseProcessArray(browserProcesses);
                        }
                    }
                },
                token,
                _logger);
                
            return result;
        }

        private void SafeReleaseProcess(Process process)
        {
            ProcessOperationHelper.SafelyDisposeProcess(process, null, _logger);
        }

        private void SafeReleaseProcessArray(Process[] processes)
        {
            ProcessOperationHelper.SafelyDisposeProcesses(processes, _logger);
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public void Stop()
        {
            if (IsDisposed)
                return;

            ErrorHelper.ExecuteWithLogging(
                "Stopping process monitoring",
                () =>
                {
                    if (_processStateSubscription != null)
                    {
                        _processStateSubscription.Dispose();
                        _processStateSubscription = null;
                        _logger.Info("Stopped monitoring processes.");
                    }
                },
                _logger);
        }

        protected override void ReleaseManagedResources()
        {
            ErrorHelper.ExecuteWithLogging(
                "Releasing managed resources in ProcessMonitor",
                () =>
                {
                    _logger.Info("Beginning resource cleanup");

                    // Stop monitoring processes
                    ErrorHelper.SafelyCleanupResource(
                        nameof(ProcessMonitor),
                        "process monitoring",
                        () =>
                        {
                            Stop();
                            _logger.Info("Stopped monitoring processes");
                        });

                    // Unsubscribe from events - Use standard EventHandler version instead
                    CollectionHelper.SafelyRemoveEventHandler(
                        ref ProcessStateChanged,
                        "ProcessStateChanged",
                        _logger);

                    // Clear collections
                    CollectionHelper.SafelyClearCollection(
                        _monitoredProcesses,
                        _logger,
                        "_monitoredProcesses");
                        
                    CollectionHelper.SafelyClearCollection(
                        _mediaPlayerProcessNames,
                        _logger,
                        "_mediaPlayerProcessNames");

                    // Clear URL reference
                    _currentLaunchedUrl = null;

                    // Dispose semaphore
                    ErrorHelper.SafelyCleanupResource(
                        nameof(ProcessMonitor),
                        "process check lock semaphore",
                        () =>
                        {
                            if (_processCheckLock != null)
                            {
                                ResourceCleanup.DisposeSemaphore(_processCheckLock, "ProcessMonitor._processCheckLock");
                            }
                        });
                },
                _logger);
        }

        protected override void ReleaseUnmanagedResources()
        {
            ErrorHelper.ExecuteWithLogging(
                "Releasing unmanaged resources in ProcessMonitor",
                () =>
                {
                    _logger.Info("Beginning unmanaged resource cleanup");

                    // Force a garbage collection to help clean up any RCW (Runtime Callable Wrapper) objects
                    // that might be holding onto COM objects from WMI queries
                    ErrorHelper.SafelyCleanupResource(
                        nameof(ProcessMonitor),
                        "COM objects",
                        () =>
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            // Clean up any unused COM objects
                            Marshal.CleanupUnusedObjectsInCurrentContext();
                            _logger.Info("COM cleanup completed");
                        });
                },
                _logger);
        }
    }
}