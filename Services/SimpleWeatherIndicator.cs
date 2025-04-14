using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PC_Launcher.Services.WeatherEngine;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Legacy compatibility wrapper for creating weather icons.
    /// Internally delegates to WeatherIconFactory for full visual rendering.
    /// </summary>
    public static class SimpleWeatherIndicator
    {
        private static readonly WeatherIconFactory _factory = new WeatherIconFactory();

        /// <summary>
        /// Creates a weather icon using the modular WeatherEngine system.
        /// </summary>
        /// <param name="weatherCode">Weather condition code (e.g., c01d, r01n)</param>
        /// <param name="width">Icon width in pixels</param>
        /// <param name="height">Icon height in pixels</param>
        /// <returns>A fully composed weather visual</returns>
        public static UIElement CreateWeatherIcon(string weatherCode, int width = 80, int height = 80)
        {
            Logger.LogInfo($"[SimpleWeatherIndicator] Delegating to WeatherIconFactory for code {weatherCode}");
            return _factory.CreateWeatherIcon(weatherCode, width, height);
        }
    }
}