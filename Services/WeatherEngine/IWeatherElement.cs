using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Interface for all weather elements
    /// </summary>
    public interface IWeatherElement
    {
        /// <summary>
        /// Creates and returns a UI element representing this weather component
        /// </summary>
        /// <param name="width">Width of the container</param>
        /// <param name="height">Height of the container</param>
        /// <returns>A UIElement representing this weather component</returns>
        UIElement Render(double width, double height);
    }
}
