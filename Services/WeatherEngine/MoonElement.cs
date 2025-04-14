using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Linq;

namespace PC_Launcher.Services.WeatherEngine
{
    public class MoonElement : BaseWeatherElement
    {
        private readonly double _offsetX;
        private readonly double _offsetY;
        private readonly double _scale;
        private readonly string _weatherCode;

        public MoonElement(double offsetX = 0, double offsetY = 0, double scale = 1.0, string weatherCode = null)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _scale = scale;
            _weatherCode = weatherCode;
        }

        private Color GetNightSkyColor(string weatherCode)
        {
            Color skyColor = Color.FromRgb(10, 10, 55);

            if (string.IsNullOrEmpty(weatherCode))
                return skyColor;

            string code = weatherCode.ToLowerInvariant();

            if (code == "c01n") skyColor = Color.FromRgb(0, 0, 50);
            else if (code.StartsWith("c02") || code.StartsWith("c03")) skyColor = Color.FromRgb(10, 10, 55);
            else if (code.StartsWith("c04")) skyColor = Color.FromRgb(25, 25, 40);
            else if (code.StartsWith("r01") || code.StartsWith("r02") || code.StartsWith("r04")) skyColor = Color.FromRgb(20, 25, 40);
            else if (code.StartsWith("r03") || code.StartsWith("r05") || code.StartsWith("r06")) skyColor = Color.FromRgb(15, 20, 35);
            else if (code.StartsWith("t")) skyColor = Color.FromRgb(10, 10, 20);
            else if (code.StartsWith("s01") || code.StartsWith("s02") || code.StartsWith("s04")) skyColor = Color.FromRgb(20, 30, 55);
            else if (code.StartsWith("s03") || code.StartsWith("s05") || code.StartsWith("s06")) skyColor = Color.FromRgb(15, 25, 50);
            else if (code.StartsWith("a0")) skyColor = Color.FromRgb(30, 30, 40);
            else if (code.StartsWith("a07") || code.StartsWith("a08")) skyColor = Color.FromRgb(35, 30, 25);

            return skyColor;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                Canvas moonCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = false
                };

                double centerX = width / 2 + _offsetX;
                double centerY = height / 2 + _offsetY;
                double radius = Math.Min(width, height) / 2.5 * _scale;
                Color skyColor = GetNightSkyColor(_weatherCode);

                // double phase = 0.0;  // 🌑 New Moon
                // double phase = 0.125; // 🌒 Waxing Crescent
                // double phase = 0.25; // 🌓 First Quarter
                // double phase = 0.375; // 🌔 Waxing Gibbous
                // double phase = 0.5; // 🌕 Full Moon
                // double phase = 0.625; // 🌖 Waning Gibbous
                // double phase = 0.75; // 🌗 Last Quarter
                // double phase = 0.875; // 🌘 Waning Crescent

                double phase = CalculateMoonPhase();

                // Add glow first (background element)
                AddMoonGlow(moonCanvas, centerX, centerY, radius);

                // Create moon surface
                var moonSurface = CreateMoonSurface(centerX, centerY, radius);
                moonCanvas.Children.Add(moonSurface);

                // Add lunar maria (dark patches)
                AddLunarMaria(moonCanvas, centerX, centerY, radius);

                // Add craters
                AddCraters(moonCanvas, centerX, centerY, radius, phase);

                // Render phase
                RenderMoonPhase(moonCanvas, centerX, centerY, radius, phase, skyColor);

                // Add earthshine for crescent phases
                if (phase < 0.3 || phase > 0.7)
                {
                    AddEarthshine(moonCanvas, centerX, centerY, radius, phase);
                }

                _logger.Trace($"Added moon with radius {radius} at ({centerX}, {centerY}) with scale {_scale}");
                return moonCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating moon element", ex);
                return null;
            }
        }

        private Path CreateMoonSurface(double centerX, double centerY, double radius)
        {
            var moonSurface = new Path
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.3, 0.3),
                    EndPoint = new Point(0.7, 0.7),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(255, 253, 230), 0.0),
                        new GradientStop(Color.FromRgb(250, 245, 210), 0.5),
                        new GradientStop(Color.FromRgb(240, 235, 200), 1.0)
                    }
                },
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX, centerY),
                    RadiusX = radius,
                    RadiusY = radius
                }
            };

            return moonSurface;
        }

        private void AddLunarMaria(Canvas moonCanvas, double centerX, double centerY, double radius)
        {
            // Add several irregular dark patches (maria) on the moon's surface
            var maria1 = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX - radius * 0.3, centerY - radius * 0.2),
                    RadiusX = radius * 0.4,
                    RadiusY = radius * 0.3
                },
                Fill = new SolidColorBrush(Color.FromArgb(120, 200, 195, 175)),
                Opacity = 0.5
            };
            moonCanvas.Children.Add(maria1);

            var maria2 = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX + radius * 0.2, centerY + radius * 0.3),
                    RadiusX = radius * 0.3,
                    RadiusY = radius * 0.25
                },
                Fill = new SolidColorBrush(Color.FromArgb(120, 205, 200, 180)),
                Opacity = 0.5
            };
            moonCanvas.Children.Add(maria2);

            var maria3 = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX - radius * 0.1, centerY + radius * 0.1),
                    RadiusX = radius * 0.2,
                    RadiusY = radius * 0.15
                },
                Fill = new SolidColorBrush(Color.FromArgb(120, 210, 205, 185)),
                Opacity = 0.5
            };
            moonCanvas.Children.Add(maria3);

            // Add additional maria for more detailed appearance
            var maria4 = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX + radius * 0.4, centerY - radius * 0.35),
                    RadiusX = radius * 0.25,
                    RadiusY = radius * 0.2
                },
                Fill = new SolidColorBrush(Color.FromArgb(120, 195, 190, 170)),
                Opacity = 0.5
            };
            moonCanvas.Children.Add(maria4);

            var maria5 = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX - radius * 0.35, centerY + radius * 0.35),
                    RadiusX = radius * 0.2,
                    RadiusY = radius * 0.15
                },
                Fill = new SolidColorBrush(Color.FromArgb(120, 200, 195, 175)),
                Opacity = 0.5
            };
            moonCanvas.Children.Add(maria5);
        }

        private void AddCraters(Canvas moonCanvas, double centerX, double centerY, double radius, double phase)
        {
            bool lightFromLeft = phase < 0.5; // Determine direction of lighting

            // Main craters
            AddStylizedCrater(moonCanvas, centerX, centerY, radius * 0.25, radius * 0.22, lightFromLeft, 0.9); // Center crater
            AddStylizedCrater(moonCanvas, centerX - radius * 0.4, centerY - radius * 0.3, radius * 0.2, radius * 0.16, lightFromLeft, 0.85);
            AddStylizedCrater(moonCanvas, centerX + radius * 0.35, centerY - radius * 0.2, radius * 0.15, radius * 0.14, lightFromLeft, 0.8);
            AddStylizedCrater(moonCanvas, centerX - radius * 0.25, centerY + radius * 0.4, radius * 0.17, radius * 0.15, lightFromLeft, 0.75);
            AddStylizedCrater(moonCanvas, centerX + radius * 0.5, centerY + radius * 0.35, radius * 0.18, radius * 0.16, lightFromLeft, 0.7);

            // Medium craters
            AddStylizedCrater(moonCanvas, centerX - radius * 0.1, centerY - radius * 0.45, radius * 0.12, radius * 0.1, lightFromLeft, 0.7);
            AddStylizedCrater(moonCanvas, centerX + radius * 0.2, centerY + radius * 0.15, radius * 0.1, radius * 0.09, lightFromLeft, 0.75);
            AddStylizedCrater(moonCanvas, centerX - radius * 0.6, centerY + radius * 0.1, radius * 0.11, radius * 0.1, lightFromLeft, 0.65);

            // Small craters
            AddStylizedCrater(moonCanvas, centerX + radius * 0.6, centerY - radius * 0.5, radius * 0.06, radius * 0.055, lightFromLeft, 0.6);
            AddStylizedCrater(moonCanvas, centerX - radius * 0.35, centerY - radius * 0.55, radius * 0.05, radius * 0.045, lightFromLeft, 0.5);
            AddStylizedCrater(moonCanvas, centerX + radius * 0.15, centerY - radius * 0.15, radius * 0.04, radius * 0.035, lightFromLeft, 0.55);
            AddStylizedCrater(moonCanvas, centerX - radius * 0.15, centerY + radius * 0.2, radius * 0.05, radius * 0.045, lightFromLeft, 0.5);
            AddStylizedCrater(moonCanvas, centerX + radius * 0.4, centerY + radius * 0.55, radius * 0.06, radius * 0.055, lightFromLeft, 0.6);
            AddStylizedCrater(moonCanvas, centerX - radius * 0.55, centerY + radius * 0.45, radius * 0.04, radius * 0.035, lightFromLeft, 0.55);
        }

        private void AddStylizedCrater(Canvas canvas, double x, double y, double width, double height, bool lightFromLeft, double depthFactor)
        {
            // Simplified stylized crater for a cleaner look
            var craterBase = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(210, 205, 185), 0.0),
                        new GradientStop(Color.FromRgb(190, 185, 165), 0.7),
                        new GradientStop(Color.FromRgb(170, 165, 145), 1.0)
                    }
                }
            };
            Canvas.SetLeft(craterBase, x - width / 2);
            Canvas.SetTop(craterBase, y - height / 2);
            canvas.Children.Add(craterBase);

            // Add interior shadow for depth
            var innerShadow = new Ellipse
            {
                Width = width * 0.85,
                Height = height * 0.85,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(160, 155, 135), 0.0),
                        new GradientStop(Color.FromRgb(180, 175, 155), 1.0)
                    }
                },
                Opacity = 0.8
            };
            Canvas.SetLeft(innerShadow, x - width * 0.85 / 2);
            Canvas.SetTop(innerShadow, y - height * 0.85 / 2);
            canvas.Children.Add(innerShadow);

            // Add shadow on one side
            var shadowRectangle = new Rectangle
            {
                Width = width * 0.5,
                Height = height,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb((byte)(150 * depthFactor), 0, 0, 10), 0.0),
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0)
                    }
                },
                RadiusX = width * 0.5,
                RadiusY = height * 0.5,
                Opacity = 0.6
            };

            // Position the shadow based on lighting direction
            if (lightFromLeft)
            {
                Canvas.SetLeft(shadowRectangle, x);
            }
            else
            {
                Canvas.SetLeft(shadowRectangle, x - width * 0.5);
            }
            Canvas.SetTop(shadowRectangle, y - height / 2);

            // Create clipping path to constrain shadow within crater
            var clipGeometry = new EllipseGeometry(new Point(x, y), width / 2, height / 2);
            var clipPath = new Path { Data = clipGeometry };
            shadowRectangle.Clip = clipGeometry;

            canvas.Children.Add(shadowRectangle);

            // Add highlight on opposite side
            var highlightRectangle = new Rectangle
            {
                Width = width * 0.5,
                Height = height,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb((byte)(120 * depthFactor), 255, 255, 245), 0.0),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0)
                    }
                },
                RadiusX = width * 0.5,
                RadiusY = height * 0.5,
                Opacity = 0.5
            };

            // Position the highlight based on lighting direction (opposite to shadow)
            if (lightFromLeft)
            {
                Canvas.SetLeft(highlightRectangle, x - width * 0.5);
            }
            else
            {
                Canvas.SetLeft(highlightRectangle, x);
            }
            Canvas.SetTop(highlightRectangle, y - height / 2);

            // Clip the highlight to the crater shape
            highlightRectangle.Clip = clipGeometry;

            canvas.Children.Add(highlightRectangle);

            // Add tiny inner craters for realism in larger craters
            if (width > 20 && new Random().NextDouble() < 0.7)
            {
                double innerX = x + (new Random().NextDouble() - 0.5) * width * 0.4;
                double innerY = y + (new Random().NextDouble() - 0.5) * height * 0.4;
                double innerWidth = width * 0.15;
                double innerHeight = height * 0.15;

                var innerCrater = new Ellipse
                {
                    Width = innerWidth,
                    Height = innerHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(165, 160, 140)),
                    Opacity = 0.7
                };
                Canvas.SetLeft(innerCrater, innerX - innerWidth / 2);
                Canvas.SetTop(innerCrater, innerY - innerHeight / 2);
                canvas.Children.Add(innerCrater);
            }
        }

        private void RenderMoonPhase(Canvas moonCanvas, double centerX, double centerY, double radius, double phase, Color skyColor)
        {
            // Always apply shadow except exactly at full moon (phase = 0.5)
            if (Math.Abs(phase - 0.5) > 0.001)
            {
                double shadowWidth = radius * 2 * Math.Abs(0.5 - phase);
                double clipX = phase < 0.5 ? centerX + radius - shadowWidth : centerX - radius;

                // Use a darker, more opaque shadow for visibility
                var shadow = new Path
                {
                    Data = new EllipseGeometry
                    {
                        Center = new Point(centerX, centerY),
                        RadiusX = radius,
                        RadiusY = radius
                    },
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(phase < 0.5 ? 1 : 0, 0),
                        EndPoint = new Point(phase < 0.5 ? 0 : 1, 0),
                        GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(255, 0, 0, 20), 0),
                    new GradientStop(Color.FromArgb(200, skyColor.R, skyColor.G, skyColor.B), 0.5),
                    new GradientStop(Color.FromArgb(0, skyColor.R, skyColor.G, skyColor.B), 1)
                }
                    },
                    Opacity = 1.0 // Full opacity for more obvious shadow
                };

                moonCanvas.Children.Add(shadow);
            }
        }

        private void AddMoonGlow(Canvas moonCanvas, double centerX, double centerY, double radius)
        {
            var glow = new Ellipse
            {
                Width = radius * 3.0,
                Height = radius * 3.0,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(100, 255, 255, 230), 0.3),
                        new GradientStop(Color.FromArgb(50, 255, 255, 220), 0.6),
                        new GradientStop(Color.FromArgb(0, 255, 255, 210), 1.0)
                    }
                }
            };

            Canvas.SetLeft(glow, centerX - radius * 1.5);
            Canvas.SetTop(glow, centerY - radius * 1.5);
            moonCanvas.Children.Insert(0, glow);

            // Add a second, more intense inner glow
            var innerGlow = new Ellipse
            {
                Width = radius * 2.4,
                Height = radius * 2.4,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(130, 255, 255, 240), 0.4),
                        new GradientStop(Color.FromArgb(70, 255, 255, 230), 0.7),
                        new GradientStop(Color.FromArgb(0, 255, 255, 220), 1.0)
                    }
                }
            };

            Canvas.SetLeft(innerGlow, centerX - radius * 1.2);
            Canvas.SetTop(innerGlow, centerY - radius * 1.2);
            moonCanvas.Children.Insert(1, innerGlow);
        }

        private void AddEarthshine(Canvas moonCanvas, double centerX, double centerY, double radius, double phase)
        {
            // Only show earthshine on the dark side
            double earthshineWidth = radius * 0.15;
            double earthshineX = phase < 0.5 ? centerX - radius : centerX + radius;

            var earthshine = new Path
            {
                Data = new EllipseGeometry
                {
                    Center = new Point(centerX, centerY),
                    RadiusX = radius,
                    RadiusY = radius
                },
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(phase < 0.5 ? 0 : 1, 0),
                    EndPoint = new Point(phase < 0.5 ? 1 : 0, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0, 200, 200, 255), 0),
                        new GradientStop(Color.FromArgb(30, 200, 200, 255), 0.3),
                        new GradientStop(Color.FromArgb(0, 200, 200, 255), 0.6)
                    }
                },
                Opacity = 0.4
            };

            moonCanvas.Children.Add(earthshine);
        }

        private double CalculateMoonPhase()
        {
            DateTime now = DateTime.UtcNow;
            int year = now.Year;
            int month = now.Month;
            int day = now.Day;

            if (month < 3)
            {
                year--;
                month += 12;
            }

            ++month;
            double c = 365.25 * year;
            double e = 30.6 * month;
            double jd = c + e + day - 694039.09;
            jd /= 29.5305882;
            double b = jd - Math.Floor(jd);
            return b;
        }
    }
}
