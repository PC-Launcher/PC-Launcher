using System;
using System.Net.Http;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public class WeatherApiService
    {
        private readonly ContextLogger _logger = Logger.GetLogger<WeatherApiService>();
        private readonly string _apiKey;
        private readonly string _locationType; // "zip", "city", or "coordinates"
        private readonly string _zipCode;
        private readonly string _cityName;
        private readonly string _countryCode;
        private readonly string _latitude;
        private readonly string _longitude;
        private readonly string _temperatureUnit;
        private readonly string _windSpeedUnit;

        // Properties to store weather data
        public string CityName { get; private set; }
        public string ShortForecast { get; private set; }
        public string Temperature { get; private set; }
        public string IconCode { get; private set; }
        public float? Humidity { get; private set; }
        public float? WindSpeed { get; private set; }
        public string WindSpeedDisplay { get; private set; }
        public float? FeelsLike { get; private set; }
        public DateTime? LastUpdateTime { get; private set; }

        // Constructor - Reads configuration
        public WeatherApiService(string configPath)
        {
            // Load configuration from config file
            var config = ConfigParser.LoadSection(configPath, ConfigKeys.Sections.Weather);

            // Extract config values
            _apiKey = config.ContainsKey(ConfigKeys.Weather.WeatherApiKey) ? config[ConfigKeys.Weather.WeatherApiKey] : "";
            
            // Get location type
            _locationType = config.ContainsKey(ConfigKeys.Weather.LocationType) ? 
                           config[ConfigKeys.Weather.LocationType].Trim().ToLower() : "zip";
            
            // Get location values based on type
            _zipCode = config.ContainsKey(ConfigKeys.Weather.ZipCode) ? config[ConfigKeys.Weather.ZipCode] : "";
            _cityName = config.ContainsKey(ConfigKeys.Weather.CityName) ? config[ConfigKeys.Weather.CityName] : "";
            _countryCode = config.ContainsKey(ConfigKeys.Weather.CountryCode) ? config[ConfigKeys.Weather.CountryCode] : "US";
            _latitude = config.ContainsKey(ConfigKeys.Weather.Latitude) ? config[ConfigKeys.Weather.Latitude] : "";
            _longitude = config.ContainsKey(ConfigKeys.Weather.Longitude) ? config[ConfigKeys.Weather.Longitude] : "";
            
            // Get temperature unit and normalize it by removing whitespace and converting to uppercase
            _temperatureUnit = config.ContainsKey(ConfigKeys.Weather.TemperatureUnit) ? 
                               config[ConfigKeys.Weather.TemperatureUnit].Trim().ToUpper() : "F";
                               
            // Get wind speed unit and normalize it
            _windSpeedUnit = config.ContainsKey(ConfigKeys.Weather.WindSpeedUnit) ? 
                            config[ConfigKeys.Weather.WindSpeedUnit].Trim().ToUpper() : "MPH";

            // Log initialization info
            _logger.Info("CONSTRUCTOR:");
            _logger.Info($"API Key: {(_apiKey.Length > 0 ? "Provided" : "Missing")}");
            _logger.Info($"Location Type: {_locationType}");
            _logger.Info($"Zip Code: {_zipCode}");
            _logger.Info($"City Name: {_cityName}");
            _logger.Info($"Country Code: {_countryCode}");
            _logger.Info($"Latitude: {_latitude}");
            _logger.Info($"Longitude: {_longitude}");
            _logger.Info($"Temperature Unit: {_temperatureUnit}");
            _logger.Info($"Wind Speed Unit: {_windSpeedUnit}");
            
            // Validate required settings
            ValidateSettings();
        }

        /// <summary>
        /// Validates settings to ensure we have enough data to make an API call
        /// </summary>
        private void ValidateSettings()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.Warning("Weather API key is missing");
                return;
            }

            switch (_locationType)
            {
                case "zip":
                    if (string.IsNullOrEmpty(_zipCode))
                    {
                        _logger.Warning("ZIP code is required when location type is 'zip'");
                    }
                    break;
                    
                case "city":
                    if (string.IsNullOrEmpty(_cityName))
                    {
                        _logger.Warning("City name is required when location type is 'city'");
                    }
                    break;
                    
                case "coordinates":
                    if (string.IsNullOrEmpty(_latitude) || string.IsNullOrEmpty(_longitude))
                    {
                        _logger.Warning("Both latitude and longitude are required when location type is 'coordinates'");
                    }
                    break;
                    
                default:
                    _logger.Warning($"Unknown location type: {_locationType}. Must be 'zip', 'city', or 'coordinates'");
                    break;
            }
        }

        /// <summary>
        /// Asynchronously fetches the current weather forecast.
        /// Accepts a CancellationToken to cancel the request if needed.
        /// </summary>
        /// <param name="token">Cancellation token for this operation.</param>
        /// <returns>True if successful; otherwise, false.</returns>
        public async Task<bool> FetchForecastAsync(CancellationToken token = default)
        {
            // RESET ALL PROPERTIES TO ENSURE CLEAN STATE
            CityName = null;
            ShortForecast = null;
            Temperature = null;
            IconCode = null;
            Humidity = null;
            WindSpeed = null;
            WindSpeedDisplay = null;
            FeelsLike = null;
            LastUpdateTime = null;

            // Validate API key is present
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.Warning("API key missing.");
                return false;
            }

            // Construct API URL based on location type
            string url;
            switch (_locationType)
            {
                case "zip":
                    if (string.IsNullOrEmpty(_zipCode))
                    {
                        _logger.Warning("ZIP code missing.");
                        return false;
                    }
                    url = $"https://api.weatherbit.io/v2.0/current?postal_code={_zipCode}&country={_countryCode}&key={_apiKey}";
                    break;

                case "city":
                    if (string.IsNullOrEmpty(_cityName))
                    {
                        _logger.Warning("City name missing.");
                        return false;
                    }
                    url = $"https://api.weatherbit.io/v2.0/current?city={_cityName}&country={_countryCode}&key={_apiKey}";
                    break;

                case "coordinates":
                    if (string.IsNullOrEmpty(_latitude) || string.IsNullOrEmpty(_longitude))
                    {
                        _logger.Warning("Latitude or longitude missing.");
                        return false;
                    }
                    url = $"https://api.weatherbit.io/v2.0/current?lat={_latitude}&lon={_longitude}&key={_apiKey}";
                    break;

                default:
                    _logger.Warning($"Unknown location type: {_locationType}");
                    return false;
            }

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Log the URL being called
                    _logger.Info($"Requesting URL: {url.Replace(_apiKey, "API_KEY_HIDDEN")}");

                    // Send API request with cancellation support
                    HttpResponseMessage response = await client.GetAsync(url, token);
                    token.ThrowIfCancellationRequested();

                    // Check response status
                    _logger.Info($"Response Status: {response.StatusCode}");
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Warning($"Weather API request failed: {response.StatusCode}");
                        return false;
                    }

                    // Read response content
                    string json = await response.Content.ReadAsStringAsync();
                    _logger.Debug($"Response received - length: {json.Length} bytes");

                    // Parse JSON response using the model classes
                    var weatherResponse = JsonConvert.DeserializeObject<WeatherApiResponse>(json);

                    if (weatherResponse?.Data == null || weatherResponse.Data.Length == 0)
                    {
                        _logger.Warning("No weather data received.");
                        return false;
                    }

                    // Get first weather data entry
                    var weather = weatherResponse.Data[0];

                    // Convert temperature based on user preference (API returns in Celsius)
                    float tempValue;
                    string tempUnitSymbol;
                    
                    if (_temperatureUnit == "C")
                    {
                        // Keep as Celsius
                        tempValue = weather.Temp;
                        tempUnitSymbol = "°C";
                    }
                    else
                    {
                        // Convert to Fahrenheit
                        tempValue = (weather.Temp * 9 / 5) + 32;
                        tempUnitSymbol = "°F";
                    }
                    
                    // Convert wind speed based on user preference (API returns in m/s)
                    float windValue;
                    string windUnitLabel;
                    
                    // API returns wind speed in m/s, convert to appropriate unit
                    if (_windSpeedUnit == "KPH")
                    {
                        // Convert m/s to km/h (multiply by 3.6)
                        windValue = weather.WindSpd * 3.6f;
                        windUnitLabel = "km/h";
                    }
                    else
                    {
                        // Convert m/s to mph (multiply by 2.237)
                        windValue = weather.WindSpd * 2.237f;
                        windUnitLabel = "mph";
                    }

                    // STORE PARSED DATA
                    CityName = weather.CityName ?? "Unknown";
                    ShortForecast = weather.Weather?.Description ?? "No description";
                    IconCode = weather.Weather?.Icon ?? "";
                    Temperature = $"{Math.Round(tempValue)}{tempUnitSymbol}";
                    WindSpeed = windValue;
                    WindSpeedDisplay = $"{Math.Round(windValue)} {windUnitLabel}";
                    Humidity = weather.Rh;
                    FeelsLike = weather.AppTemp;
                    LastUpdateTime = DateTime.Now;

                    // Log final parsed values
                    _logger.Info("FINAL WEATHERAPISERVICE PROPERTY VALUES:");
                    _logger.Info($"CityName: {CityName}");
                    _logger.Info($"ShortForecast: {ShortForecast}");
                    _logger.Info($"Temperature: {Temperature}");
                    _logger.Info($"IconCode: {IconCode}");
                    _logger.Info($"Humidity: {Humidity}");
                    _logger.Info($"WindSpeed: {WindSpeed}");
                    _logger.Info($"WindSpeedDisplay: {WindSpeedDisplay}");

                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("Weather API call canceled.");
                    throw;
                }
                catch (Exception ex)
                {
                    // Error handling
                    _logger.Error($"WEATHER API ERROR: {ex.Message}", ex);
                    return false;
                }
            }
        }

        // Model classes for JSON deserialization
        public class WeatherApiResponse
        {
            public WeatherData[] Data { get; set; }
        }

        public class WeatherData
        {
            [JsonProperty("city_name")]
            public string CityName { get; set; }

            [JsonProperty("temp")]
            public float Temp { get; set; }

            [JsonProperty("app_temp")]
            public float AppTemp { get; set; }

            [JsonProperty("rh")]
            public float Rh { get; set; }

            [JsonProperty("wind_spd")]
            public float WindSpd { get; set; }

            [JsonProperty("weather")]
            public WeatherInfo Weather { get; set; }
        }

        public class WeatherInfo
        {
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("icon")]
            public string Icon { get; set; }
        }
    }
}