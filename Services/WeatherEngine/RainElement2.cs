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
using PCStreamerLauncher.Logging;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Enhanced Cloud-aware Rain Element:
    /// - Uses realistic teardrop shapes for raindrops
    /// - Starts just below cloud layer with dynamic Y-offset
    /// - Follows cloud movement direction
    /// - Features reduced drop count for better visual clarity
    /// - Evenly distributes drops across cloud width
    /// </summary>
    public class RainElement2 : AnimatedWeatherElement
    {
        private readonly RainIntensity _intensity;
        private readonly string _weatherCode;
        private readonly bool _cloudMovesRight;
        private readonly ContextLogger _logger = Logger.GetLogger<RainElement2>();

        private readonly double _offsetY; // Vertical offset for rain starting position
        
        public RainElement2(RainIntensity intensity = RainIntensity.Medium, string weatherCode = "c02d", bool cloudMovesRight = true, double offsetY = 0)
        {
            _intensity = intensity;
            _weatherCode = weatherCode?.ToLowerInvariant() ?? "c02d";
            _cloudMovesRight = cloudMovesRight;
            _offsetY = offsetY;
        }

        /// <summary>
        /// Creates a teardrop-shaped Path element for more realistic raindrops
        /// </summary>
        private Path CreateTeardropShape(double thickness, double length, double opacity, Color rainColor)
        {
            try
            {
                // Create the teardrop path geometry
                PathGeometry teardropGeometry = new PathGeometry();
                PathFigure teardropFigure = new PathFigure();
                
                // Scale factors to control width and shape of the teardrop
                double widthFactor = thickness * 1.2;
                
                // Start at the top rounded part of the teardrop
                teardropFigure.StartPoint = new Point(0, length);
                
                // Use more control points for a more natural teardrop shape
                // Left curve - from top to tip (slightly asymmetrical)
                teardropFigure.Segments.Add(new BezierSegment(
                    // Make the top part very round with these control points
                    new Point(-widthFactor, length * 0.9),        // Control point 1 - wide bulb
                    new Point(-widthFactor * 0.4, length * 0.3),  // Control point 2 - gradual taper
                    new Point(0, 0),                             // End at bottom tip
                    true));
                
                // Right curve - from tip back to top (slightly asymmetrical)
                teardropFigure.Segments.Add(new BezierSegment(
                    new Point(widthFactor * 0.35, length * 0.3),   // Control point 1 - gradual taper
                    new Point(widthFactor * 0.9, length * 0.85),   // Control point 2 - wide bulb
                    new Point(0, length),                         // Back to top
                    true));
                
                // Close the figure
                teardropFigure.IsClosed = true;
                teardropGeometry.Figures.Add(teardropFigure);
                
                // Create the Path with the teardrop geometry
                Path teardrop = new Path
                {
                    Data = teardropGeometry,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.3, 1), // Slightly offset for more natural look
                        EndPoint = new Point(0.7, 0),  // Slight diagonal gradient 
                        GradientStops = new GradientStopCollection
                        {
                            // Bright highlight at the top-left
                            new GradientStop(Color.FromArgb((byte)(opacity * 255), 
                                                         (byte)Math.Min(255, rainColor.R + 60), 
                                                         (byte)Math.Min(255, rainColor.G + 60), 
                                                         (byte)Math.Min(255, rainColor.B + 60)), 0.0),
                            // Main water color
                            new GradientStop(Color.FromArgb((byte)(opacity * 255), 
                                                         rainColor.R, 
                                                         rainColor.G, 
                                                         rainColor.B), 0.4),
                            // Slightly darker at the tip for depth
                            new GradientStop(Color.FromArgb((byte)(opacity * 255),
                                                         (byte)Math.Max(0, rainColor.R - 20),
                                                         (byte)Math.Max(0, rainColor.G - 20),
                                                         (byte)Math.Max(0, rainColor.B - 20)), 0.9)
                        }
                    },
                    Stroke = null  // No stroke for smooth appearance
                };
                
                return teardrop;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create teardrop shape", ex);
                
                // Fallback to a simple oval
                Ellipse fallback = new Ellipse
                {
                    Width = thickness,
                    Height = length,
                    Fill = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), rainColor.R, rainColor.G, rainColor.B))
                };
                
                return new Path(); // Empty path as fallback
            }
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                var rainCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = true,
                    Background = Brushes.Transparent
                };

                // Slight horizontal drift to match clouds
                var driftTransform = new TranslateTransform();
                rainCanvas.RenderTransform = driftTransform;

                // Add a slight horizontal drift to the rain canvas that's different from cloud movement
                // This creates the impression of air currents affecting the falling drops
                var driftAnim = new DoubleAnimation
                {
                    From = _cloudMovesRight ? -8 : 8,  // Move opposite to cloud direction first
                    To = _cloudMovesRight ? 12 : -12,  // Then move further in cloud direction
                    Duration = TimeSpan.FromSeconds(15), // Longer period for more subtle motion
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } // Smooth acceleration/deceleration
                };
                driftTransform.BeginAnimation(TranslateTransform.XProperty, driftAnim);

                int dropCount;
                double minLength, maxLength;
                double minThickness, maxThickness;
                double opacity;
                double minDuration, maxDuration;
                double angleDeg = _cloudMovesRight ? -15 : 15;

                switch (_intensity)
                {
                    case RainIntensity.Light:
                        dropCount =3; // Few, larger teardrops
                        minLength = 10; maxLength = 16; // Even larger drops for light rain
                        minThickness = 4; maxThickness = 7;
                        opacity = 0.8;
                        minDuration = .5; maxDuration = 2.5; // Slow falling for larger drops
                        break;
                    case RainIntensity.Heavy:
                        dropCount = 5; // More, smaller teardrops
                        minLength = 10; maxLength = 16;
                        minThickness = 4; maxThickness = 7;
                        opacity = 0.8;
                        minDuration = .5; maxDuration = 1.5;
                        break;
                    default: // Medium
                        dropCount = 4; // Medium count of teardrops
                        minLength = 10; maxLength = 16;
                        minThickness = 4; maxThickness = 7;
                        opacity = 0.8;
                        minDuration = .5; maxDuration = 1.5;
                        break;
                }

                // Choose rain color - enhanced blues for better visibility
                Color rainColor;
                if (_weatherCode.EndsWith("n"))
                    rainColor = Color.FromRgb(140, 180, 220); // Brighter blue for night
                else if (_weatherCode.StartsWith("a01") || _weatherCode.StartsWith("a02") ||
                         _weatherCode.StartsWith("a03") || _weatherCode.StartsWith("a04") ||
                         _weatherCode.StartsWith("a05") || _weatherCode.StartsWith("a06"))
                    rainColor = Color.FromRgb(120, 180, 230);
                else if (_weatherCode.StartsWith("a07") || _weatherCode.StartsWith("a08"))
                    rainColor = Color.FromRgb(130, 180, 210);
                else if (_weatherCode.StartsWith("c04"))
                    rainColor = Color.FromRgb(130, 190, 230);
                else if (_weatherCode.StartsWith("c03"))
                    rainColor = Color.FromRgb(140, 190, 235);
                else if (_weatherCode.StartsWith("c02"))
                    rainColor = Color.FromRgb(150, 200, 240);
                else
                    rainColor = Color.FromRgb(160, 210, 250); // Bright blue for good visibility

                double cloudStartX = width * 0.15;
                double cloudWidth = width * 0.7;
                double cloudBottomY = height * 0.45;
                Random rand = new Random();

                // Create twice as many drops for more density in animation cycles
                int actualDropCount = dropCount * 2;

                // Create raindrops with staggered animation
                for (int i = 0; i < actualDropCount; i++)
                {
                    // Calculate size parameters for this drop
                    double length = minLength + rand.NextDouble() * (maxLength - minLength);
                    double thickness = minThickness + rand.NextDouble() * (maxThickness - minThickness);
                    
                    // Create the teardrop shape
                    Path teardrop = CreateTeardropShape(thickness, length, opacity, rainColor);
                    
                    // Calculate the horizontal position (evenly distributed with some randomness)
                    double sectionWidth = cloudWidth / dropCount; // Use original dropCount for spacing
                    double sectionMiddle = cloudStartX + ((i % dropCount) * sectionWidth) + (sectionWidth / 2);
                    double x = sectionMiddle + (rand.NextDouble() * sectionWidth * 0.6) - (sectionWidth * 0.3);
                    
                    // Start position is below the cloud with a more significant random variation
                    // This breaks up the "line" of drops that would form if they all started at exactly the same height
                    double yVariation = rand.NextDouble() * 20; // 0-30px variation
                    double y = cloudBottomY + _offsetY + 1 + yVariation;
                    
                    // Position the teardrop
                    Canvas.SetLeft(teardrop, x);
                    Canvas.SetTop(teardrop, y);
                    
                    // Add slight tilt based on angle
                    TransformGroup transforms = new TransformGroup();
                    
                    // Set proper rotation origin
                    teardrop.RenderTransformOrigin = new Point(0.5, 0.5);
                    
                    // Add rotation for subtle tilt
                    RotateTransform tiltTransform = new RotateTransform(angleDeg * 0.15); // Subtle tilt
                    transforms.Children.Add(tiltTransform);
                    
                    // Add transform for falling animation
                    TranslateTransform fallTransform = new TranslateTransform();
                    transforms.Children.Add(fallTransform);
                    
                    // Apply the transforms
                    teardrop.RenderTransform = transforms;
                    
                    // Calculate falling duration - larger drops fall slower
                    double sizeFactor = (thickness - minThickness) / (maxThickness - minThickness); // 0 to 1
                    double duration = minDuration + (maxDuration - minDuration) * (1 - sizeFactor); // Inverse of size
                    
                    // Calculate delay - use a different approach for first-frame preparation
                    double delay;
                    
                    // Calculate starting opacity - fade in for natural appearance
                    // Some drops start partially transparent, others already at full opacity
                    double startingOpacity = rand.NextDouble() * 0.7 + 0.3; // 30% to 100% initial opacity
                    
                    // For first half of the drops, have some already in progress for first frame
                    if (i < actualDropCount / 2) {
                        // Set delay to 0 - these drops are already in motion when the animation begins
                        delay = rand.NextDouble() * 0.1; // Very small random delay (0-0.1s)
                        
                        // Set a different starting opacity based on position
                        // Drops further down the screen are more opaque (as if they've already been falling longer)
                        double positionFactor = yVariation / 30.0; // 0-1 scale
                        startingOpacity = 0.5 + (positionFactor * 0.5); // 50-100% based on position
                    } else {
                        // Second half of drops use staggered delay for continuous rain effect
                        double baseDelay = ((i - actualDropCount/2) / (double)(actualDropCount/2)) * duration;
                        double randomDelay = rand.NextDouble() * 0.5; // Add small random component
                        delay = baseDelay + randomDelay;
                    }
                    
                    // Apply the starting opacity
                    teardrop.Opacity = startingOpacity;
                    DoubleAnimation fallAnimation = new DoubleAnimation
                    {
                        From = (i < actualDropCount / 4) ? (rand.NextDouble() * 60) : 0, // First 25% start mid-fall
                        To = height - y + 20, // Ensure it goes fully off screen
                        Duration = TimeSpan.FromSeconds(duration),
                        BeginTime = TimeSpan.FromSeconds(delay),
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    
                    // Create fade-in animation for smoother appearance
                    DoubleAnimation fadeInAnimation = new DoubleAnimation
                    {
                        From = startingOpacity,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.5), // Quick fade in
                        BeginTime = TimeSpan.FromSeconds(delay),
                    };
                    
                    // Apply the opacity animation
                    teardrop.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    
                    // Apply the falling animation
                    fallTransform.BeginAnimation(TranslateTransform.YProperty, fallAnimation);
                    
                    // Add the drop to the canvas
                    rainCanvas.Children.Add(teardrop);
                }

                _logger.Trace($"Rendered {_intensity} rain with cloud sync and adaptive visuals for '{_weatherCode}'.");
                return rainCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to render RainElement2.", ex);
                return null;
            }
        }
    }
}
