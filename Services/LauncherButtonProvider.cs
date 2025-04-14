
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;
using static PCStreamerLauncher.TaskExtensions;

namespace PCStreamerLauncher
{
    public class LauncherButtonProvider : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<LauncherButtonProvider>();

        private readonly LauncherService _launcherService;
        private readonly UniformGrid _appGrid;
        private readonly Window _parentWindow;
        private readonly string _configPath;
        private readonly string _imagesPath;
        private readonly Dictionary<Button, RoutedEventHandler> _buttonClickHandlers = new Dictionary<Button, RoutedEventHandler>();
        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private WeakReference<Window> _parentWindowWeakRef;
        private System.Diagnostics.Stopwatch _totalLoadTime = new System.Diagnostics.Stopwatch();
        private DateTime _lastButtonFocusTime = DateTime.Now;
        private int _successfulButtonLoads = 0;
        private int _failedButtonLoads = 0;
        private Dictionary<string, System.Diagnostics.Stopwatch> _operationTimers = new Dictionary<string, System.Diagnostics.Stopwatch>();

        // Cache for configuration data
        private Dictionary<string, string> _cachedCommands;
        private Dictionary<string, string> _cachedMediaPlayers;
        public bool _buttonsLoaded = false;

        public LauncherButtonProvider(LauncherService launcherService, UniformGrid appGrid, string configPath, Window parentWindow)
        {
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);
            _logger.Info($"[Operation:{operationId}] Initializing with parameters");

            _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
            _appGrid = appGrid ?? throw new ArgumentNullException(nameof(appGrid));
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            _imagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigKeys.Files.ImagesDir);

            // Use a weak reference to avoid circular references
            _parentWindowWeakRef = new WeakReference<Window>(parentWindow);

            _logger.Info($"[Operation:{operationId}] Initialized with images path: {_imagesPath}");

            // Check if images directory exists and log count of available images
            if (!Directory.Exists(_imagesPath))
            {
                _logger.Warning($"[Operation:{operationId}] ATTENTION - Images directory does not exist: {_imagesPath}");
            }
            else
            {
                var imageFiles = Directory.GetFiles(_imagesPath, "*.png", SearchOption.TopDirectoryOnly);
                _logger.Info($"[Operation:{operationId}] Found {imageFiles.Length} PNG image files in images directory");

                // Log specific image filenames for debugging
                if (imageFiles.Length > 0)
                {
                    _logger.Info($"[Operation:{operationId}] Available images: {string.Join(", ", imageFiles.Select(f => Path.GetFileName(f)))}");
                }
            }

            // Load config once at startup
            _cachedCommands = ConfigParser.LoadCommands(_configPath);
            _cachedMediaPlayers = ConfigParser.LoadMediaPlayers(_configPath);
            _logger.Info($"[Operation:{operationId}] Loaded {_cachedCommands.Count} commands and {_cachedMediaPlayers.Count} media players from config");
        }
        public async Task LoadButtonsAsync()
        {
            ThrowIfDisposed();
            string operationId = Guid.NewGuid().ToString().Substring(0, 8);

            // If buttons are already loaded, skip recreation
            if (_buttonsLoaded)
            {
                _logger.Info($"[Operation:{operationId}] Buttons already loaded, skipping recreation");
                return;
            }

            await ErrorHelper.ExecuteAsyncWithLogging(
                $"Load launcher buttons [Op:{operationId}]",
                async () =>
                {
                    _logger.Info($"[Operation:{operationId}] Beginning button loading process");
                    _totalLoadTime.Restart();
                    _lastButtonFocusTime = DateTime.Now;
                    _successfulButtonLoads = 0;
                    _failedButtonLoads = 0;

                    _logger.Info($"[Operation:{operationId}] Clearing existing buttons");
                    ClearExistingButtons();

                    if (!Directory.Exists(_imagesPath))
                    {
                        _logger.Error($"[Operation:{operationId}] Images directory not found: {_imagesPath}");
                        MessageBox.Show($"Images directory not found: {_imagesPath}");
                        return;
                    }

                    _logger.Info($"[Operation:{operationId}] Using cached configuration with {_cachedCommands.Count} commands and {_cachedMediaPlayers.Count} media players");

                    StartOperation("ImageDiscovery", operationId);
                    string[] imageFiles = Directory.GetFiles(_imagesPath, "*.png", SearchOption.TopDirectoryOnly);
                    StopOperation("ImageDiscovery", operationId);

                    _logger.Info($"[Operation:{operationId}] Found {imageFiles.Length} image files");

                    StartOperation("ButtonCreation", operationId);
                    int buttonCount = 0;
                    var buttonCreationTasks = new List<Task<Button>>();
                    int configuredButtonsFound = 0;

                    foreach (string imageFile in imageFiles)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(imageFile);

                        // Skip special buttons (Restart, Exit) for now
                        if (baseName.Equals("Restart", StringComparison.OrdinalIgnoreCase) ||
                            baseName.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Info($"[Operation:{operationId}] Skipping special button '{baseName}' for later processing");
                            continue;
                        }

                        // Try to find command in either media players or commands sections
                        string rawCommand = null;
                        string sourceSection = null;

                        if (_cachedMediaPlayers.ContainsKey(baseName))
                        {
                            rawCommand = _cachedMediaPlayers[baseName];
                            sourceSection = "MediaPlayers";
                        }
                        else if (_cachedCommands.ContainsKey(baseName))
                        {
                            rawCommand = _cachedCommands[baseName];
                            sourceSection = "Commands";
                        }

                        if (string.IsNullOrWhiteSpace(rawCommand))
                        {
                            _logger.Warning($"[Operation:{operationId}] Image '{baseName}' has no matching command in config, skipping");
                            continue;
                        }

                        configuredButtonsFound++;
                        bool isWeb = rawCommand.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                        var commandInfo = new CommandInfo { RawCommand = rawCommand, IsWeb = isWeb };

                        _logger.Info($"[Operation:{operationId}] Creating button for '{baseName}' from {sourceSection} section ({(isWeb ? "Web" : "Application")})");
                        buttonCreationTasks.Add(CreateAppButtonAsync(baseName, imageFile, commandInfo, operationId));
                        buttonCount++;
                    }

                    _logger.Info($"[Operation:{operationId}] Found {configuredButtonsFound} configured buttons out of {imageFiles.Length} image files");
                    _logger.Info($"[Operation:{operationId}] Waiting for {buttonCreationTasks.Count} button creation tasks to complete");
                    Button[] buttons = await Task.WhenAll(buttonCreationTasks);

                    int addedButtons = 0;
                    foreach (var button in buttons)
                    {
                        if (button != null)
                        {
                            _appGrid.Children.Add(button);
                            addedButtons++;
                        }
                    }

                    _logger.Info($"[Operation:{operationId}] Added {addedButtons} buttons to the grid (out of {buttons.Length} attempted)");
                    StopOperation("ButtonCreation", operationId);

                    // Add special buttons
                    _logger.Info($"[Operation:{operationId}] Adding special buttons");
                    StartOperation("SpecialButtons", operationId);

                    await AddSpecialButtonAsync("Restart", (s, e) => {
                        _logger.Info($"[Operation:{operationId}] User clicked Restart button");
                        System.Diagnostics.Process.Start("shutdown", "/r /t 0");
                    }, operationId);

                    Window window = null;
                    if (_parentWindowWeakRef.TryGetTarget(out window) && window != null && window is MainWindow mainWindow)
                    {
                        await AddSpecialButtonAsync("Exit", (s, e) =>
                        {
                            _logger.Info($"[Operation:{operationId}] User clicked Exit button");
                            mainWindow.PerformDelayedExitAsync().FireAndForgetWithLogging(_logger);
                        }, operationId);
                    }
                    else
                    {
                        await AddSpecialButtonAsync("Exit", (s, e) =>
                        {
                            _logger.Info($"[Operation:{operationId}] User clicked Exit button");
                            if (_parentWindowWeakRef.TryGetTarget(out Window parentWindow) && parentWindow != null)
                                parentWindow.Close();
                        }, operationId);
                    }

                    buttonCount += 2; // For Restart and Exit buttons
                    StopOperation("SpecialButtons", operationId);

                    // Configure the grid layout
                    if (buttonCount > 0)
                    {
                        StartOperation("GridLayout", operationId);
                        int columns = Math.Min(6, buttonCount);
                        int rows = (int)Math.Ceiling((double)buttonCount / columns);
                        _appGrid.Columns = columns;
                        _appGrid.Rows = rows;
                        _logger.Info($"[Operation:{operationId}] Set grid to {columns} columns and {rows} rows for {buttonCount} buttons");
                        StopOperation("GridLayout", operationId);
                    }

                    _totalLoadTime.Stop();
                    _logger.Info($"[Operation:{operationId}] Button loading completed in {_totalLoadTime.ElapsedMilliseconds}ms with {_successfulButtonLoads} successful and {_failedButtonLoads} failed buttons");

                    // Log timing information for major operations
                    foreach (var timer in _operationTimers)
                    {
                        _logger.Info($"[Operation:{operationId}] Operation '{timer.Key}' took {timer.Value.ElapsedMilliseconds}ms");
                    }
                    _operationTimers.Clear();

                    // Mark buttons as loaded
                    _buttonsLoaded = true;
                },
                _logger);
        }

        public void LoadButtons()
        {
            _logger.Info("LoadButtons called - starting async loading process");
            _ = LoadButtonsAsync();
        }

        private void StartOperation(string operationName, string operationId)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            _operationTimers[operationName] = stopwatch;
            _logger.Info($"[Operation:{operationId}] Started operation '{operationName}'");
        }

        private void StopOperation(string operationName, string operationId)
        {
            if (_operationTimers.TryGetValue(operationName, out var stopwatch))
            {
                stopwatch.Stop();
                _logger.Info($"[Operation:{operationId}] Completed operation '{operationName}' in {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        private void ClearExistingButtons()
        {
            if (_appGrid == null)
                return;

            ErrorHelper.ExecuteWithLogging(
                "Clear existing buttons", 
                () =>
                {
                    _logger.Info($"Clearing {_appGrid.Children.Count} existing buttons and {_buttonClickHandlers.Count} event handlers");
                    System.Diagnostics.Stopwatch clearTimer = System.Diagnostics.Stopwatch.StartNew();

                    // First, detach all event handlers from buttons
                    foreach (var kvp in _buttonClickHandlers)
                    {
                        if (kvp.Key != null && kvp.Value != null)
                        {
                            // Safely remove event handlers
                            ErrorHelper.SafelyCleanupResource(
                                "LauncherButtonProvider", $"Button-{GetButtonName(kvp.Key)}",
                                () =>
                                {
                                    kvp.Key.Click -= kvp.Value;
                                    kvp.Key.GotFocus -= Button_GotFocus;
                                    kvp.Key.LostFocus -= Button_LostFocus;

                                    // Clear image sources to allow GC to collect them
                                    if (kvp.Key.Content is StackPanel panel)
                                    {
                                        foreach (var child in panel.Children)
                                        {
                                            if (child is Image img)
                                            {
                                                ResourceCleanup.CleanupImage(img, $"ButtonImage[{GetButtonName(kvp.Key)}]");
                                            }
                                        }
                                        panel.Children.Clear();
                                    }

                                    // Clear button resources
                                    kvp.Key.Resources.Clear();

                                    // Use ResourceCleanup for transform animations
                                    if (kvp.Key.RenderTransform != null)
                                    {
                                        ResourceCleanup.StopTransformAnimations(kvp.Key.RenderTransform, $"ButtonTransform[{GetButtonName(kvp.Key)}]");
                                    }
                                });
                        }
                    }
                    
                    // Clear the event handlers dictionary
                    CollectionHelper.SafelyClearCollection(_buttonClickHandlers, _logger, "_buttonClickHandlers");

                    int buttonCount = _appGrid.Children.Count;
                    // Now clear the grid's children collection
                    _appGrid.Children.Clear();

                    clearTimer.Stop();
                    _logger.Info($"Cleared {buttonCount} existing buttons in {clearTimer.ElapsedMilliseconds}ms");
                },
                _logger);
        }

        private async Task<Button> CreateAppButtonAsync(string name, string imagePath, CommandInfo commandInfo, string operationId)
        {
            return await ErrorHelper.ExecuteAsyncWithLogging(
                $"Create application button for {name} [Op:{operationId}]",
                async () =>
                {
                    System.Diagnostics.Stopwatch buttonTimer = System.Diagnostics.Stopwatch.StartNew();
                    _logger.Info($"[Operation:{operationId}] Creating button for {name} from {imagePath}");

                    Button button = new Button
                    {
                        Tag = commandInfo,
                        Style = Application.Current.FindResource("RoundedTransparentHoverButtonStyle") as Style,
                        Width = 200,
                        Height = 200,
                        Margin = new Thickness(10),
                        Focusable = true
                    };

                    StackPanel stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    TextBlock textBlock = new TextBlock
                    {
                        Text = name,
                        FontSize = 14,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    stackPanel.Children.Add(textBlock);

                    Image image = new Image
                    {
                        Width = 150,
                        Height = 150,
                        Margin = new Thickness(5)
                    };
                    stackPanel.Children.Insert(0, image);
                    button.Content = stackPanel;

                    _logger.Info($"[Operation:{operationId}] Loading image for {name}");
                    BitmapImage bitmap = await LoadImageAsync(imagePath, name, operationId);
                    if (bitmap != null)
                    {
                        image.Source = bitmap;
                        _logger.Info($"[Operation:{operationId}] Successfully loaded image for {name}");
                    }
                    else
                    {
                        _logger.Warning($"[Operation:{operationId}] Failed to load image for {name}, button will show text only");
                    }

                    RoutedEventHandler clickHandler = async (s, e) =>
                    {
                        string clickId = Guid.NewGuid().ToString().Substring(0, 8);
                        _logger.Info($"[Click:{clickId}] Button '{name}' clicked, launching {(commandInfo.IsWeb ? "web URL" : "application")}: {commandInfo.RawCommand}");
                        await _launcherService.LaunchApplicationAsync(commandInfo);
                    };
                    button.Click += clickHandler;
                    _buttonClickHandlers[button] = clickHandler;
                    button.GotFocus += Button_GotFocus;
                    button.LostFocus += Button_LostFocus;

                    buttonTimer.Stop();
                    _successfulButtonLoads++;
                    _logger.Info($"[Operation:{operationId}] Created button for {name} in {buttonTimer.ElapsedMilliseconds}ms");
                    return button;
                },
                _logger,
                null);
        }

        private async Task<BitmapImage> LoadImageAsync(string imagePath, string imageName, string operationId)
        {
            return await ErrorHelper.ExecuteAsyncWithLogging(
                $"Load image {imageName} [Op:{operationId}]",
                async () =>
                {
                    System.Diagnostics.Stopwatch imageLoadTimer = System.Diagnostics.Stopwatch.StartNew();
                    ThrowIfDisposed();

                    if (!File.Exists(imagePath))
                    {
                        _logger.Warning($"[Operation:{operationId}] Image file not found: {imagePath}");
                        return null;
                    }

                    // Check if image is already in cache
                    if (_imageCache.TryGetValue(imagePath, out BitmapImage cachedImage))
                    {
                        imageLoadTimer.Stop();
                        _logger.Info($"[Operation:{operationId}] Using cached image for {imageName} ({imageLoadTimer.ElapsedMilliseconds}ms)");
                        return cachedImage;
                    }

                    _logger.Info($"[Operation:{operationId}] Loading image from disk: {imagePath}");

                    return await Task.Run(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            long fileSize = new FileInfo(imagePath).Length;
                            _logger.Info($"[Operation:{operationId}] Reading {fileSize} bytes for image {imageName}");

                            using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                            {
                                byte[] imageData = new byte[stream.Length];
                                stream.Read(imageData, 0, (int)stream.Length);

                                using (var memoryStream = new MemoryStream(imageData))
                                {
                                    bitmap.BeginInit();
                                    bitmap.StreamSource = memoryStream;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze(); // Important to freeze the bitmap for thread safety
                                }
                            }

                            _imageCache[imagePath] = bitmap;
                            imageLoadTimer.Stop();

                            // Get dimensions for logging
                            int width = bitmap.PixelWidth;
                            int height = bitmap.PixelHeight;
                            _logger.Info($"[Operation:{operationId}] Successfully loaded image for {imageName} ({width}x{height}) in {imageLoadTimer.ElapsedMilliseconds}ms");

                            return bitmap;
                        }
                        catch (Exception ex)
                        {
                            imageLoadTimer.Stop();
                            _logger.Error($"[Operation:{operationId}] Error loading image for {imageName}: {ex.Message}", ex);
                            return null;
                        }
                    });
                },
                _logger,
                null);
        }
        private void Button_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string buttonName = GetButtonName(button);
                string buttonType = button.Tag is CommandInfo commandInfo ?
                    (commandInfo.IsWeb ? "Web" : "Application") : "Special";

                button.Tag = "VisuallyFocused";

                // Calculate time since last focus to detect rapid navigation
                TimeSpan timeSinceLastFocus = DateTime.Now - _lastButtonFocusTime;
                _lastButtonFocusTime = DateTime.Now;

                if (timeSinceLastFocus.TotalMilliseconds < 200)
                {
                    _logger.Info($"Rapid focus change detected - Button '{buttonName}' got focus after only {timeSinceLastFocus.TotalMilliseconds:F0}ms");
                }
                else
                {
                    _logger.Info($"Button '{buttonName}' ({buttonType}) got focus");
                }
            }
        }

        private void Button_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string buttonName = GetButtonName(button);
                if (!(button.Tag is CommandInfo))
                {
                    button.Tag = "NotFocused";
                    _logger.Info($"Button '{buttonName}' lost focus");
                }
            }
        }

        private string GetButtonName(Button button)
        {
            try
            {
                if (button.Content is StackPanel panel)
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is TextBlock text)
                            return text.Text;
                    }
                }

                // If direct TextBlock content
                if (button.Content is TextBlock directText)
                {
                    return directText.Text;
                }

                // Try to get command info
                if (button.Tag is CommandInfo commandInfo)
                {
                    string filename = Path.GetFileName(commandInfo.RawCommand);
                    return $"{filename} (from tag)";
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error getting button name: {ex.Message}");
            }
            return "Unknown";
        }

        private async Task AddSpecialButtonAsync(string buttonName, RoutedEventHandler clickAction, string operationId)
        {
            await ErrorHelper.ExecuteAsyncWithLogging(
                $"Add special button {buttonName} [Op:{operationId}]",
                async () =>
                {
                    System.Diagnostics.Stopwatch specialButtonTimer = System.Diagnostics.Stopwatch.StartNew();
                    _logger.Info($"[Operation:{operationId}] Creating special button '{buttonName}'");

                    Button button = new Button
                    {
                        Width = 200,
                        Height = 200,
                        Margin = new Thickness(10),
                        Style = Application.Current.FindResource("RoundedTransparentHoverButtonStyle") as Style,
                        Focusable = true
                    };

                    StackPanel stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    TextBlock textBlock = new TextBlock
                    {
                        Text = buttonName,
                        FontSize = 14,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0)
                    };

                    stackPanel.Children.Add(textBlock);

                    Image image = new Image
                    {
                        Width = 150,
                        Height = 150,
                        Margin = new Thickness(5)
                    };

                    stackPanel.Children.Insert(0, image);
                    button.Content = stackPanel;
                    button.Click += clickAction;
                    _buttonClickHandlers[button] = clickAction;
                    button.GotFocus += Button_GotFocus;
                    button.LostFocus += Button_LostFocus;

                    _appGrid.Children.Add(button);
                    _logger.Info($"[Operation:{operationId}] Added special button '{buttonName}' to grid");

                    string imagePath = Path.Combine(_imagesPath, buttonName + ".png");
                    _logger.Info($"[Operation:{operationId}] Looking for special button image at {imagePath}");

                    if (File.Exists(imagePath))
                    {
                        var bitmap = await LoadImageAsync(imagePath, buttonName, operationId);
                        if (bitmap != null)
                        {
                            Window window = null;
                            if (_parentWindowWeakRef.TryGetTarget(out window) && window != null)
                            {
                                await UIOperationHelper.InvokeOnUIThreadAsync(
                                    () => image.Source = bitmap,
                                    $"Set image for special button '{buttonName}'" , 
                                    _logger);
                            }
                        }
                        else
                        {
                            _logger.Warning($"[Operation:{operationId}] Failed to load image for special button '{buttonName}'");
                        }
                    }
                    else
                    {
                        _logger.Warning($"[Operation:{operationId}] Image file for special button '{buttonName}' not found at {imagePath}");
                    }

                    specialButtonTimer.Stop();
                    _logger.Info($"[Operation:{operationId}] Completed special button '{buttonName}' creation in {specialButtonTimer.ElapsedMilliseconds}ms");
                },
                _logger);
        }
        protected override void ReleaseManagedResources()
        {
            string disposeId = Guid.NewGuid().ToString().Substring(0, 8);
            System.Diagnostics.Stopwatch disposeTimer = System.Diagnostics.Stopwatch.StartNew();

            ErrorHelper.ExecuteWithLogging(
                $"Release launcher button provider resources [Op:{disposeId}]",
                () =>
                {
                    _logger.Info($"[Operation:{disposeId}] Beginning resource cleanup");

                    // Clear existing buttons and event handlers first
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "Buttons",
                        () => ClearExistingButtons());

                    // Clear image cache safely
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "ImageCache",
                        () =>
                        {
                            if (_imageCache != null)
                            {
                                int cacheSize = _imageCache.Count;
                                // Use ResourceCleanup to clear the dictionary and handle proper bitmap disposal
                                ResourceCleanup.ClearDictionary(_imageCache, true, $"LauncherButtonProvider.ImageCache [Op:{disposeId}]");
                                _logger.Info($"[Operation:{disposeId}] Image cache cleared ({cacheSize} images)");
                            }
                        });

                    // Clear the button handlers collection
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "ButtonClickHandlers",
                        () =>
                        {
                            int handlerCount = _buttonClickHandlers.Count;
                            CollectionHelper.SafelyClearCollection(_buttonClickHandlers, _logger, "Button click handlers");
                            _logger.Info($"[Operation:{disposeId}] {handlerCount} button click handlers cleared");
                        });

                    // Clear cached configurations
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "CachedConfigurations",
                        () =>
                        {
                            if (_cachedCommands != null)
                            {
                                CollectionHelper.SafelyClearCollection(_cachedCommands, _logger, "Cached commands");
                                _cachedCommands = null;
                            }

                            if (_cachedMediaPlayers != null)
                            {
                                CollectionHelper.SafelyClearCollection(_cachedMediaPlayers, _logger, "Cached media players");
                                _cachedMediaPlayers = null;
                            }

                            _buttonsLoaded = false;
                            _logger.Info($"[Operation:{disposeId}] Cached configurations cleared");
                        });

                    // Clear the window reference
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "WindowReference",
                        () =>
                        {
                            _parentWindowWeakRef = null;
                            _logger.Info($"[Operation:{disposeId}] Window reference cleared");
                        });

                    // Clear timers
                    ErrorHelper.SafelyCleanupResource(
                        "LauncherButtonProvider", "OperationTimers",
                        () =>
                        {
                            CollectionHelper.SafelyClearCollection(_operationTimers, _logger, "Operation timers");
                            _logger.Info($"[Operation:{disposeId}] Operation timers cleared");
                        });
                },
                _logger);

            disposeTimer.Stop();
            _logger.Info($"[Operation:{disposeId}] Resource cleanup completed in {disposeTimer.ElapsedMilliseconds}ms");
        }

        protected override void ReleaseUnmanagedResources()
        {
            // Your existing implementation
            string disposeId = Guid.NewGuid().ToString().Substring(0, 8);
            System.Diagnostics.Stopwatch gcTimer = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.Info($"[Operation:{disposeId}] Releasing unmanaged resources");

                // Force GC to run to help release image resources that might be tied to native memory
                try
                {
                    _logger.Info($"[Operation:{disposeId}] Starting forced garbage collection");
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                    gcTimer.Stop();
                    _logger.Info($"[Operation:{disposeId}] Completed forced GC collection in {gcTimer.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[Operation:{disposeId}] Error during forced GC", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[Operation:{disposeId}] Error in ReleaseUnmanagedResources", ex);
            }
        }
    }
}