
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using PCStreamerLauncher.Logging;
using System.Numerics;
using System.Windows.Forms;

namespace PC_Launcher.Services.WeatherEngine
{
    public class SnowElement : AnimatedWeatherElement
    {
        private readonly SnowIntensity _intensity;
        private readonly double _offsetY;
        private readonly ContextLogger _logger = Logger.GetLogger<SnowElement>();

        public SnowElement(SnowIntensity intensity = SnowIntensity.Medium, double offsetY = 0)
        {
            _intensity = intensity;
            _offsetY = offsetY;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                Canvas snowCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = true,
                    Background = Brushes.Transparent
                };

                var driftTransform = new TranslateTransform();
                snowCanvas.RenderTransform = driftTransform;
                var driftAnim = new DoubleAnimation
                {
                    From = -8,
                    To = 12,
                    Duration = TimeSpan.FromSeconds(15),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                driftTransform.BeginAnimation(TranslateTransform.XProperty, driftAnim);

                int flakeCount;
                double minSize, maxSize;
                double minDuration, maxDuration;
                switch (_intensity)
                {
                    case SnowIntensity.Light:
                        flakeCount = 8;
                        minSize = 8;
                        maxSize = 16;
                        minDuration = .75;
                        maxDuration = 2.5;
                        break;
                    case SnowIntensity.Heavy:
                        flakeCount = 16;
                        minSize = 8;
                        maxSize = 14;
                        minDuration = .75;
                        maxDuration = 2.5;
                        break;
                    default:
                        flakeCount = 12;
                        minSize = 8;
                        maxSize = 16;
                        minDuration = .75;
                        maxDuration = 2.5;
                        break;
                }

                double cloudBottomY = height * 0.45;
                Random rand = new Random();

                for (int i = 0; i < flakeCount; i++)
                {
                    double size = minSize + rand.NextDouble() * (maxSize - minSize);
                    double flakeX = width * 0.2 + (width * 0.6) * (i / (double)flakeCount) + rand.NextDouble() * 10 - 5;
                    double yVariation = rand.NextDouble() * 30;
                    double baseY = cloudBottomY - 15 + _offsetY + yVariation;

                    double finalOpacity = 0.85;
                    Path snowflake = CreateSnowflakeShape(size, 0);
                    snowflake.Opacity = 0; // Start invisible

                    // Add blur to ~half the flakes for depth layering
                    if (rand.NextDouble() < 0.5)
                    {
                        snowflake.Effect = new BlurEffect { Radius = 2 + rand.NextDouble() * 2 };
                    }

                    Canvas.SetLeft(snowflake, flakeX);
                    Canvas.SetTop(snowflake, baseY);

                    TransformGroup transforms = new TransformGroup();
                    RotateTransform rotateTransform = new RotateTransform { Angle = rand.NextDouble() * 360 };
                    TranslateTransform translateTransform = new TranslateTransform();
                    transforms.Children.Add(rotateTransform);
                    transforms.Children.Add(translateTransform);
                    snowflake.RenderTransform = transforms;
                    snowflake.RenderTransformOrigin = new Point(0.5, 0.5);

                    double fallDistance = height - baseY;

                    // Parallax: larger flakes fall faster
                    double duration = minDuration + rand.NextDouble() * (maxDuration - minDuration);
                    duration *= 1.0 - (size - minSize) / (maxSize - minSize) * 0.25;
                    double beginDelay = rand.NextDouble() * 2;

                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = finalOpacity,
                        Duration = TimeSpan.FromSeconds(0.5),
                        BeginTime = TimeSpan.FromSeconds(beginDelay),
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    snowflake.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    DoubleAnimation fallAnim = new DoubleAnimation
                    {
                        BeginTime = TimeSpan.FromSeconds(beginDelay),
                        From = 0,
                        To = fallDistance,
                        Duration = TimeSpan.FromSeconds(duration),
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    DoubleAnimation swayAnim = new DoubleAnimation
                    {
                        From = -2,
                        To = 2,
                        Duration = TimeSpan.FromSeconds(duration * 0.8),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    DoubleAnimation rotAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = 360,
                        Duration = TimeSpan.FromSeconds(duration * 1.5),
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    translateTransform.BeginAnimation(TranslateTransform.YProperty, fallAnim);
                    translateTransform.BeginAnimation(TranslateTransform.XProperty, swayAnim);
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotAnim);

                    snowCanvas.Children.Add(snowflake);
                }

                _logger.Trace($"Rendered {_intensity} snow with custom snowflake shapes and enhancements.");
                return snowCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error rendering SnowElement.", ex);
                return null;
            }
        }

        private Path CreateSnowflakeShape(double size, double opacity)
        {
            GeometryGroup geometryGroup = new GeometryGroup();
            Point center = new Point(0, 0);
            double radius = size / 2;

            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);

                geometryGroup.Children.Add(new LineGeometry(center, new Point(x, y)));

                double midX = x * 0.5;
                double midY = y * 0.5;
                double branchLength = radius * 0.3;
                double branch1X = midX - branchLength * Math.Sin(angle);
                double branch1Y = midY + branchLength * Math.Cos(angle);
                double branch2X = midX + branchLength * Math.Sin(angle);
                double branch2Y = midY - branchLength * Math.Cos(angle);

                geometryGroup.Children.Add(new LineGeometry(new Point(midX, midY), new Point(branch1X, branch1Y)));
                geometryGroup.Children.Add(new LineGeometry(new Point(midX, midY), new Point(branch2X, branch2Y)));
            }

            return new Path
            {
                Data = geometryGroup,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Opacity = opacity
            };
        }
    }
}
