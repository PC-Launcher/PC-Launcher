using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Enumeration of rain intensity levels
    /// </summary>
    public enum RainIntensity
    {
        Light,
        Medium,
        Heavy
    }

    /// <summary>
    /// Enumeration of snow intensity levels
    /// </summary>
    public enum SnowIntensity
    {
        Light,
        Medium,
        Heavy
    }

    /// <summary>
    /// Enumeration of cloud types
    /// </summary>
    public enum CloudType
    {
        Normal,
        Dark,
        Storm
    }

    /// <summary>
    /// Enumeration of weather condition categories
    /// </summary>
    public enum WeatherCondition
    {
        Clear,
        PartlyCloudy,
        Cloudy,
        Overcast,
        Rain,
        Snow,
        Thunderstorm,
        Fog,
        Dust,
        Unknown
    }
}