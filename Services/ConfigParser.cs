using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public static class ConfigParser
    {
        private static readonly ContextLogger _logger = Logger.GetLogger(nameof(ConfigParser));

        /// <summary>
        /// Synchronously loads a section from the INI file into a dictionary.
        /// </summary>
        public static Dictionary<string, string> LoadSection(string filePath, string sectionName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _logger.Info($"Loading section '{sectionName}' from {filePath}");

            if (!File.Exists(filePath))
            {
                _logger.Warning($"Configuration file not found: {filePath}");
                return result;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                _logger.Info($"Successfully read {lines.Length} lines from {filePath}");

                bool inSection = false;
                int keyCount = 0;
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string currentSection = line.Substring(1, line.Length - 2).Trim();
                        bool wasInSection = inSection;
                        inSection = currentSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase);

                        if (wasInSection && !inSection)
                        {
                            // We've just exited our target section, log the keys found
                            _logger.Info($"Finished loading section '{sectionName}' - found {keyCount} keys");
                            break;
                        }

                        if (inSection)
                        {
                            _logger.Info($"Found section '{currentSection}' at line {Array.IndexOf(lines, rawLine) + 1}");
                        }
                        continue;
                    }

                    if (inSection)
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx).Trim();
                            string value = line.Substring(idx + 1).Trim().Trim('"');
                            
                            // Remove any inline comments (starting with semicolon)
                            int commentIdx = value.IndexOf(';');
                            if (commentIdx >= 0)
                            {
                                value = value.Substring(0, commentIdx).Trim();
                                _logger.Info($"Removed inline comment from value for key '{key}'");
                            }

                            // Use sanitized value for logging (hide sensitive data like API keys)
                            string logValue = SanitizeValueForLogging(key, value);
                            _logger.Info($"Loaded key '{key}' = '{logValue}'");

                            result[key] = value;
                            keyCount++;
                        }
                    }
                }

                if (keyCount == 0)
                {
                    _logger.Warning($"Section '{sectionName}' not found or empty in {filePath}");
                }
                else
                {
                    _logger.Info($"Successfully loaded {keyCount} keys from section '{sectionName}'");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading section '{sectionName}' from {filePath}", ex);
            }

            return result;
        }

        /// <summary>
        /// Asynchronously loads a section from the INI file into a dictionary.
        /// </summary>
        public static async Task<Dictionary<string, string>> LoadSectionAsync(string filePath, string sectionName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _logger.Info($"Asynchronously loading section '{sectionName}' from {filePath}");

            if (!File.Exists(filePath))
            {
                _logger.Warning($"Configuration file not found: {filePath}");
                return result;
            }

            try
            {
                // Read all lines asynchronously
                string[] lines = await Task.Run(() => File.ReadAllLines(filePath));
                _logger.Info($"Successfully read {lines.Length} lines from {filePath}");

                bool inSection = false;
                int keyCount = 0;
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string currentSection = line.Substring(1, line.Length - 2).Trim();
                        bool wasInSection = inSection;
                        inSection = currentSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase);

                        if (wasInSection && !inSection)
                        {
                            // We've just exited our target section, log the keys found
                            _logger.Info($"Finished loading section '{sectionName}' - found {keyCount} keys");
                            break;
                        }

                        if (inSection)
                        {
                            _logger.Info($"Found section '{currentSection}' at line {Array.IndexOf(lines, rawLine) + 1}");
                        }
                        continue;
                    }

                    if (inSection)
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx).Trim();
                            string value = line.Substring(idx + 1).Trim().Trim('"');
                            
                            // Remove any inline comments (starting with semicolon)
                            int commentIdx = value.IndexOf(';');
                            if (commentIdx >= 0)
                            {
                                value = value.Substring(0, commentIdx).Trim();
                                _logger.Info($"Removed inline comment from value for key '{key}'");
                            }

                            // Use sanitized value for logging (hide sensitive data like API keys)
                            string logValue = SanitizeValueForLogging(key, value);
                            _logger.Info($"Loaded key '{key}' = '{logValue}'");

                            result[key] = value;
                            keyCount++;
                        }
                    }
                }

                if (keyCount == 0)
                {
                    _logger.Warning($"Section '{sectionName}' not found or empty in {filePath}");
                }
                else
                {
                    _logger.Info($"Successfully loaded {keyCount} keys from section '{sectionName}'");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading section {sectionName} from {filePath}", ex);
                return result;
            }
        }

        /// <summary>
        /// Sanitizes sensitive configuration values for logging
        /// </summary>
        private static string SanitizeValueForLogging(string key, string value)
        {
            // List of keys that contain sensitive information
            if (key.Contains("ApiKey") || key.Contains("Password") || key.Contains("Secret") ||
                key.Contains("Token") || key.Contains("Credential"))
            {
                if (string.IsNullOrEmpty(value))
                    return "empty";

                // Show just the first few and last few characters
                if (value.Length > 8)
                    return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);

                // For very short secrets, just show a placeholder
                return "****";
            }

            // For non-sensitive values, return as is
            return value;
        }

        public static Dictionary<string, string> LoadCommands(string filePath)
        {
            _logger.Info($"Loading Commands section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.Commands);
        }

        public static Dictionary<string, string> LoadMediaPlayers(string filePath)
        {
            _logger.Info($"Loading MediaPlayers section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.MediaPlayers);
        }

        public static Dictionary<string, string> LoadBrowserConfig(string filePath)
        {
            _logger.Info($"Loading Browser section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.Browser);
        }

        public static Dictionary<string, string> LoadNavigationConfig(string filePath)
        {
            _logger.Info($"Loading Navigation section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.Navigation);
        }

        public static Dictionary<string, string> LoadGamepadConfig(string filePath)
        {
            _logger.Info($"Loading Gamepad section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.Gamepad);
        }

        // Add this method for the weather system
        public static Dictionary<string, string> LoadSoundConfig(string filePath)
        {
            _logger.Info($"Loading Sound section from {filePath}");
            return LoadSection(filePath, ConfigKeys.Sections.Sound);
        }

        /// <summary>
        /// Asynchronously loads the Commands section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadCommandsAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading Commands section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.Commands);
        }

        /// <summary>
        /// Asynchronously loads the MediaPlayers section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadMediaPlayersAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading MediaPlayers section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.MediaPlayers);
        }

        /// <summary>
        /// Asynchronously loads the Browser configuration section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadBrowserConfigAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading Browser section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.Browser);
        }

        /// <summary>
        /// Asynchronously loads the Navigation configuration section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadNavigationConfigAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading Navigation section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.Navigation);
        }

        /// <summary>
        /// Asynchronously loads the Gamepad configuration section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadGamepadConfigAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading Gamepad section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.Gamepad);
        }

        /// <summary>
        /// Asynchronously loads the Sound configuration section.
        /// </summary>
        public static Task<Dictionary<string, string>> LoadSoundConfigAsync(string filePath)
        {
            _logger.Info($"Asynchronously loading Sound section from {filePath}");
            return LoadSectionAsync(filePath, ConfigKeys.Sections.Sound);
        }

        /// <summary>
        /// Asynchronously loads multiple configuration sections at once.
        /// </summary>
        public static async Task<Dictionary<string, Dictionary<string, string>>> LoadMultipleSectionsAsync(
            string filePath, params string[] sectionNames)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _logger.Info($"Loading {sectionNames.Length} sections from {filePath}");

            if (!File.Exists(filePath) || sectionNames == null || sectionNames.Length == 0)
            {
                if (!File.Exists(filePath))
                    _logger.Warning($"Configuration file not found: {filePath}");
                if (sectionNames == null || sectionNames.Length == 0)
                    _logger.Warning("No section names provided for loading");

                return result;
            }

            try
            {
                // Read all lines once
                _logger.Info($"Reading all lines from {filePath}");
                string[] lines = await Task.Run(() => File.ReadAllLines(filePath));
                _logger.Info($"Successfully read {lines.Length} lines from {filePath}");

                foreach (string sectionName in sectionNames)
                {
                    _logger.Info($"Processing section '{sectionName}'");
                    var sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[sectionName] = sectionData;

                    bool inSection = false;
                    int keyCount = 0;

                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(";"))
                            continue;

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            string currentSection = line.Substring(1, line.Length - 2).Trim();
                            bool wasInSection = inSection;
                            inSection = currentSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase);

                            if (wasInSection && !inSection)
                            {
                                // We've just exited our target section
                                break;
                            }

                            if (inSection)
                            {
                                _logger.Info($"Found section '{currentSection}' at line {Array.IndexOf(lines, rawLine) + 1}");
                            }
                            continue;
                        }

                        if (inSection)
                        {
                            int idx = line.IndexOf('=');
                            if (idx > 0)
                            {
                                string key = line.Substring(0, idx).Trim();
                                string value = line.Substring(idx + 1).Trim().Trim('"');
                                
                                // Remove any inline comments (starting with semicolon)
                                int commentIdx = value.IndexOf(';');
                                if (commentIdx >= 0)
                                {
                                    value = value.Substring(0, commentIdx).Trim();
                                    _logger.Info($"Removed inline comment from value for key '{key}' in section '{sectionName}'");
                                }

                                // Use sanitized value for logging
                                string logValue = SanitizeValueForLogging(key, value);
                                _logger.Info($"Loaded key '{key}' = '{logValue}' for section '{sectionName}'");

                                sectionData[key] = value;
                                keyCount++;
                            }
                        }
                    }

                    if (keyCount == 0)
                    {
                        _logger.Warning($"Section '{sectionName}' not found or empty");
                    }
                    else
                    {
                        _logger.Info($"Successfully loaded {keyCount} keys from section '{sectionName}'");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading multiple sections from {filePath}", ex);
                return result;
            }
        }
    }
}