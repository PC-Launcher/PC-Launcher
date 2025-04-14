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
    /// Creates and manages a sun element for weather icons
    /// </summary>
    public class SunElement : BaseWeatherElement
    {
        private readonly double _offsetX;
        private readonly double _offsetY;
        private readonly double _scale;

        public SunElement(double offsetX = 0, double offsetY = 0, double scale = 1.0)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _scale = scale;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Calculate precise center points for the sun
                double centerX = width / 2 + _offsetX;
                double centerY = height / 2 + _offsetY;

                // Calculate radius based on canvas dimensions
                double radius = Math.Min(width, height) / 3.2 * _scale;

                // Create a container for the sun and rays to keep them together
                Canvas sunCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = false // Allow rays to extend beyond if needed
                };

                // Add the sun circle
                Ellipse sun = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Colors.Yellow, 0.3),
                            new GradientStop(Color.FromRgb(255, 200, 0), 1.0)
                        }
                    }
                };

                // Position sun precisely in the center of its container
                Canvas.SetLeft(sun, centerX - radius);
                Canvas.SetTop(sun, centerY - radius);
                sunCanvas.Children.Add(sun);

                // Add sun rays with consistent positioning
                AddSunRays(sunCanvas, centerX, centerY, radius);

                // Add rotation animation that rotates the entire sun+rays together
                AnimateSunRotation(sunCanvas, centerX, centerY);

                _logger.Trace($"Added sun with radius {radius} at ({centerX}, {centerY}) with scale {_scale}");
                return sunCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error adding sun element", ex);
                return null;
            }
        }

        private void AddSunRays(Canvas sunCanvas, double centerX, double centerY, double radius)
        {
            try
            {
                double rayLength = radius * 0.4;
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    double startX = centerX + Math.Cos(angle) * radius;
                    double startY = centerY + Math.Sin(angle) * radius;
                    double endX = centerX + Math.Cos(angle) * (radius + rayLength);
                    double endY = centerY + Math.Sin(angle) * (radius + rayLength);

                    Line ray = new Line
                    {
                        X1 = startX,
                        Y1 = startY,
                        X2 = endX,
                        Y2 = endY,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = Math.Max(2, (centerX * 2) / 60)
                    };

                    sunCanvas.Children.Add(ray);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error adding sun rays", ex);
            }
        }

        private void AnimateSunRotation(Canvas sunCanvas, double centerX, double centerY)
        {
            try
            {
                RotateTransform rotateTransform = new RotateTransform(0, centerX, centerY);
                sunCanvas.RenderTransform = rotateTransform;

                DoubleAnimation rotateAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(120),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            }
            catch (Exception ex)
            {
                _logger.Error("Error animating sun rotation", ex);
            }
        }
    }
}