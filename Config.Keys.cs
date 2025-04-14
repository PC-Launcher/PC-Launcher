using System;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Provides centralized access to all configuration key strings used throughout the application.
    /// </summary>
    public static class ConfigKeys
    {
        /// <summary>
        /// Configuration file sections
        /// </summary>
        public static class Sections
        {
            public const string Weather = "Weather";
            public const string Commands = "Commands";
            public const string MediaPlayers = "MediaPlayers";
            public const string Browser = "Browser";
            public const string Navigation = "Navigation";
            public const string Gamepad = "Gamepad";
            public const string Sound = "Sound";
        }

        /// <summary>
        /// Weather section configuration keys
        /// </summary>
        public static class Weather
        {
            public const string ZipCode = "ZipCode";
            public const string CityName = "CityName"; 
            public const string CountryCode = "CountryCode";
            public const string Latitude = "Latitude";
            public const string Longitude = "Longitude";
            public const string LocationType = "LocationType"; // Can be "zip", "city", or "coordinates"
            public const string WeatherApiKey = "WeatherApiKey";
            public const string TemperatureUnit = "TemperatureUnit";
            public const string WindSpeedUnit = "WindSpeedUnit";
            public const string ShowConditionIcon = "ShowConditionIcon";
            public const string UpdateFrequency = "UpdateFrequency";
        }

        /// <summary>
        /// Browser section configuration keys
        /// </summary>
        public static class Browser
        {
            public const string DefaultBrowserExecutable = "DefaultBrowserExecutable";
            public const string DefaultBrowserProcess = "DefaultBrowserProcess";
            public const string AlternateBrowserExecutable = "AlternateBrowserExecutable";
            public const string AlternateBrowserProcess = "AlternateBrowserProcess";
            public const string HideDelay = "HideDelay";
        }

        /// <summary>
        /// Navigation section configuration keys
        /// </summary>
        public static class Navigation
        {
            public const string NavigationMode = "NavigationMode";
        }

        /// <summary>
        /// Gamepad section configuration keys
        /// </summary>
        public static class Gamepad
        {
            public const string Enabled = "Enabled";
            public const string Sensitivity = "Sensitivity";

            // Button mapping keys
            public const string ActionButton = "ActionButton";
            public const string TerminateButton = "TerminateButton";
            public const string UpButton = "UpButton";
            public const string DownButton = "DownButton";
            public const string LeftButton = "LeftButton";
            public const string RightButton = "RightButton";
        }

        /// <summary>
        /// Sound section configuration keys
        /// </summary>
        public static class Sound
        {
            public const string Enabled = "Enabled";
        }

        /// <summary>
        /// File and directory constants
        /// </summary>
        public static class Files
        {
            public const string ConfigFile = "config.ini";
            public const string ImagesDir = "Images";
            public const string SoundsDir = "Sounds";
        }

        /// <summary>
        /// Navigation modes
        /// </summary>
        public static class NavigationModes
        {
            public const string Keyboard = "Keyboard";
            public const string Gamepad = "Gamepad";
        }
    }
}