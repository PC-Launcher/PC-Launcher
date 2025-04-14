using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;

namespace PCStreamerLauncher
{
    public class WeatherManager : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<WeatherManager>();

        private readonly WeatherApiService _weatherService;
        private readonly TextBlock _cityTextBlock;
        private readonly TextBlock _conditionsTextBlock;
        private readonly TextBlock _temperatureTextBlock;
        private readonly TextBlock _humidityTextBlock;
        private readonly TextBlock _windTextBlock;
        private readonly Image _weatherImageControl;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private UIElement _currentWeatherIcon;
        private readonly Dictionary<string, string> _config;
        private Border _weatherBorder;
        private const string WeatherIconBaseUrl = "https://www.weatherbit.io/static/img/icons/";

        // Cancellation token source to cancel ongoing weather update operations.
        private CancellationTokenSource _weatherUpdateCts = new CancellationTokenSource();

        // Semaphore to ensure that only one weather update runs at a time.
        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);

        public WeatherManager(string configPath, TextBlock cityTextBlock, TextBlock conditionsTextBlock,
                              TextBlock temperatureTextBlock, TextBlock humidityTextBlock,
                              TextBlock windTextBlock, Image weatherImageControl)
        {
            _weatherService = new WeatherApiService(configPath);
            _cityTextBlock = cityTextBlock;
            _conditionsTextBlock = conditionsTextBlock;
            _temperatureTextBlock = temperatureTextBlock;
            _humidityTextBlock = humidityTextBlock;
            _windTextBlock = windTextBlock;
            _weatherImageControl = weatherImageControl;
            _config = ConfigParser.LoadSection(configPath, ConfigKeys.Sections.Weather);
            FindWeatherBorder();
            _logger.Info("Initialized");
        }

        private void FindWeatherBorder()
        {
            ThrowIfDisposed();
            
            ErrorHelper.ExecuteWithLogging(
                "Finding weather border",
                () =>
                {
                    if (_weatherImageControl != null)
                    {
                        FrameworkElement current = _weatherImageControl;
                        while (current != null)
                        {
                            var parent = VisualTreeHelper.GetParent(current) as FrameworkElement;
                            if (parent == null) break;
                            if (parent is Border border && border.Name == "WeatherBorder")
                            {
                                _weatherBorder = border;
                                _logger.Info("Found WeatherBorder control");
                                break;
                            }
                            current = parent;
                        }
                    }
                },
                _logger);
        }

        public async Task StartAsync(int intervalMinutes = 15)
        {
            ThrowIfDisposed();
            if (_config.TryGetValue(ConfigKeys.Weather.UpdateFrequency, out string configMinutes) &&
                int.TryParse(configMinutes, out int parsedMinutes))
            {
                intervalMinutes = Math.Max(5, parsedMinutes);
            }
            _logger.Info($"Starting weather updates with {intervalMinutes} minute interval...");
            await FetchAndUpdateUI();
            if (_timer.IsEnabled)
                _timer.Stop();
            _timer.Interval = TimeSpan.FromMinutes(intervalMinutes);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            _logger.Info($"Scheduled updates every {intervalMinutes} minutes");
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Capture the timer to stop it during the operation to prevent overlapping executions
            var timer = sender as DispatcherTimer;
            if (timer != null) timer.Stop();

            // Fire and forget with explicit error handling and timer restart
            Task.Run(async () => {
                await ErrorHelper.ExecuteAsyncWithLogging(
                    "Timer-triggered weather update",
                    async () => await FetchAndUpdateUI(),
                    _logger);
                    
                // Restart the timer when done if not disposed
                if (!IsDisposed && timer != null)
                {
                    await UIOperationHelper.InvokeOnUIThreadAsync(
                        () => { if (!IsDisposed && timer != null) timer.Start(); },
                        "Restarting weather update timer",
                        _logger);
                }
            });
        }

        private async Task FetchAndUpdateUI()
        {
            ThrowIfDisposed();

            // Ensure only one update runs at a time
            if (!await _updateLock.WaitAsync(0))
            {
                _logger.Info("Update already in progress, skipping this update");
                return;
            }

            await ErrorHelper.ExecuteAsyncWithLogging(
                "Fetching and updating weather data",
                async () =>
                {
                    // Cancel any ongoing update
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "weather update cancellation token",
                        () =>
                        {
                            _weatherUpdateCts.Cancel();
                            _weatherUpdateCts.Dispose();
                            _weatherUpdateCts = new CancellationTokenSource();
                        });

                    CancellationToken token = _weatherUpdateCts.Token;

                    _logger.Info("Fetching updated weather data...");

                    // Check for cancellation or disposal
                    if (token.IsCancellationRequested || IsDisposed)
                    {
                        _logger.Info("Update cancelled before fetch");
                        return;
                    }

                    bool success = await _weatherService.FetchForecastAsync(token);

                    // Check for cancellation or disposal again after the async operation
                    if (token.IsCancellationRequested || IsDisposed)
                    {
                        _logger.Info("Update cancelled after fetch");
                        return;
                    }

                    if (success)
                    {
                        // Apply the UI updates on the UI thread
                        await UIOperationHelper.InvokeOnUIThreadAsync(
                            () =>
                            {
                                // Check for cancellation or disposal before UI updates
                                if (token.IsCancellationRequested || IsDisposed)
                                    return;

                                UpdateWeatherText();
                                UpdateWeatherIcon();
                                UpdateBackgroundColor();
                            },
                            "Updating weather UI elements",
                            _logger);
                    }
                    else
                    {
                        _logger.Warning("Failed to fetch weather data");

                        // Check for cancellation or disposal
                        if (token.IsCancellationRequested || IsDisposed)
                            return;

                        await ClearWeatherDisplayAsync();

                        await UIOperationHelper.InvokeOnUIThreadAsync(
                            () =>
                            {
                                if (!token.IsCancellationRequested && !IsDisposed)
                                {
                                    _cityTextBlock.Text = "Weather Unavailable";
                                    _conditionsTextBlock.Text = "";
                                    _temperatureTextBlock.Text = "";
                                    _humidityTextBlock.Text = "";
                                    _windTextBlock.Text = "";
                                }
                            },
                            "Setting weather unavailable message",
                            _logger);
                    }
                },
                _logger);

            try
            {
                _updateLock.Release();
            }
            catch (Exception ex)
            {
                _logger.Error("Error releasing update lock", ex);
            }
        }

        private void UpdateWeatherText()
        {
            ThrowIfDisposed();
            
            ErrorHelper.ExecuteWithLogging(
                "Updating weather text",
                () =>
                {
                    _logger.Info("RECEIVED DATA:");
                    _logger.Info($"City from Service: {_weatherService.CityName}");
                    _logger.Info($"Forecast from Service: {_weatherService.ShortForecast}");
                    _logger.Info($"Temperature from Service: {_weatherService.Temperature}");
                    _logger.Info($"Humidity from Service: {_weatherService.Humidity}");
                    _logger.Info($"Wind Speed from Service: {_weatherService.WindSpeed}");

                    if (string.IsNullOrEmpty(_weatherService.CityName) ||
                        string.IsNullOrEmpty(_weatherService.ShortForecast) ||
                        string.IsNullOrEmpty(_weatherService.Temperature))
                    {
                        // Already on UI thread, no need for UIOperationHelper
                        _cityTextBlock.Text = "Weather Unavailable";
                        _conditionsTextBlock.Text = "";
                        _temperatureTextBlock.Text = "";
                        _humidityTextBlock.Text = "";
                        _windTextBlock.Text = "";
                        _logger.Warning("Insufficient data to display");
                        return;
                    }

                    // Format city name with proper capitalization
                    string cityName = FormatCityName(_weatherService.CityName);
                    string description = char.ToUpper(_weatherService.ShortForecast[0]) +
                                         _weatherService.ShortForecast.Substring(1).ToLower();
                    string humidityText = _weatherService.Humidity.HasValue
                        ? $"Humidity: {Math.Round(_weatherService.Humidity.Value)}%"
                        : "Humidity: N/A";
                    string windSpeedText = !string.IsNullOrEmpty(_weatherService.WindSpeedDisplay)
                        ? $"Wind: {_weatherService.WindSpeedDisplay}"
                        : "Wind: N/A";

                    // Already on UI thread from caller, no need for additional dispatcher
                    _cityTextBlock.Text = cityName;
                    _conditionsTextBlock.Text = description;
                    _temperatureTextBlock.Text = _weatherService.Temperature;
                    _humidityTextBlock.Text = humidityText;
                    _windTextBlock.Text = windSpeedText;

                    _logger.Info("Weather text updated successfully");
                },
                _logger);
        }

        private void UpdateWeatherIcon()
        {
            ThrowIfDisposed();
            
            ErrorHelper.ExecuteWithLogging(
                "Updating weather icon",
                () =>
                {
                    string iconCode = _weatherService.IconCode;
                    if (string.IsNullOrEmpty(iconCode))
                    {
                        CleanupPreviousWeatherIcon();
                        return;
                    }
                    if (_config.TryGetValue(ConfigKeys.Weather.ShowConditionIcon, out string showIcon) &&
                        showIcon.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        CleanupPreviousWeatherIcon();
                        return;
                    }

                    // Already on UI thread from caller
                    CleanupPreviousWeatherIcon();
                    if (_weatherImageControl.Parent is Grid parentGrid)
                    {
                        _logger.Info("Weather image parent is a Grid - using updated layout");
                        int column = Grid.GetColumn(_weatherImageControl);
                        _weatherImageControl.Visibility = Visibility.Collapsed;
                        var weatherIcon = SimpleWeatherIndicator.CreateWeatherIcon(iconCode, 120, 100);
                        Grid.SetColumn(weatherIcon, column);
                        if (weatherIcon is FrameworkElement fe)
                        {
                            fe.HorizontalAlignment = HorizontalAlignment.Center;
                            fe.VerticalAlignment = VerticalAlignment.Center;
                            fe.Margin = new Thickness(0);
                        }
                        parentGrid.Children.Add(weatherIcon);
                        _currentWeatherIcon = weatherIcon;
                        _logger.Info("Weather icon added to UI successfully");
                    }
                    else if (_weatherImageControl.Parent is StackPanel panel)
                    {
                        _logger.Info("Weather image parent is a StackPanel - using legacy layout");
                        int index = panel.Children.IndexOf(_weatherImageControl);
                        _logger.Info($"Weather image found at index {index} in panel");
                        if (index >= 0)
                        {
                            _weatherImageControl.Visibility = Visibility.Collapsed;
                            var weatherIcon = SimpleWeatherIndicator.CreateWeatherIcon(iconCode, 80, 80);
                            if (weatherIcon is FrameworkElement fe)
                                fe.Margin = new Thickness(0, 0, 10, 0);
                            panel.Children.Insert(index, weatherIcon);
                            _currentWeatherIcon = weatherIcon;
                            _logger.Info("Weather icon added to UI successfully");
                        }
                        else
                        {
                            _logger.Warning("Weather image not found in panel children");
                        }
                    }
                    else
                    {
                        _logger.Warning($"Weather image parent is neither Grid nor StackPanel: {_weatherImageControl.Parent?.GetType().Name ?? "null"}");
                    }
                },
                _logger);
            
            // If an error occurs, try to fall back to static icon
            if (_currentWeatherIcon == null)
            {
                FallbackToStaticIcon();
            }
        }

        private void CleanupPreviousWeatherIcon()
        {
            ErrorHelper.ExecuteWithLogging(
                "Cleaning up previous weather icon",
                () =>
                {
                    if (_currentWeatherIcon != null)
                    {
                        var iconParent = VisualTreeHelper.GetParent(_currentWeatherIcon) as Panel;
                        _logger.Info($"Previous weather icon has parent: {iconParent != null}");
                        if (iconParent != null && iconParent.Children.Contains(_currentWeatherIcon))
                        {
                            iconParent.Children.Remove(_currentWeatherIcon);
                            _logger.Info("Removed previous weather icon from panel");
                        }
                        if (_currentWeatherIcon is FrameworkElement element)
                        {
                            // Check if the RenderTransform is a TranslateTransform and is not frozen.
                            if (element.RenderTransform is TranslateTransform translateTransform && !translateTransform.IsFrozen)
                            {
                                translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                            }
                            // Otherwise, simply clear the RenderTransform.
                            element.RenderTransform = null;
                        }
                        _currentWeatherIcon = null;
                    }
                },
                _logger);
        }

        private void UpdateBackgroundColor()
        {
            ThrowIfDisposed();
            
            ErrorHelper.ExecuteWithLogging(
                "Updating background color",
                () =>
                {
                    if (_weatherBorder == null)
                    {
                        FindWeatherBorder();
                        if (_weatherBorder == null)
                        {
                            _logger.Warning("Cannot update background color, border not found");
                            return;
                        }
                    }
                    string iconCode = _weatherService.IconCode;
                    if (string.IsNullOrEmpty(iconCode))
                    {
                        SetBackgroundColor("#22000000");
                        return;
                    }
                    string colorHex = GetWeatherColor(iconCode);

                    // Already on UI thread from caller
                    SetBackgroundColor(colorHex);

                    _logger.Info($"Updated background color to {colorHex} for {iconCode}");
                },
                _logger);
        }

        private void SetBackgroundColor(string colorHex)
        {
            if (_weatherBorder == null)
                return;
                
            ErrorHelper.ExecuteWithLogging(
                "Setting background color",
                () =>
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorHex);
                    SolidColorBrush brush = new SolidColorBrush(color);
                    ColorAnimation animation = new ColorAnimation
                    {
                        To = color,
                        Duration = TimeSpan.FromSeconds(1)
                    };
                    if (_weatherBorder.Background is SolidColorBrush currentBrush)
                    {
                        currentBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                    }
                    else
                    {
                        _weatherBorder.Background = brush;
                    }
                    _logger.Info($"Set background color to {colorHex}");
                },
                _logger);
        }

        private string GetWeatherColor(string iconCode)
        {
            string codePrefix = iconCode.Length >= 2 ? iconCode.Substring(0, 2) : "";
            bool isNight = iconCode.EndsWith("n");
            switch (codePrefix)
            {
                case "c0":
                    if (iconCode.StartsWith("c01"))
                        return isNight ? "#59000066" : "#59007ACC";
                    else if (iconCode.StartsWith("c02"))
                        return isNight ? "#59333366" : "#595599CC";
                    else if (iconCode.StartsWith("c03"))
                        return "#59667788";
                    else if (iconCode.StartsWith("c04"))
                        return "#59555555";
                    else
                        return isNight ? "#59000066" : "#59007ACC";
                case "r0":
                case "d0":
                    return "#59004080";
                case "t0":
                    return "#59220033";
                case "s0":
                    return "#59AADDFF";
                case "a0":
                    if (iconCode.StartsWith("a07") || iconCode.StartsWith("a08"))
                        return "#59BF8970";
                    else
                        return "#59808080";
                default:
                    return "#22000000";
            }
        }

        private void FallbackToStaticIcon()
        {
            ThrowIfDisposed();
            
            ErrorHelper.ExecuteWithLogging(
                "Falling back to static weather icon",
                () =>
                {
                    if (_weatherImageControl != null)
                    {
                        // Already on UI thread from caller
                        _weatherImageControl.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_weatherService.IconCode))
                        {
                            string iconUrl = $"{WeatherIconBaseUrl}{_weatherService.IconCode}.png";
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(iconUrl);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            _weatherImageControl.Source = bitmap;
                            _logger.Info($"Fell back to static icon from URL: {iconUrl}");
                        }
                    }
                },
                _logger);
        }

        private async Task ClearWeatherDisplayAsync()
        {
            ThrowIfDisposed();
            
            await UIOperationHelper.InvokeOnUIThreadAsync(
                () =>
                {
                    if (_weatherImageControl != null)
                    {
                        _weatherImageControl.Source = null;
                        _weatherImageControl.Visibility = Visibility.Visible;
                    }
                    CleanupPreviousWeatherIcon();
                    if (_weatherBorder != null)
                        SetBackgroundColor("#22000000");
                },
                "Clearing weather display",
                _logger);
        }

        public void Stop()
        {
            if (IsDisposed)
                return;
                
            ErrorHelper.ExecuteWithLogging(
                "Stopping weather service",
                () =>
                {
                    if (_timer != null && _timer.IsEnabled)
                        _timer.Stop();

                    // Fire and forget task for UI operations
                    Task.Run(async () => 
                    {
                        await ErrorHelper.ExecuteAsyncWithLogging(
                            "Clearing weather display during stop",
                            async () => await ClearWeatherDisplayAsync(),
                            _logger);
                    });

                    _logger.Info("Weather service stopped");
                },
                _logger);
        }

        /// <summary>
        /// Formats a city name with proper capitalization for each word
        /// </summary>
        private string FormatCityName(string cityName)
        {
            if (string.IsNullOrEmpty(cityName))
                return cityName;
            
            // Split the city name into words and capitalize each word
            string[] words = cityName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    // Handle special cases like "McDowell" or "O'Hare" where there's an internal capital
                    if (words[i].Contains("'") || words[i].Contains("-"))
                    {
                        // For apostrophes and hyphens, split and capitalize each part
                        string[] parts = words[i].Split(new[] { '\'', '-' });
                        for (int j = 0; j < parts.Length; j++)
                        {
                            if (parts[j].Length > 0)
                                parts[j] = char.ToUpper(parts[j][0]) + (parts[j].Length > 1 ? parts[j].Substring(1).ToLower() : "");
                        }
                        words[i] = string.Join(words[i].Contains("'") ? "'" : "-", parts);
                    }
                    else
                    {
                        // Regular word - capitalize first letter, lowercase rest
                        words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
                    }
                }
            }
            
            return string.Join(" ", words);
        }

        protected override void ReleaseManagedResources()
        {
            ErrorHelper.ExecuteWithLogging(
                "Releasing managed resources in WeatherManager",
                () =>
                {
                    _logger.Info("Beginning resource cleanup");

                    // Stop the weather update process first
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "weather service",
                        () => Stop());

                    // Clean up UI resources on the UI thread
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "UI resources",
                        () =>
                        {
                            UIOperationHelper.TryInvokeOnUIThread(
                                () =>
                                {
                                    // Clean up previous weather icon
                                    CleanupPreviousWeatherIcon();

                                    // Stop any running animations on the weather border
                                    if (_weatherBorder?.Background is SolidColorBrush backgroundBrush)
                                    {
                                        ResourceCleanup.StopBrushAnimations(backgroundBrush, "WeatherManager.WeatherBorder.Background");
                                    }

                                    // Clear image source
                                    if (_weatherImageControl != null)
                                    {
                                        ResourceCleanup.CleanupImage(_weatherImageControl, "WeatherManager.WeatherImageControl");
                                    }

                                    _logger.Info("UI resources cleaned up");
                                },
                                "Cleaning up weather UI resources", 
                                _logger);
                        });

                    // Clean up timer resources
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "timer resources",
                        () =>
                        {
                            if (_timer != null)
                            {
                                ResourceCleanup.StopAndDisposeTimer(_timer, Timer_Tick, "WeatherManager._timer");
                                _logger.Info("Timer stopped and event handler removed");
                            }
                        });

                    // Clean up thread synchronization resources
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "thread synchronization resources",
                        () =>
                        {
                            if (_updateLock != null)
                            {
                                ResourceCleanup.DisposeSemaphore(_updateLock, "WeatherManager._updateLock");
                                _logger.Info("Update lock disposed");
                            }
                        });

                    // Cancel and dispose any pending operations
                    ErrorHelper.SafelyCleanupResource(
                        nameof(WeatherManager),
                        "cancellation token source",
                        () =>
                        {
                            if (_weatherUpdateCts != null)
                            {
                                ResourceCleanup.DisposeCancellationTokenSource(_weatherUpdateCts, "WeatherManager._weatherUpdateCts");
                                _logger.Info("Cancellation token source canceled and disposed");
                            }
                        });
                },
                _logger);
        }
    }
}