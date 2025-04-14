using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;
using PCStreamerLauncher.Windows;
using static PCStreamerLauncher.Helpers.WindowFocusHelper;

namespace PCStreamerLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ContextLogger _logger = Logger.GetLogger<MainWindow>();

        // Core components
        private LauncherService _launcherService;
        private ProcessMonitor _processMonitor;
        private NavigationManager _navigationManager;
        private LauncherButtonProvider _buttonProvider;
        private SoundManager _soundManager;
        private WeatherManager _weatherManager;
        private HelpWindow _helpWindow;
        private Process _launchedProcess;
        private readonly List<DispatcherTimer> _timers = new List<DispatcherTimer>();
        private bool _isExitingSoundPlaying = false;

        // Semaphore to guard termination of non‑Plex processes.
        private readonly SemaphoreSlim _terminationLock = new SemaphoreSlim(1, 1);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("nircmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool DoNirCmd(string lpszCommand);

        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;

        public MainWindow()
        {
            // Start hidden to avoid flashing before initialization.
            this.Visibility = Visibility.Hidden;
            InitializeComponent();
            _logger.Info("Initializing...");

            // Set window size and center on screen.
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Width = screenWidth * 0.95;
            this.Height = screenHeight * 0.95;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;

            // Debug focus state when window becomes visible.
            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible && _navigationManager != null)
                {
                    _logger.Info("Visibility changed to visible");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _navigationManager?.DebugFocusState();
                    }), DispatcherPriority.Input);
                }
            };

            // Initialize components using the configuration file.
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.ConfigFile);
            InitializeComponents(configPath);
            SetupClock();
            InitializeWeatherService();

            // Delay launcher activation for animations and process checks.
            this.Loaded += (s, e) =>
            {
                NotifyWallpaperEngines();
                DispatcherTimer delayTimer = CreateTimer(TimeSpan.FromSeconds(3), (s2, e2) =>
                {
                    ((DispatcherTimer)s2).Stop();
                    _logger.Info("Delay timer elapsed; checking monitored processes.");
                    _processMonitor.Start();
                    // Use Task.Run to avoid blocking the UI thread during activation
                    Task.Run(() => ActivateLauncherAsync());
                });
                delayTimer.Start();
                _logger.Info("Delay timer started for launcher activation.");
            };

            // Handle F1 key to toggle help overlay.
            this.KeyDown += MainWindow_KeyDown;
        }

        private DispatcherTimer CreateTimer(TimeSpan interval, EventHandler callback)
        {
            var timer = new DispatcherTimer { Interval = interval };
            timer.Tick += callback;
            _timers.Add(timer);
            return timer;
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _logger.Info("OnActivated event triggered");
            this.Focus();
            _navigationManager?.SetInitialFocus();
        }

        private void InitializeComponents(string configPath)
        {
            ErrorHelper.ExecuteWithLogging(
                "Initialize application components",
                () =>
                {
                    // Initialize sound manager first.
                    InitializeSoundManager(configPath);

                    // Create LauncherService with browser configuration.
                    var browserConfig = ConfigParser.LoadBrowserConfig(configPath);
                    _launcherService = new LauncherService(_soundManager, browserConfig);

                    // Create ProcessMonitor for external process tracking.
                    _processMonitor = new ProcessMonitor(configPath);

                    // Create LauncherButtonProvider for dynamic UI buttons.
                    _buttonProvider = new LauncherButtonProvider(_launcherService, AppGrid, configPath, this);

                    // Create NavigationManager with keyboard and gamepad support.
                    var navigationConfig = ConfigParser.LoadNavigationConfig(configPath);
                    var gamepadConfig = ConfigParser.LoadGamepadConfig(configPath);
                    var gamepadHandler = gamepadConfig.ContainsKey(ConfigKeys.Gamepad.Enabled) &&
                                         gamepadConfig[ConfigKeys.Gamepad.Enabled].Equals("true", StringComparison.OrdinalIgnoreCase)
                                         ? new GamepadHandler(gamepadConfig)
                                         : null;
                    _navigationManager = new NavigationManager(AppGrid, _soundManager, _launcherService, navigationConfig, gamepadHandler, this);

                    // Initialize help window.
                    InitializeHelpOverlay();

                    // Wire up events between components.
                    WireUpEvents();

                    // Load buttons and attach keyboard handlers.
                    _buttonProvider.LoadButtons();
                    _navigationManager.AttachKeyboardHandlers(this);
                    _logger.Info($"Navigation mode: {_navigationManager.GetNavigationMode()}");
                    _logger.Info("All components initialized.");
                },
                _logger);
        }

        private void InitializeHelpOverlay()
        {
            ErrorHelper.ExecuteWithLogging(
                "Initialize help window",
                () =>
                {
                    // We only create the window when it's needed, not on startup
                    // This happens in the ToggleHelpOverlay method
                    _logger.Info("Help window will be initialized when needed");
                },
                _logger);
        }

        private void WireUpEvents()
        {
            ErrorHelper.ExecuteWithLogging(
                "Wire up component events",
                () =>
                {
                    _processMonitor.ProcessStateChanged += OnProcessStateChanged;
                    _launcherService.StateChanged += OnLauncherStateChanged;
                    _launcherService.PlaySoundRequested += OnPlaySoundRequested;
                    _launcherService.LauncherVisibilityChangeRequested += OnLauncherVisibilityChangeRequested;
                    _launcherService.LaunchAppRequested += LauncherService_LaunchAppRequested;
                    _launcherService.TerminateAppRequested += OnTerminateAppRequested;
                    _launcherService.ButtonsEnableRequested += OnButtonsEnableRequested;
                    _launcherService.ButtonsDisableRequested += OnButtonsDisableRequested;
                    _launcherService.NavigationEnableRequested += OnNavigationEnableRequested;
                    _navigationManager.ButtonActivated += OnButtonActivated;
                    _logger.Info("Events wired up between components.");
                },
                _logger);
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            ErrorHelper.SafelyHandleEvent(
                "MainWindow",
                "KeyDown",
                () =>
                {
                    if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        _logger.Info("Ctrl+H pressed - toggling help overlay");
                        ToggleHelpOverlay();
                        e.Handled = true;
                    }
                });
        }

        public void ToggleHelpOverlay()
        {
            ErrorHelper.ExecuteWithLogging(
                "Toggle help window",
                () =>
                {
                    _logger.Info("Toggling help window");
                    
                    // If the help window is already open, close it
                    if (_helpWindow != null)
                    {
                        _logger.Info("Closing existing help window");
                        _helpWindow.CloseWindow();
                        return;
                    }
                    
                    // Create a new help window
                    _helpWindow = new HelpWindow(_soundManager, _navigationManager?.GamepadHandler);
                    
                    // When the help window is closed, show the launcher again
                    _helpWindow.HelpWindowClosed += (s, e) =>
                    {
                        _logger.Info("Help window closed event received");
                        _helpWindow = null;
                        
                        // Show the launcher again
                        this.Show();
                        this.Activate();
                        this.Focus();
                    };
                    
                    // Hide the launcher
                    this.Hide();
                    
                    // Show the help window
                    _helpWindow.Show();
                },
                _logger);
        }

        private void InitializeSoundManager(string configPath)
        {
            var soundConfig = ConfigParser.LoadSection(configPath, ConfigKeys.Sections.Sound);
            string soundsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.SoundsDir);
            _soundManager = new SoundManager(soundsPath);
            if (soundConfig.TryGetValue(ConfigKeys.Sound.Enabled, out string soundEnabledValue))
            {
                bool isSoundEnabled = string.Equals(soundEnabledValue, "true", StringComparison.OrdinalIgnoreCase);
                _soundManager.SetEnabled(isSoundEnabled);
                _logger.Info($"Sound enabled = {isSoundEnabled}");
            }
        }

        private void NotifyWallpaperEngines()
        {
            string[] wallpaperClasses = new string[]
            {
                "Progman", "WorkerW", "RocketDock", "WallpaperEngine", null
            };

            foreach (var className in wallpaperClasses)
            {
                IntPtr hwnd = FindWindow(className, null);
                if (hwnd != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero);
                    _logger.Info($"Sent restore message to {className ?? "default"} window.");
                }
            }
        }
        private async void LauncherService_LaunchAppRequested(object sender, CommandInfo commandInfo)
        {
            try
            {
                if (commandInfo.IsWeb)
                {
                    _processMonitor.SetCurrentLaunchedUrl(commandInfo.RawCommand);
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = GetBrowserExecutable(),
                        Arguments = $"--app={commandInfo.RawCommand}",
                        UseShellExecute = true
                    };
                    _launchedProcess = Process.Start(psi);
                    await _launcherService.NotifyProcessLaunchedAsync(_launchedProcess);
                    
                    // Wait a moment for browser to initialize
                    await Task.Delay(2000);
                    
                    // Force focus on the web app
                    _logger.Info("Forcing focus on web application window");
                    await FocusAppProcessAsync(_launchedProcess, 5);
                    
                    // Send F11 for fullscreen after focusing
                    bool result = DoNirCmd("sendkey F11 press");
                    _logger.Info($"DoNirCmd F11 result: {result}");
                }
                else
                {
                    _processMonitor.SetCurrentLaunchedUrl(null);
                    _launchedProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = commandInfo.RawCommand,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(commandInfo.RawCommand)
                    });
                    await _launcherService.NotifyProcessLaunchedAsync(_launchedProcess);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error launching application", ex);
                MessageBox.Show("Failed to launch the application. Please check your configuration.");
                _launcherService.TerminateApplication();
            }
        }

        // FIXED: Changed from async void to async Task
        public async Task PerformDelayedExitAsync()
        {
            if (_isExitingSoundPlaying)
                return;

            _isExitingSoundPlaying = true;
            try
            {
                _soundManager?.PlaySound("Return");
                _logger.Info("Playing exit sound before shutdown");
                await Task.Delay(1500);
                _logger.Info("Delayed exit completed, closing application");
            }
            catch (Exception ex)
            {
                _logger.Error("Error during delayed exit", ex);
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExitingSoundPlaying && _soundManager != null)
            {
                _logger.Info("OnClosing event - initiating delayed exit");
                e.Cancel = true;
                // Fire-and-forget with error logging using Task.Run
                Task.Run(async () =>
                {
                    try
                    {
                        await PerformDelayedExitAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error in delayed exit task", ex);
                    }
                });
                return;
            }
            base.OnClosing(e);
        }

        private void OnProcessStateChanged(object sender, bool isRunning)
        {
            _launcherService.NotifyProcessRunning(isRunning);
        }

        private void OnLauncherStateChanged(object sender, LauncherState state)
        {
            _logger.Info($"LauncherService state changed to {state}");
        }

        private void OnPlaySoundRequested(object sender, string soundName)
        {
            _soundManager?.PlaySound(soundName);
        }

        private void OnLauncherVisibilityChangeRequested(object sender, bool show)
        {
            if (show)
                ActivateLauncherAsync().FireAndForgetWithLogging(_logger);
            else
                this.Hide();
        }

        // FIXED: Changed from async void to async Task and renamed
        // FIXED: Keep async void for event handler but use proper pattern
        private async void OnTerminateAppRequested(object sender, EventArgs e)
        {
            try
            {
                _logger.Info("Received request to terminate application");
                await TerminateLaunchedProcessAsync();
                _logger.Info("Application termination completed");
            }
            catch (Exception ex)
            {
                _logger.Error("Error handling terminate app request", ex);
            }
        }

        private void OnButtonsEnableRequested(object sender, EventArgs e)
        {
            EnableLauncherButtons();
        }

        private void OnButtonsDisableRequested(object sender, EventArgs e)
        {
            DisableLauncherButtons();
        }
        
        private void OnNavigationEnableRequested(object sender, bool enabled)
        {
            if (_navigationManager != null)
            {
                _logger.Info($"Setting navigation enabled: {enabled}");
                _navigationManager.SetNavigationEnabled(enabled);
            }
        }

        private void OnButtonActivated(object sender, CommandInfo commandInfo)
        {
            _ = _launcherService.LaunchApplicationAsync(commandInfo);
        }
        // Updated termination logic using ProcessOperationHelper.
        private async Task TerminateLaunchedProcessAsync()
        {
            await ErrorHelper.ExecuteAsyncWithLogging(
                "Terminate launched application",
                async () =>
                {
                    _processMonitor.SetCurrentLaunchedUrl(null);
                    if (_launchedProcess != null && !_launchedProcess.HasExited)
                    {
                        string processName = _launchedProcess.ProcessName;
                        // If the process is Plex, use the direct termination method.
                        if (processName.IndexOf("Plex", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.Info($"Terminating Plex process directly: {processName} (ID: {_launchedProcess.Id})");
                            ProcessOperationHelper.SafelyKillProcess(_launchedProcess, 3000, processName, _logger);
                            _launchedProcess = null;
                        }
                        else
                        {
                            int processId = _launchedProcess.Id;
                            processName = _launchedProcess.ProcessName;
                            _launchedProcess = null;
                            _logger.Info($"Killing process tree for {processName} (ID: {processId})");
                            await ProcessOperationHelper.SafelyTerminateProcessTree(processId, _logger);
                            
                            // Safety net: ensure all processes with this name are terminated
                            await KillProcessesByNameAsync(processName);
                        }
                    }
                    
                    // Also, terminate any browser processes that may have been launched.
                    string browserProcName = Path.GetFileNameWithoutExtension(GetBrowserExecutable());
                    _logger.Info($"Checking for browser processes ({browserProcName}) to terminate");
                    await KillProcessesByNameAsync(browserProcName);
                },
                _logger);
        }

        // This method is a wrapper around ProcessOperationHelper.SafelyTerminateProcessTree
        // to maintain compatibility with existing code
        private async Task KillProcessAndChildrenAsync(int processId)
        {
            await _terminationLock.WaitAsync();
            try
            {
                await ProcessOperationHelper.SafelyTerminateProcessTree(processId, _logger);
            }
            finally
            {
                _terminationLock.Release();
            }
        }

        private async Task KillProcessesByNameAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            await ErrorHelper.ExecuteAsyncWithLogging(
                $"Kill processes by name: {processName}",
                async () =>
                {
                    await _terminationLock.WaitAsync();
                    try
                    {
                        // Get all processes with the specified name
                        Process[] processes = Process.GetProcessesByName(processName);
                        _logger.Info($"Found {processes.Length} process(es) named {processName}");
                        
                        // Kill each process individually
                        var killTasks = new List<Task>();
                        foreach (var proc in processes)
                        {
                            killTasks.Add(Task.Run(() => ProcessOperationHelper.SafelyKillProcess(proc, 3000, proc.ProcessName, _logger)));
                        }

                        if (killTasks.Count > 0)
                        {
                            await Task.WhenAll(killTasks);
                        }

                        // Ensure proper cleanup of process objects
                        ProcessOperationHelper.SafelyDisposeProcesses(processes, _logger);
                        
                        // Final safety check with taskkill
                        await Task.Run(() => ProcessOperationHelper.ExecuteTaskKill($"/F /IM \"{processName}.exe\"", _logger));
                    }
                    finally
                    {
                        _terminationLock.Release();
                    }
                },
                _logger);
        }
        private string GetBrowserExecutable()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.ConfigFile);
            var browserConfig = ConfigParser.LoadBrowserConfig(configPath);
            if (browserConfig.ContainsKey(ConfigKeys.Browser.DefaultBrowserExecutable))
            {
                return browserConfig[ConfigKeys.Browser.DefaultBrowserExecutable].Replace("\"", "").Trim();
            }
            return @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        }

        private void EnableLauncherButtons()
        {
            foreach (var btn in AppGrid.Children.OfType<Button>())
            {
                btn.IsEnabled = true;
            }
            _logger.Info("Launcher buttons enabled.");
        }

        private void DisableLauncherButtons()
        {
            foreach (var btn in AppGrid.Children.OfType<Button>())
            {
                btn.IsEnabled = false;
            }
            _logger.Info("Launcher buttons disabled.");
        }

        // FIXED: Changed from async void to async Task
        public async Task ActivateLauncherAsync()
        {
            _logger.Info("Activating launcher...");

            // UI updates need to be on the UI thread
            await Dispatcher.InvokeAsync(() => {
                this.Show();
                this.WindowState = WindowState.Normal;

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                this.Width = screenWidth * 0.95;
                this.Height = screenHeight * 0.95;
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = (screenHeight - this.Height) / 2;
                this.Topmost = true;

                // Use multiple methods to ensure foreground status
                SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                this.Activate();
                this.Focus();

                // Force window to be shown and active
                this.ShowActivated = true;
            });

            NotifyWallpaperEngines();

            // Only load buttons once on startup, not every time the launcher becomes visible
            if (!_buttonProvider._buttonsLoaded)
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.ConfigFile);
                await _buttonProvider.LoadButtonsAsync();
            }

            // Always enable buttons when activating the launcher
            EnableLauncherButtons();

            // Use async/await pattern for delays and focus handling
            await Task.Delay(250);

            await Dispatcher.InvokeAsync(() => {
                // Force window activation first
                this.Activate();
                this.Focus();
            });

            // Then use another timer for setting button focus
            await Task.Delay(100);

            await Dispatcher.InvokeAsync(() => {
                _navigationManager.SetInitialFocus();
                _navigationManager.DebugFocusState();
                _navigationManager.DebugKeyboardEvents();
            });

            _logger.Info("Launcher activated and buttons refreshed.");
        }

        private void SetupClock()
        {
            var clockTimer = CreateTimer(TimeSpan.FromSeconds(1), (s, e) =>
            {
                ClockTextBlock.Text = DateTime.Now.ToString("hh:mm:ss tt");
            });
            clockTimer.Start();
            _logger.Info("Clock timer started.");
        }

        private void InitializeWeatherService()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.ConfigFile);
                var weatherConfig = ConfigParser.LoadSection(configPath, "Weather");

                if (weatherConfig.TryGetValue("Enable", out string enabledValue) &&
                    enabledValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info("Weather service disabled via config.");
                    WeatherBorder.Visibility = Visibility.Collapsed;
                    return;
                }

                _weatherManager = new WeatherManager(
                    configPath,
                    CityTextBlock,
                    ConditionsTextBlock,
                    TemperatureTextBlock,
                    HumidityTextBlock,
                    WindTextBlock,
                    WeatherIcon
                );
                _ = _weatherManager.StartAsync(15);
                _logger.Info("Weather service initialized.");
            }
            catch (Exception ex)
            {
                _logger.Error("Error initializing weather service", ex);
                CityTextBlock.Text = "Weather unavailable";
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            _logger.Info("OnClosed called - beginning cleanup");
            try
            {
                CleanupResources();
            }
            catch (Exception ex)
            {
                _logger.Error("Error during cleanup", ex);
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        private void CleanupResources()
        {
            _logger.Info("OnClosed called - beginning cleanup");
            
            ErrorHelper.ExecuteWithLogging(
                "Cleanup application resources",
                () =>
                {
                    // Clean up ProcessMonitor
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "ProcessMonitor",
                        () =>
                        {
                            if (_processMonitor != null)
                            {
                                _logger.Debug("Removing ProcessMonitor.ProcessStateChanged event handler");
                                _processMonitor.ProcessStateChanged -= OnProcessStateChanged;
                                _processMonitor.Dispose();
                                _processMonitor = null;
                            }
                        });

                    // Clean up NavigationManager
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "NavigationManager",
                        () =>
                        {
                            if (_navigationManager != null)
                            {
                                _logger.Debug("Removing NavigationManager.ButtonActivated event handler");
                                _navigationManager.ButtonActivated -= OnButtonActivated;
                                _navigationManager.Dispose();
                                _navigationManager = null;
                            }
                        });

                    // Clean up WeatherManager
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "WeatherManager",
                        () =>
                        {
                            if (_weatherManager != null)
                            {
                                _weatherManager.Dispose();
                                _weatherManager = null;
                            }
                        });

                    // Clean up LauncherService
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "LauncherService",
                        () =>
                        {
                            if (_launcherService != null)
                            {
                                // Detach standard event handlers
                                _logger.Debug("Removing LauncherService event handlers");
                                _launcherService.StateChanged -= OnLauncherStateChanged;
                                _launcherService.PlaySoundRequested -= OnPlaySoundRequested;
                                _launcherService.LauncherVisibilityChangeRequested -= OnLauncherVisibilityChangeRequested;
                                _launcherService.LaunchAppRequested -= LauncherService_LaunchAppRequested;
                                _launcherService.TerminateAppRequested -= OnTerminateAppRequested;
                                _launcherService.ButtonsEnableRequested -= OnButtonsEnableRequested;
                                _launcherService.ButtonsDisableRequested -= OnButtonsDisableRequested;
                                _launcherService.NavigationEnableRequested -= OnNavigationEnableRequested;

                                _launcherService.Dispose();
                                _launcherService = null;
                            }
                        });

                    // Clean up LauncherButtonProvider
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "LauncherButtonProvider",
                        () =>
                        {
                            if (_buttonProvider != null)
                            {
                                _buttonProvider.Dispose();
                                _buttonProvider = null;
                            }
                        });

                    // Clean up SoundManager
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "SoundManager",
                        () =>
                        {
                            if (_soundManager != null)
                            {
                                _soundManager.Dispose();
                                _soundManager = null;
                            }
                        });

                    // Clean up launched process
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "LaunchedProcess",
                        () =>
                        {
                            if (_launchedProcess != null)
                            {
                                ResourceCleanup.DisposeProcess(_launchedProcess, "MainWindow._launchedProcess");
                                _launchedProcess = null;
                            }
                        });

                    // Clean up help window
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "HelpWindow",
                        () =>
                        {
                            if (_helpWindow != null)
                            {
                                _logger.Debug("Closing HelpWindow during cleanup");
                                try
                                {
                                    // Close the window
                                    UIOperationHelper.InvokeOnUIThread(
                                        () => _helpWindow.Close(),
                                        "Close help window",
                                        _logger);
                                    
                                    _logger.Info("Help window closed during cleanup");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error("Error closing help window", ex);
                                }
                                
                                _helpWindow = null;
                            }
                        });

                    // Clean up timers
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "Timers",
                        () =>
                        {
                            // First stop and dispose each timer
                            foreach (var timer in _timers)
                            {
                                ResourceCleanup.StopAndDisposeTimer(timer, null, "MainWindow timer");
                            }
                            
                            // Then safely clear the collection
                            CollectionHelper.SafelyClearCollection(_timers, _logger, "Timer collection");
                        });

                    // Dispose semaphore
                    ErrorHelper.SafelyCleanupResource(
                        "MainWindow", "TerminationLock",
                        () =>
                        {
                            ResourceCleanup.DisposeSemaphore(_terminationLock, "MainWindow._terminationLock");
                        });

                    _logger.Info("Resource cleanup complete");
                },
                _logger);
        }
    }
}