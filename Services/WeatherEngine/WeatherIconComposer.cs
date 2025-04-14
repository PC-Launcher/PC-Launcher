using PCStreamerLauncher.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Composes multiple weather elements into a complete weather icon
    /// </summary>
    public class WeatherIconComposer
    {
        private readonly ContextLogger _logger = PCStreamerLauncher.Logging.Logger.GetLogger<WeatherIconComposer>();
        private readonly List<IWeatherElement> _elements = new List<IWeatherElement>();
        private readonly double _width;
        private readonly double _height;

        /// <summary>
        /// Creates a new composer for weather icons
        /// </summary>
        /// <param name="width">Width of the final icon</param>
        /// <param name="height">Height of the final icon</param>
        public WeatherIconComposer(double width, double height)
        {
            _width = width;
            _height = height;
        }

        /// <summary>
        /// Adds a weather element to the composition
        /// </summary>
        /// <param name="element">Element to add</param>
        public void AddElement(IWeatherElement element)
        {
            _elements.Add(element);
        }

        /// <summary>
        /// Composes all added elements into a final weather icon
        /// </summary>
        /// <returns>A UIElement representing the complete weather icon</returns>
        public UIElement Compose()
        {
            try
            {
                // Create a container for the weather icon
                Canvas canvas = new Canvas
                {
                    Width = _width,
                    Height = _height,
                    Background = Brushes.Transparent,
                    ClipToBounds = true // This ensures all elements stay within the canvas bounds
                };

                // Render and add each element to the canvas
                foreach (var element in _elements)
                {
                    try
                    {
                        var uiElement = element.Render(_width, _height);
                        if (uiElement != null)
                        {
                            canvas.Children.Add(uiElement);

                            // Special case for SkyElement - it should always be at the back
                            if (element is SkyElement)
                            {
                                // Move the sky to the back
                                canvas.Children.Remove(uiElement);
                                canvas.Children.Insert(0, uiElement);
                            }
                            
                            // Special case for LightningElement - it should always be at the front
                            if (element is LightningElement)
                            {
                                Panel.SetZIndex(uiElement, 10);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error rendering element {element.GetType().Name}", ex);
                    }
                }

                _logger.Info($"Successfully composed weather icon with {_elements.Count} elements");
                return canvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error composing weather icon", ex);
                return new Border
                {
                    Width = _width,
                    Height = _height,
                    Background = new SolidColorBrush(Colors.Gray)
                };
            }
        }
    }
}
