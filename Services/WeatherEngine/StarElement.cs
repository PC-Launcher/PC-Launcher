using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Renders a single star with twinkling animation
    /// </summary>
    public class StarElement : AnimatedWeatherElement
    {
        private readonly double _positionX;
        private readonly double _positionY;
        private readonly double _scale;

        /// <summary>
        /// Creates a new star element
        /// </summary>
        /// <param name="positionX">Relative X position (0-1)</param>
        /// <param name="positionY">Relative Y position (0-1)</param>
        /// <param name="scale">Size scale</param>
        public StarElement(double positionX = 0.5, double positionY = 0.5, double scale = 0.02)
        {
            _positionX = positionX;
            _positionY = positionY;
            _scale = scale;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Calculate actual position and size
                double x = width * _positionX;
                double y = height * _positionY;
                double size = width * _scale;

                // Create star container
                Canvas starCanvas = new Canvas
                {
                    Width = width,
                    Height = height
                };

                // Create basic star as a circle with glow
                Ellipse starGlow = new Ellipse
                {
                    Width = size * 2.5,
                    Height = size * 2.5,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(40, 255, 255, 255), 0.0),
                            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0)
                        }
                    }
                };

                Ellipse star = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White
                };

                // Position the elements
                Canvas.SetLeft(starGlow, x - (size * 2.5 / 2));
                Canvas.SetTop(starGlow, y - (size * 2.5 / 2));
                Canvas.SetLeft(star, x - (size / 2));
                Canvas.SetTop(star, y - (size / 2));

                starCanvas.Children.Add(starGlow);
                starCanvas.Children.Add(star);

                // Add twinkling animation with random timing for more natural effect
                Random random = new Random();
                double twinkleDuration = 2 + random.NextDouble() * 3;

                ApplyAnimation(star, UIElement.OpacityProperty, 1.0, 0.4,
                    TimeSpan.FromSeconds(twinkleDuration), true);

                ApplyAnimation(starGlow, UIElement.OpacityProperty, 0.8, 0.2,
                    TimeSpan.FromSeconds(twinkleDuration * 1.5), true);

                _logger.Trace($"Added star at ({x}, {y}) with size {size}");
                return starCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating star element", ex);
                return null;
            }
        }
    }
}
