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
    /// Renders rain elements for weather icons
    /// </summary>
    public class RainElement : AnimatedWeatherElement
    {
        private readonly RainIntensity _intensity;

        public RainElement(RainIntensity intensity = RainIntensity.Medium)
        {
            _intensity = intensity;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create a container for the raindrops
                Canvas rainCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = true // Keep raindrops inside the bounds
                };

                // Determine number of drops based on intensity
                int dropCount;
                switch (_intensity)
                {
                    case RainIntensity.Light:
                        dropCount = 5;
                        break;
                    case RainIntensity.Medium:
                        dropCount = 8;
                        break;
                    case RainIntensity.Heavy:
                        dropCount = 12;
                        break;
                    default:
                        dropCount = 8;
                        break;
                }

                // Add multiple raindrops with better distribution
                Random random = new Random();

                for (int i = 0; i < dropCount; i++)
                {
                    // Randomly distribute raindrops across the width of the cloud (with some padding)
                    double dropX = width * 0.2 + random.NextDouble() * (width * 0.6);
                    
                    // Vary the starting height slightly for more natural appearance
                    double dropY = height * (0.55 + random.NextDouble() * 0.1);

                    // Create a longer, more visible raindrop with slight angle variation
                    double angle = -5 + random.NextDouble() * 4; // Slight angle variation (-5 to -1 degrees)
                    double length = 8 + random.NextDouble() * 4; // Length between 8-12 pixels
                    
                    // Calculate end coordinates based on the angle and length
                    double radian = angle * Math.PI / 180.0;
                    double xOffset = Math.Sin(radian) * length;
                    double yOffset = Math.Cos(radian) * length;
                    
                    Line raindrop = new Line
                    {
                        X1 = dropX,
                        Y1 = dropY,
                        X2 = dropX + xOffset,
                        Y2 = dropY + yOffset,
                        Stroke = new SolidColorBrush(Color.FromArgb(230, 100, 149, 237)), // Increased opacity
                        StrokeThickness = 2.0 + (random.NextDouble() * 1.0), // Varied thickness between 2.0-3.0
                        StrokeEndLineCap = PenLineCap.Round
                    };

                    rainCanvas.Children.Add(raindrop);

                    // Create falling animation with varied speed and distance
                    TranslateTransform translateTransform = new TranslateTransform();
                    raindrop.RenderTransform = translateTransform;

                    // Vary the delay so drops don't all start falling at once
                    double delay = random.NextDouble() * 2;
                    
                    // Vary the duration (falling speed)
                    double duration = 0.7 + random.NextDouble() * 0.8;
                    
                    // Vary the falling distance 
                    double distance = height * (0.3 + random.NextDouble() * 0.2);

                    // Create the animation
                    var animation = new DoubleAnimation
                    {
                        From = 0,
                        To = distance,
                        Duration = TimeSpan.FromSeconds(duration),
                        BeginTime = TimeSpan.FromSeconds(delay),
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    
                    // Create a subtle horizontal sway for some raindrops (about 1 in 3)
                    if (random.NextDouble() < 0.3)
                    {
                        var swayAnimation = new DoubleAnimation
                        {
                            From = -2,
                            To = 2,
                            Duration = TimeSpan.FromSeconds(duration * 1.5),
                            BeginTime = TimeSpan.FromSeconds(delay),
                            AutoReverse = true,
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        
                        // Add horizontal sway
                        translateTransform.BeginAnimation(TranslateTransform.XProperty, swayAnimation);
                    }
                    
                    // Apply the animation to the transform, not the element
                    translateTransform.BeginAnimation(TranslateTransform.YProperty, animation);
                }

                _logger.Trace($"Added {dropCount} raindrops with '{_intensity}' intensity");
                return rainCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating rain element", ex);
                return null;
            }
        }
    }
}
