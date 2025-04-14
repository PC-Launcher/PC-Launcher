using PCStreamerLauncher.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using PC_Launcher.Services.WeatherEngine;
using System.Windows.Media.Media3D;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Factory class that creates weather icons based on weather codes
    /// </summary>
    public class WeatherIconFactory
    {
        private readonly ContextLogger _logger = PCStreamerLauncher.Logging.Logger.GetLogger<WeatherIconFactory>();

        // Y-offset for cloud positioning
        // Default value when sun/moon is present
        private const double CLOUD_OFFSET_WITH_SUN_MOON = -15;
        // Value when sun/moon is not present (higher position)
        private const double CLOUD_OFFSET_WITHOUT_SUN_MOON = -25;

        // Y-offset for precipitation starting position
        // These values should be adjusted to visually align with the clouds
        private const double PRECIP_OFFSET_WITH_SUN_MOON = 20;
        private const double PRECIP_OFFSET_WITHOUT_SUN_MOON = 15;

        /// <summary>
        /// Creates a weather icon for the specified weather code
        /// </summary>
        /// <param name="weatherCode">Weather code (from weather API)</param>
        /// <param name="width">Desired width of the icon</param>
        /// <param name="height">Desired height of the icon</param>
        /// <returns>A UIElement representing the weather icon</returns>
        public UIElement CreateWeatherIcon(string weatherCode, int width = 80, int height = 80)
        {
            try
            {
                // Create the composer that will handle the layering
                var composer = new WeatherIconComposer(width, height);

                // Determine if it's day or night
                bool isDay = !weatherCode?.EndsWith("n") ?? true;

                // Determine cloud Y position based on weather code
                // When sun/moon is present (clear or partly cloudy), use default offset
                // For other conditions (cloudy, overcast, etc.), position clouds higher
                double offsetY = HasSunOrMoon(weatherCode) ? CLOUD_OFFSET_WITH_SUN_MOON : CLOUD_OFFSET_WITHOUT_SUN_MOON;

                // Similarly determine precipitation position to match cloud height
                double precipOffsetY = HasSunOrMoon(weatherCode) ? PRECIP_OFFSET_WITH_SUN_MOON : PRECIP_OFFSET_WITHOUT_SUN_MOON;

                _logger.Info($"Creating weather icon for code '{weatherCode}' at size {width}x{height} (isDay: {isDay})");

                // First, add the sky background to all icons with appropriate color for the conditions
                composer.AddElement(new SkyElement(isDay, weatherCode));

                // Next, add weather-specific elements based on the code
                switch (weatherCode?.ToLowerInvariant())
                {
                    // Clear day - simple sun
                    case "c01d":
                        _logger.Debug("Creating clear day icon");
                        composer.AddElement(new SunElement());
                        break;

                    // Clear night - moon
                    case "c01n":
                        _logger.Debug("Creating clear night icon");
                        composer.AddElement(new SkyElement(isDay, weatherCode));

                        // Smaller moon with adjusted scale for better balance
                        composer.AddElement(new MoonElement(offsetX: 0, offsetY: 0, scale: 0.8, weatherCode: weatherCode));

                        // Star placement away from center and moon
                        composer.AddElement(new StarElement(positionX: 0.1, positionY: 0.1, scale: 0.015));
                        composer.AddElement(new StarElement(positionX: 0.75, positionY: 0.2, scale: 0.018));
                        composer.AddElement(new StarElement(positionX: 0.05, positionY: 0.80, scale: 0.012));
                        composer.AddElement(new StarElement(positionX: 0.55, positionY: 0.10, scale: 0.010));
                        composer.AddElement(new StarElement(positionX: 0.2, positionY: 0.35, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.85, positionY: 0.13, scale: 0.015));
                        composer.AddElement(new StarElement(positionX: 0.90, positionY: 0.45, scale: 0.018));
                        break;



                    // Partly cloudy day - use EnhancedCloudGroupElement for consistency
                    case "c02d":
                        _logger.Debug("Creating partly cloudy day icon");
                        composer.AddElement(new SunElement(scale: 0.75));
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        break;

                    // Partly cloudy night - use EnhancedCloudGroupElement for consistency
                    case "c02n":
                        _logger.Debug("Creating partly cloudy night icon");
              //          positionY ↑
              // 0.0 ┌────────────────────────────────┐
              //     │  *(0.10, 0.10) * (0.90, 0.10)
              //     │     Top - left             Top - right
              //     │                                
              //     │                                
              //     │                                
              //     │                                
              //     │                                
              //     │       CENTER(0.50, 0.50)
              //     │                                
              //     │                                
              //     │                                
              //     │                                
              //     │  *(0.10, 0.90) * (0.90, 0.90)
              // 1.0 └────────────────────────────────┘
              //       0.0           →          1.0
              //             positionX →

                        composer.AddElement(new StarElement(positionX: 0.75, positionY: 0.2, scale: 0.018));
                        composer.AddElement(new StarElement(positionX: 0.05, positionY: 0.15, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.55, positionY: 0.10, scale: 0.010));
                        composer.AddElement(new StarElement(positionX: 0.2, positionY: 0.35, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.85, positionY: 0.13, scale: 0.015));
                        composer.AddElement(new StarElement(positionX: 0.90, positionY: 0.45, scale: 0.018));
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        break;

                    // Cloudy day (c03d)
                    case "c03d":
                        _logger.Debug("Creating cloudy day icon");
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        break;

                    // Cloudy night (c03n)
                    case "c03n":
                        _logger.Debug("Creating cloudy night icon");
                        composer.AddElement(new StarElement(positionX: 0.75, positionY: 0.2, scale: 0.018));
                        composer.AddElement(new StarElement(positionX: 0.05, positionY: 0.15, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.55, positionY: 0.10, scale: 0.010));
                        composer.AddElement(new StarElement(positionX: 0.2, positionY: 0.35, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.85, positionY: 0.13, scale: 0.015));
                        composer.AddElement(new StarElement(positionX: 0.90, positionY: 0.45, scale: 0.018));
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        break;

                    // Overcast day (c04d) - more clouds
                    case "c04d":
                        _logger.Debug("Creating overcast day icon");
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        break;

                    // Overcast night (c04n) - more clouds
                    case "c04n":
                        _logger.Debug("Creating overcast night icon");
                        composer.AddElement(new StarElement(positionX: 0.75, positionY: 0.2, scale: 0.014));
                        composer.AddElement(new StarElement(positionX: 0.05, positionY: 0.15, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.55, positionY: 0.10, scale: 0.010));
                        composer.AddElement(new StarElement(positionX: 0.2, positionY: 0.35, scale: 0.013));
                        composer.AddElement(new StarElement(positionX: 0.85, positionY: 0.13, scale: 0.015));
                        composer.AddElement(new StarElement(positionX: 0.90, positionY: 0.45, scale: 0.013));
                        composer.AddElement(new EnhancedCloudGroupElement(weatherCode, offsetY));
                        

                        break;

                    // Rain (light) - day and night
                    case "r01d":
                    case "r02d":
                    case "r04d":
                        _logger.Debug("Creating light rain day icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Light, offsetY: precipOffsetY));
                        break;

                    case "r01n":
                    case "r02n":
                    case "r04n":
                        _logger.Debug("Creating light rain night icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Light, offsetY: precipOffsetY));
                        break;

                    // Heavy rain
                    case "r03d":
                    case "r05d":
                    case "r06d":
                        _logger.Debug("Creating heavy rain day icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    case "r03n":
                    case "r05n":
                    case "r06n":
                    case "d01d":
                    case "d01n":
                        _logger.Debug("Creating heavy rain night icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    // Thunderstorm
                    case "t01d":
                    case "t02d":
                    case "t03d":
                    case "t04d":
                        _logger.Debug("Creating thunderstorm day icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Medium, offsetY: precipOffsetY));
                        composer.AddElement(new LightningElement()); // Add as an element, not UIElement
                        break;

                    case "t01n":
                    case "t02n":
                    case "t03n":
                    case "t04n":
                        _logger.Debug("Creating thunderstorm night icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Medium, offsetY: precipOffsetY));
                        composer.AddElement(new LightningElement()); // Add as an element, not UIElement
                        break;

                    // Snow (light)
                    case "s01d":
                    case "s02d":
                    case "s04d":
                        _logger.Debug("Creating light snow day icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Light, offsetY: precipOffsetY));
                        break;

                    case "s01n":
                    case "s02n":
                    case "s04n":
                        _logger.Debug("Creating light snow night icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Light, offsetY: precipOffsetY));
                        break;

                    // Snow (heavy)
                    case "s03d":
                    case "s05d":
                    case "s06d":
                        _logger.Debug("Creating heavy snow day icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    case "s03n":
                    case "s05n":
                    case "s06n":
                        _logger.Debug("Creating heavy snow night icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    // Fog, mist, etc.
                    case "a01d":
                    case "a02d":
                    case "a03d":
                    case "a04d":
                    case "a05d":
                    case "a06d":
                        _logger.Debug("Creating fog/mist day icon");
                        composer.AddElement(new FogElement());
                        break;

                    case "a01n":
                    case "a02n":
                    case "a03n":
                    case "a04n":
                    case "a05n":
                    case "a06n":
                        _logger.Debug("Creating fog/mist night icon");
                        composer.AddElement(new FogElement());
                        break;

                    // Dust
                    case "a07d":
                    case "a08d":
                        _logger.Debug("Creating dust day icon");
                        composer.AddElement(new DustElement());
                        break;

                    case "a07n":
                    case "a08n":
                        _logger.Debug("Creating dust night icon");
                        composer.AddElement(new DustElement());
                        break;


                    // Mixed precipitation (rain + snow)
                    case "rs01d":
                    case "rs02d":
                        _logger.Debug("Creating mixed rain and snow (light/moderate day) icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Light, offsetY: precipOffsetY));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Light, offsetY: precipOffsetY));
                        break;

                    case "rs03d":
                        _logger.Debug("Creating mixed rain and snow (heavy day) icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Heavy, offsetY: precipOffsetY));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    case "rs01n":
                    case "rs02n":
                        _logger.Debug("Creating mixed rain and snow (light/moderate night) icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Light, offsetY: precipOffsetY));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Light, offsetY: precipOffsetY));
                        break;

                    case "rs03n":
                        _logger.Debug("Creating mixed rain and snow (heavy night) icon");
                        composer.AddElement(new EnhancedCloudElement(scale: 1.0, offsetY: offsetY, isDarkCloud: true));
                        composer.AddElement(new RainElement2(intensity: RainIntensity.Heavy, offsetY: precipOffsetY));
                        composer.AddElement(new SnowElement(intensity: SnowIntensity.Heavy, offsetY: precipOffsetY));
                        break;

                    // Default/unknown
                    default:
                        _logger.Warning($"Unknown weather code '{weatherCode}', creating default icon");
                        composer.AddElement(new DefaultWeatherElement());
                        break;
                }

                // Let the composer build the final weather icon
                return composer.Compose();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error creating weather indicator for code '{weatherCode}'", ex);
                return new Border
                {
                    Width = width,
                    Height = height,
                    Background = new SolidColorBrush(Colors.Gray)
                };
            }
        }

        /// <summary>
        /// Determines if the weather condition includes a sun or moon
        /// </summary>
        /// <param name="weatherCode">The weather code</param>
        /// <returns>True if sun or moon is present, false otherwise</returns>
        private bool HasSunOrMoon(string weatherCode)
        {
            // Weather codes where sun or moon is visible:
            // c01d, c01n - Clear day/night (sun/moon visible)
            // c02d, c02n - Partly cloudy (sun/moon still visible)

            if (string.IsNullOrEmpty(weatherCode))
                return false;

            // Check for clear or partly cloudy conditions
            return weatherCode.StartsWith("c01") || weatherCode.StartsWith("c02");
        }
    }

    /// <summary>
    /// Enumeration of rain intensity levels
    /// </summary>

}
