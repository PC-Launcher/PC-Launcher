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
    /// Renders lightning effects for thunderstorm weather icons
    /// </summary>
    public class LightningElement : AnimatedWeatherElement
    {
        private readonly Random _random = new Random();

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create container for the lightning
                Canvas lightningCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.Transparent
                };

                // Use fewer lightning bolts - just 1 for cleaner appearance
                int numberOfBolts = 1;  // Reduced from 3 to 1 for cleaner appearance

                for (int i = 0; i < numberOfBolts; i++)
                {
                    // Each lightning bolt has a different position - centered for single bolt
                    double offsetX = _random.Next(-5, 5);  // Small random offset for variety
                    double centerX = width / 2 + offsetX;
                    bool isGroundStrike = _random.Next(3) == 0;  // Centered
                    double startY = height * 0.5;  // Start about halfway down

                    // Create zig-zag lightning bolt using a polygon with random variations
                    Polygon lightning = CreateLightningBolt(centerX, startY, height, isGroundStrike, i);
                    lightningCanvas.Children.Add(lightning);
                    Panel.SetZIndex(lightning, 1000);
                    lightning.Visibility = Visibility.Collapsed;

                    // Add a stronger glow around the lightning
                    Polygon lightningGlow = new Polygon
                    {
                        Points = new PointCollection(lightning.Points),
                        Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 200)),  // More opaque glow
                        Effect = new System.Windows.Media.Effects.BlurEffect
                        {
                            Radius = 15,  // Larger radius for more visible glow
                            KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                        }
                    };

                    lightningCanvas.Children.Insert(0, lightningGlow);
                    Panel.SetZIndex(lightningGlow, 999);
                    lightningGlow.Visibility = Visibility.Collapsed;

                    // Create flash animation with minimal initial delay but varied subsequent flashes
                    double initialDelay = 0.05 + _random.NextDouble() * 0.1;  // Very quick initial delay (50-150ms)
                    double flashDuration = 150 + _random.Next(150);          // actual: 150–300ms flash duration
                    double repeatInterval = 1 + _random.NextDouble() * 4.0; // 1-5 seconds between subsequent flashes

                    // Create storyboard for more complex animation
                    Storyboard lightningStoryboard = new Storyboard();
                    lightningStoryboard.RepeatBehavior = new RepeatBehavior(1);


                    // Flash animation with fade-in, hold, and fade-out
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(30),
                        AutoReverse = true,
                        RepeatBehavior = new RepeatBehavior(1),
                        EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut },
                        BeginTime = TimeSpan.FromSeconds(initialDelay),
                    };

                    var hold = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(30),
                        AutoReverse = true,
                        RepeatBehavior = new RepeatBehavior(1),
                        EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut },
                        BeginTime = fadeIn.BeginTime.Value + fadeIn.Duration.TimeSpan
                    };

                    var fadeOut = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(flashDuration),
                        BeginTime = hold.BeginTime.Value + hold.Duration.TimeSpan
                    };

                    foreach (var anim in new[] { fadeIn, hold, fadeOut })
                    {
                        var clone = anim.Clone();
                        Storyboard.SetTarget(clone, lightning);
                        Storyboard.SetTargetProperty(clone, new PropertyPath(UIElement.OpacityProperty));
                        lightningStoryboard.Children.Add(clone);

                        var glowClone = anim.Clone();
                        Storyboard.SetTarget(glowClone, lightningGlow);
                        Storyboard.SetTargetProperty(glowClone, new PropertyPath(UIElement.OpacityProperty));
                        lightningStoryboard.Children.Add(glowClone);
                    }


                    // Start the animation
                    lightningStoryboard.Begin();

                    // Setup timer for repeating the flash at random intervals
                    System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(repeatInterval);
                    timer.Tick += (sender, e) =>
                    {
                        lightningStoryboard.Stop();
                        
                        // Keep elements visible but reset opacity for next flash
                        lightning.Opacity = 0;
                        lightningGlow.Opacity = 0;
                        lightning.Visibility = Visibility.Visible;
                        lightningGlow.Visibility = Visibility.Visible;
                        
                        // Start the next flash immediately
                        lightningStoryboard.Begin();

                    };
                    // Make elements visible from the start
                    lightning.Visibility = Visibility.Visible;
                    lightningGlow.Visibility = Visibility.Visible;
                    lightning.Opacity = 0;
                    lightningGlow.Opacity = 0;
                    
                    // Start the animation and timer
                    lightningStoryboard.Begin();
                    timer.Start();
                    
                    // Create immediate first flash
                    FlashBolt(lightning);
                }

                _logger.Trace($"Added lightning effects");
                return lightningCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating lightning element", ex);
                return null;
            }
        }





        private Polygon CreateLightningBolt(double centerX, double startY, double height, bool isGroundStrike, int boltIndex)
        {
            double boltHeight = isGroundStrike ? height * 0.25 : height * 0.18;


            var points = new PointCollection
            {
                new Point(centerX + (0.043 * boltHeight), startY + (0.002 * boltHeight)),
                new Point(centerX + (-0.037 * boltHeight), startY + (0.000 * boltHeight)),
                new Point(centerX + (-0.297 * boltHeight), startY + (0.006 * boltHeight)),
                new Point(centerX + (-0.307 * boltHeight), startY + (0.010 * boltHeight)),
                new Point(centerX + (-0.521 * boltHeight), startY + (0.311 * boltHeight)),
                new Point(centerX + (-0.527 * boltHeight), startY + (0.322 * boltHeight)),
                new Point(centerX + (-0.527 * boltHeight), startY + (0.332 * boltHeight)),
                new Point(centerX + (-0.521 * boltHeight), startY + (0.344 * boltHeight)),
                new Point(centerX + (-0.512 * boltHeight), startY + (0.350 * boltHeight)),
                new Point(centerX + (-0.371 * boltHeight), startY + (0.352 * boltHeight)),
                new Point(centerX + (-0.369 * boltHeight), startY + (0.355 * boltHeight)),
                new Point(centerX + (-0.619 * boltHeight), startY + (0.635 * boltHeight)),
                new Point(centerX + (-0.627 * boltHeight), startY + (0.648 * boltHeight)),
                new Point(centerX + (-0.625 * boltHeight), startY + (0.664 * boltHeight)),
                new Point(centerX + (-0.609 * boltHeight), startY + (0.676 * boltHeight)),
                new Point(centerX + (-0.518 * boltHeight), startY + (0.678 * boltHeight)),
                new Point(centerX + (-0.479 * boltHeight), startY + (0.682 * boltHeight)),
                new Point(centerX + (-0.652 * boltHeight), startY + (0.969 * boltHeight)),
                new Point(centerX + (-0.652 * boltHeight), startY + (0.982 * boltHeight)),
                new Point(centerX + (-0.645 * boltHeight), startY + (0.994 * boltHeight)),
                new Point(centerX + (-0.637 * boltHeight), startY + (0.998 * boltHeight)),
                new Point(centerX + (-0.623 * boltHeight), startY + (0.998 * boltHeight)),
                new Point(centerX + (-0.609 * boltHeight), startY + (0.988 * boltHeight)),
                new Point(centerX + (-0.160 * boltHeight), startY + (0.596 * boltHeight)),
                new Point(centerX + (-0.154 * boltHeight), startY + (0.582 * boltHeight)),
                new Point(centerX + (-0.156 * boltHeight), startY + (0.572 * boltHeight)),
                new Point(centerX + (-0.162 * boltHeight), startY + (0.562 * boltHeight)),
                new Point(centerX + (-0.176 * boltHeight), startY + (0.557 * boltHeight)),
                new Point(centerX + (-0.279 * boltHeight), startY + (0.557 * boltHeight)),
                new Point(centerX + (0.018 * boltHeight), startY + (0.268 * boltHeight)),
                new Point(centerX + (0.018 * boltHeight), startY + (0.244 * boltHeight)),
                new Point(centerX + (0.002 * boltHeight), startY + (0.232 * boltHeight)),
                new Point(centerX + (-0.146 * boltHeight), startY + (0.230 * boltHeight)),
                new Point(centerX + (0.049 * boltHeight), startY + (0.037 * boltHeight)),
                new Point(centerX + (0.053 * boltHeight), startY + (0.029 * boltHeight)),
                new Point(centerX + (0.053 * boltHeight), startY + (0.016 * boltHeight))
            };

            return new Polygon
            {
                Points = points,
                Fill = Brushes.Gold,
                Stroke = Brushes.White,
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.LightYellow,
                    BlurRadius = 15,
                    Opacity = 0.8,
                    ShadowDepth = 0
                }
            };
        }

        private void FlashBolt(UIElement bolt)
        {
            var flash = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(30),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1),
                EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut }
            };

            var colorAnim = new ColorAnimation
            {
                From = Colors.White,
                To = Colors.Gold,
                Duration = TimeSpan.FromMilliseconds(80),
                AutoReverse = true
            };

            if (bolt is Shape shape && shape.Fill is SolidColorBrush solidBrush)
            {
                var animatedBrush = solidBrush.Clone();
                shape.Fill = animatedBrush;
                animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }

            bolt.BeginAnimation(UIElement.OpacityProperty, flash);
        }

    }
}
