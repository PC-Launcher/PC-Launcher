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
    /// Renders a sky background with appropriate day/night appearance and weather condition
    /// </summary>
    public class SkyElement : AnimatedWeatherElement
    {
        private readonly bool _isDay;
        private readonly string _weatherCode;

        /// <summary>
        /// Creates a new sky element
        /// </summary>
        /// <param name="isDay">Whether to create a day or night sky</param>
        /// <param name="weatherCode">Weather code to determine appropriate sky color</param>
        public SkyElement(bool isDay, string weatherCode = null)
        {
            _isDay = isDay;
            _weatherCode = weatherCode;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create a container canvas for sky and stars
                Canvas skyCanvas = new Canvas
                {
                    Width = width,
                    Height = height
                };

                // Make the sky rectangle fill the entire canvas
                Rectangle sky = new Rectangle
                {
                    Width = width,
                    Height = height,
                    RadiusX = 10,
                    RadiusY = 10
                };

                // Position the sky to cover the entire canvas
                Canvas.SetLeft(sky, 0);
                Canvas.SetTop(sky, 0);

                if (_isDay)
                {
                    // Set sky gradient based on weather condition for day
                    sky.Fill = CreateDaySkyGradient(_weatherCode);
                }
                else
                {
                    // Set sky gradient based on weather condition for night
                    sky.Fill = CreateNightSkyGradient(_weatherCode);

                    // Add stars for night sky, unless it's heavily overcast, stormy, or foggy
                    bool addStars = !(_weatherCode?.StartsWith("c04") == true || 
                                      _weatherCode?.StartsWith("t") == true || 
                                      _weatherCode?.StartsWith("a0") == true);
                    
                    if (addStars)
                    {
                        AddStars(skyCanvas, width, height);
                    }
                }

                skyCanvas.Children.Add(sky);

                _logger.Trace($"Created {(_isDay ? "day" : "night")} sky background");
                return skyCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating sky element", ex);
                return null;
            }
        }

        /// <summary>
        /// Adds animated stars to the night sky
        /// </summary>
        private void AddStars(Canvas canvas, double width, double height)
        {
            try
            {
                Random random = new Random();

                // Scale number of stars with canvas size
                int starCount = (int)(width * height / 200); // Increased star density
                starCount = Math.Max(20, Math.Min(starCount, 50)); // Increased min/max stars

                for (int i = 0; i < starCount; i++)
                {
                    double starX = random.NextDouble() * width;
                    double starY = random.NextDouble() * height * 0.8; // Stars across more of the sky
                    double starSize = 1 + random.NextDouble() * 2.5; // Slightly larger stars

                    // Scale star size with canvas dimensions
                    starSize = Math.Max(1.5, Math.Min(starSize * (width / 200), 5)); // Increased size range

                    AddStar(canvas, starX, starY, starSize);
                }

                _logger.Trace($"Added {starCount} stars to night sky");
            }
            catch (Exception ex)
            {
                _logger.Error("Error adding stars to sky", ex);
            }
        }

        /// <summary>
        /// Adds a single star with twinkling animation
        /// </summary>
        private void AddStar(Canvas canvas, double x, double y, double size)
        {
            try
            {
                Ellipse star = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Opacity = 0.9 // Start with higher opacity
                };

                Canvas.SetLeft(star, x - size / 2);
                Canvas.SetTop(star, y - size / 2);
                canvas.Children.Add(star);

                // Add twinkling animation with random timing for more natural effect
                double twinkleDuration = 1.5 + new Random().NextDouble() * 3;
                ApplyAnimation(star, UIElement.OpacityProperty, 0.9, 0.3,
                    TimeSpan.FromSeconds(twinkleDuration), true);

                _logger.Trace($"Added star at ({x}, {y}) with size {size}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error adding star at ({x}, {y})", ex);
            }
        }

        /// <summary>
        /// Creates a gradient brush for the day sky based on the weather condition
        /// </summary>
        private Brush CreateDaySkyGradient(string weatherCode)
        {
            try
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };

                // Default clear day sky (bright blue)
                Color topColor = Color.FromRgb(135, 206, 235); // Sky blue at top
                Color bottomColor = Color.FromRgb(165, 216, 245); // Lighter blue at bottom

                if (!string.IsNullOrEmpty(weatherCode))
                {
                    string code = weatherCode.ToLowerInvariant();
                    
                    // Clear Day (c01d) - already set as default
                    
                    // Partly Cloudy (c02d, c03d)
                    if (code.StartsWith("c02") || code.StartsWith("c03"))
                    {
                        topColor = Color.FromRgb(164, 202, 246); // Light blue
                        bottomColor = Color.FromRgb(184, 212, 246); // Lighter blue
                    }
                    
                    // Overcast (c04d)
                    else if (code.StartsWith("c04"))
                    {
                        topColor = Color.FromRgb(184, 197, 214); // Light gray-blue
                        bottomColor = Color.FromRgb(200, 205, 215); // Lighter gray-blue
                    }
                    
                    // Light Rain (r01d, r02d, r04d)
                    else if (code.StartsWith("r01") || code.StartsWith("r02") || code.StartsWith("r04"))
                    {
                        topColor = Color.FromRgb(141, 156, 175); // Gray-blue
                        bottomColor = Color.FromRgb(167, 178, 189); // Lighter gray-blue
                    }
                    
                    // Heavy Rain (r03d, r05d, r06d)
                    else if (code.StartsWith("r03") || code.StartsWith("r05") || code.StartsWith("r06"))
                    {
                        topColor = Color.FromRgb(109, 122, 140); // Darker gray-blue
                        bottomColor = Color.FromRgb(125, 138, 155); // Slightly lighter gray-blue
                    }
                    
                    // Thunderstorm (t01d-t04d)
                    else if (code.StartsWith("t"))
                    {
                        topColor = Color.FromRgb(74, 84, 95); // Dark slate gray-blue
                        bottomColor = Color.FromRgb(93, 92, 90); // Yellow-gray tint
                    }
                    
                    // Light Snow (s01d, s02d, s04d)
                    else if (code.StartsWith("s01") || code.StartsWith("s02") || code.StartsWith("s04"))
                    {
                        topColor = Color.FromRgb(214, 229, 243); // Very light blue-gray
                        bottomColor = Color.FromRgb(230, 235, 240); // Almost white with blue tint
                    }
                    
                    // Heavy Snow (s03d, s05d, s06d)
                    else if (code.StartsWith("s03") || code.StartsWith("s05") || code.StartsWith("s06"))
                    {
                        topColor = Color.FromRgb(200, 210, 225); // Light blue-gray
                        bottomColor = Color.FromRgb(220, 225, 230); // Lighter blue-gray
                    }
                    
                    // Fog/Mist (a01d-a06d)
                    else if (code.StartsWith("a0"))
                    {
                        topColor = Color.FromRgb(208, 212, 217); // Pale gray
                        bottomColor = Color.FromRgb(218, 222, 227); // Lighter pale gray
                    }
                    
                    // Dust/Haze (a07d-a08d)
                    else if (code.StartsWith("a07") || code.StartsWith("a08"))
                    {
                        topColor = Color.FromRgb(201, 190, 168); // Pale yellow-brown
                        bottomColor = Color.FromRgb(211, 200, 178); // Lighter yellow-brown
                    }
                }

                gradient.GradientStops = new GradientStopCollection
                {
                    new GradientStop(topColor, 0.0),
                    new GradientStop(bottomColor, 1.0)
                };

                return gradient;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating day sky gradient", ex);
                
                // Fallback to default blue sky
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(135, 206, 235), 0.0),
                        new GradientStop(Color.FromRgb(165, 216, 245), 1.0)
                    }
                };
            }
        }

        /// <summary>
        /// Creates a gradient brush for the night sky based on the weather condition
        /// </summary>
        private Brush CreateNightSkyGradient(string weatherCode)
        {
            try
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };

                // Default clear night sky (dark blue)
                Color topColor = Color.FromRgb(0, 0, 50); // Dark blue at top
                Color bottomColor = Color.FromRgb(25, 25, 75); // Slightly lighter blue at bottom

                if (!string.IsNullOrEmpty(weatherCode))
                {
                    string code = weatherCode.ToLowerInvariant();
                    
                    // Clear Night (c01n) - already set as default
                    
                    // Partly Cloudy (c02n, c03n)
                    if (code.StartsWith("c02") || code.StartsWith("c03"))
                    {
                        topColor = Color.FromRgb(10, 10, 55); // Slightly lighter dark blue
                        bottomColor = Color.FromRgb(30, 30, 80); // Slightly lighter blue
                    }
                    
                    // Overcast (c04n)
                    else if (code.StartsWith("c04"))
                    {
                        topColor = Color.FromRgb(25, 25, 40); // Dark gray-blue
                        bottomColor = Color.FromRgb(40, 40, 55); // Slightly lighter gray-blue
                    }
                    
                    // Light Rain (r01n, r02n, r04n)
                    else if (code.StartsWith("r01") || code.StartsWith("r02") || code.StartsWith("r04"))
                    {
                        topColor = Color.FromRgb(20, 25, 40); // Dark gray-blue
                        bottomColor = Color.FromRgb(35, 40, 55); // Slightly lighter gray-blue
                    }
                    
                    // Heavy Rain (r03n, r05n, r06n)
                    else if (code.StartsWith("r03") || code.StartsWith("r05") || code.StartsWith("r06"))
                    {
                        topColor = Color.FromRgb(15, 20, 35); // Darker gray-blue
                        bottomColor = Color.FromRgb(30, 35, 50); // Slightly lighter gray-blue
                    }
                    
                    // Thunderstorm (t01n-t04n)
                    else if (code.StartsWith("t"))
                    {
                        topColor = Color.FromRgb(10, 10, 20); // Very dark blue-black
                        bottomColor = Color.FromRgb(25, 25, 35); // Dark gray-blue
                    }
                    
                    // Light Snow (s01n, s02n, s04n)
                    else if (code.StartsWith("s01") || code.StartsWith("s02") || code.StartsWith("s04"))
                    {
                        topColor = Color.FromRgb(20, 30, 55); // Blue-gray
                        bottomColor = Color.FromRgb(40, 50, 75); // Lighter blue-gray
                    }
                    
                    // Heavy Snow (s03n, s05n, s06n)
                    else if (code.StartsWith("s03") || code.StartsWith("s05") || code.StartsWith("s06"))
                    {
                        topColor = Color.FromRgb(15, 25, 50); // Darker blue-gray
                        bottomColor = Color.FromRgb(35, 45, 70); // Blue-gray
                    }
                    
                    // Fog/Mist (a01n-a06n)
                    else if (code.StartsWith("a0"))
                    {
                        topColor = Color.FromRgb(30, 30, 40); // Dark gray
                        bottomColor = Color.FromRgb(45, 45, 55); // Slightly lighter gray
                    }
                    
                    // Dust/Haze (a07n-a08n)
                    else if (code.StartsWith("a07") || code.StartsWith("a08"))
                    {
                        topColor = Color.FromRgb(35, 30, 25); // Dark yellow-brown
                        bottomColor = Color.FromRgb(50, 45, 40); // Slightly lighter brown
                    }
                }

                gradient.GradientStops = new GradientStopCollection
                {
                    new GradientStop(topColor, 0.0),
                    new GradientStop(bottomColor, 1.0)
                };

                return gradient;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating night sky gradient", ex);
                
                // Fallback to default night sky
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0, 0, 50), 0.0),
                        new GradientStop(Color.FromRgb(25, 25, 75), 1.0)
                    }
                };
            }
        }
    }
}
