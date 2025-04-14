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
    /// Renders an enhanced cloud with modern, cohesive shape for weather icons
    /// </summary>
    public class EnhancedCloudElement : AnimatedWeatherElement
    {
        // === Public properties for configuration ===
        public double OffsetX { get => _offsetX; set => _offsetX = value; }
        public double OffsetY { get => _offsetY; set => _offsetY = value; }
        public double Scale { get => _scale; set => _scale = value; }
        public bool IsDarkCloud { get => _isDarkCloud; set => _isDarkCloud = value; }
        // ============================================

        private double _offsetX;
        private double _offsetY;
        private double _scale;
        private bool _isDarkCloud;

        // Standardized animation duration for all cloud types
        private const double CLOUD_ANIMATION_DURATION = 30.0; // 30 seconds for a complete cycle

        public EnhancedCloudElement(double offsetX = 0, double offsetY = 0, double scale = 1.0, bool isDarkCloud = false)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _scale = scale;
            _isDarkCloud = isDarkCloud;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Cloud dimensions and positioning
                double cloudWidth = width * 0.8 * _scale;
                double cloudHeight = height * 0.5 * _scale;

                // Ensure cloud stays within bounds
                double left = Math.Max(0, Math.Min(width - cloudWidth, width / 2 - cloudWidth / 2 + _offsetX));
                double top = Math.Max(0, Math.Min(height - cloudHeight, height / 2 - cloudHeight / 2 + _offsetY));

                // Create container for all cloud parts
                Canvas cloudCanvas = new Canvas
                {
                    Width = cloudWidth,
                    Height = cloudHeight,
                    Background = Brushes.Transparent // Explicitly set transparent background
                };

                Canvas.SetLeft(cloudCanvas, left);
                Canvas.SetTop(cloudCanvas, top);

                // Always use overcast cloud styling for all cloud types
                Color baseCloudColor = Color.FromRgb(190, 190, 200); // Always use grayer clouds for consistency
                Color shadowColor = Color.FromRgb(140, 140, 160);    // Always use darker shadow for consistency

                // Create modern cloud shape with consistent styling
                CreateModernCloud(cloudCanvas, cloudWidth, cloudHeight, baseCloudColor, shadowColor);

                // Apply standardized animation to all cloud elements
                ApplyStandardizedCloudAnimation(cloudCanvas, width, height);

                _logger.Trace($"Added enhanced cloud with scale {_scale} at offset ({_offsetX}, {_offsetY})");
                return cloudCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating enhanced cloud element", ex);
                return null;
            }
        }

        private void CreateModernCloud(Canvas cloudCanvas, double cloudWidth, double cloudHeight, Color baseColor, Color shadowColor)
        {
            try
            {
                // Create a unified cloud shape with a clean silhouette
                Path cloudShape = new Path
                {
                    Fill = new SolidColorBrush(baseColor),
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    StrokeThickness = 1
                };

                // Create a smooth, modern cloud shape
                PathGeometry cloudGeometry = new PathGeometry();
                PathFigure cloudFigure = new PathFigure { IsClosed = true };

                // Start at bottom left
                cloudFigure.StartPoint = new Point(cloudWidth * 0.2, cloudHeight * 0.8);

                // Bottom edge
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.4, cloudHeight * 0.85),
                    new Point(cloudWidth * 0.6, cloudHeight * 0.85),
                    new Point(cloudWidth * 0.8, cloudHeight * 0.8),
                    true));

                // Right side
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.95, cloudHeight * 0.75),
                    new Point(cloudWidth * 0.98, cloudHeight * 0.6),
                    new Point(cloudWidth * 0.9, cloudHeight * 0.5),
                    true));

                // Top right bump
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.95, cloudHeight * 0.4),
                    new Point(cloudWidth * 0.9, cloudHeight * 0.3),
                    new Point(cloudWidth * 0.8, cloudHeight * 0.3),
                    true));

                // Top middle bump
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.7, cloudHeight * 0.2),
                    new Point(cloudWidth * 0.6, cloudHeight * 0.15),
                    new Point(cloudWidth * 0.5, cloudHeight * 0.2),
                    true));

                // Top left bump
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.4, cloudHeight * 0.15),
                    new Point(cloudWidth * 0.3, cloudHeight * 0.2),
                    new Point(cloudWidth * 0.25, cloudHeight * 0.3),
                    true));

                // Left side
                cloudFigure.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.15, cloudHeight * 0.4),
                    new Point(cloudWidth * 0.1, cloudHeight * 0.6),
                    new Point(cloudWidth * 0.2, cloudHeight * 0.8),
                    true));

                cloudGeometry.Figures.Add(cloudFigure);
                cloudShape.Data = cloudGeometry;

                // Add the cloud shadow/highlight effect
                Path cloudHighlight = new Path
                {
                    Data = cloudGeometry,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Colors.White, 0.0),
                            new GradientStop(Colors.Transparent, 0.5),
                            new GradientStop(shadowColor, 0.9)
                        }
                    },
                    Opacity = 0.6
                };

                // Create internal contour lines to suggest volume without using visible circles
                Path cloudContour = new Path
                {
                    Stroke = new SolidColorBrush(shadowColor),
                    StrokeThickness = 1.5,
                    Opacity = 0.3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                // Create a few subtle contour lines
                PathGeometry contourGeometry = new PathGeometry();

                // First contour - right side
                PathFigure contour1 = new PathFigure { IsClosed = false };
                contour1.StartPoint = new Point(cloudWidth * 0.65, cloudHeight * 0.45);
                contour1.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.75, cloudHeight * 0.4),
                    new Point(cloudWidth * 0.8, cloudHeight * 0.45),
                    new Point(cloudWidth * 0.85, cloudHeight * 0.55),
                    true));
                contourGeometry.Figures.Add(contour1);

                // Second contour - middle
                PathFigure contour2 = new PathFigure { IsClosed = false };
                contour2.StartPoint = new Point(cloudWidth * 0.3, cloudHeight * 0.4);
                contour2.Segments.Add(new BezierSegment(
                    new Point(cloudWidth * 0.45, cloudHeight * 0.35),
                    new Point(cloudWidth * 0.55, cloudHeight * 0.35),
                    new Point(cloudWidth * 0.65, cloudHeight * 0.45),
                    true));
                contourGeometry.Figures.Add(contour2);

                cloudContour.Data = contourGeometry;

                // Add all elements in proper order
                cloudCanvas.Children.Add(cloudShape);    // Base cloud shape
                cloudCanvas.Children.Add(cloudHighlight); // Gradient for depth
                cloudCanvas.Children.Add(cloudContour);   // Subtle contour lines
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating modern cloud", ex);
            }
        }

        private void ApplyStandardizedCloudAnimation(Canvas cloudCanvas, double width, double height)
        {
            try
            {
                // Use TranslateTransform for simple, consistent movement
                TranslateTransform translateTransform = new TranslateTransform();
                cloudCanvas.RenderTransform = translateTransform;

                // Create standardized keyframe animation for all cloud types
                DoubleAnimationUsingKeyFrames horizontalAnimation = new DoubleAnimationUsingKeyFrames();

                // Add keyframes with smooth transitions
                // Start centered
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

                // Ease to the right
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.25)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Ease to the left
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(-width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.75)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Return to center
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Set to repeat forever
                horizontalAnimation.RepeatBehavior = RepeatBehavior.Forever;

                // Apply the animation
                translateTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnimation);
            }
            catch (Exception ex)
            {
                _logger.Error("Error applying standardized cloud animation", ex);
            }
        }
    }

    /// <summary>
    /// Creates a group of enhanced clouds for more complex scenes with drift animation
    /// </summary>
    public class EnhancedCloudGroupElement : AnimatedWeatherElement
    {
        private readonly double _offsetY;
        private readonly string _weatherCode;
        private const double CLOUD_ANIMATION_DURATION = 30.0; // Standard 30 seconds for all cloud types

        public EnhancedCloudGroupElement(string weatherCode = "c03d", double offsetY = -15)
        {
            _offsetY = offsetY;
            {
                _weatherCode = weatherCode;
            }
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create a container for the cloud group
                Canvas container = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.Transparent
                };

                // All clouds should use the standard overcast styling for consistency

                // Create container for cloud animation
                Canvas cloudMovementCanvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.Transparent
                };

                // Add multiple overlapping clouds with different sizes and positions
                var cloud1 = new EnhancedCloudElement(
                    offsetX: width * -0.05,
                    offsetY: _offsetY,
                    scale: 1.0,
                    isDarkCloud: true); // Always use grayer clouds for consistency


                var cloud2 = new EnhancedCloudElement(
                    offsetX: width * 0.1,
                    offsetY: _offsetY,
                    scale: 0.9,
                    isDarkCloud: true); // Always use grayer clouds for consistency


                // Add clouds to movement container
                cloudMovementCanvas.Children.Add(cloud1.Render(width, height));
                cloudMovementCanvas.Children.Add(cloud2.Render(width, height));

                // Only add a third cloud for "overcast" conditions
                if (_weatherCode.StartsWith("c04") || _weatherCode.StartsWith("t") ||
                    _weatherCode.StartsWith("r03") || _weatherCode.StartsWith("r06"))
                {
                    var cloud3 = new EnhancedCloudElement(
                        offsetX: width * -0.15,
                        offsetY: height * 0.02,
                        scale: 0.95,
                        isDarkCloud: true);  // Always use grayer clouds for consistency
                    cloudMovementCanvas.Children.Add(cloud3.Render(width, height));
                }

                // Add the movement container to the main container
                container.Children.Add(cloudMovementCanvas);

                // Apply unified drift animation to the entire cloud group
                ApplyStandardizedCloudAnimation(cloudMovementCanvas, width, height);

                _logger.Trace($"Added enhanced cloud group for weather code {_weatherCode}");
                return container;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating enhanced cloud group", ex);
                return null;
            }
        }

        private void ApplyStandardizedCloudAnimation(Canvas cloudCanvas, double width, double height)
        {
            try
            {
                // Create a transform for the entire cloud group
                TranslateTransform translateTransform = new TranslateTransform();
                cloudCanvas.RenderTransform = translateTransform;

                // Create standardized keyframe animation
                DoubleAnimationUsingKeyFrames horizontalAnimation = new DoubleAnimationUsingKeyFrames();

                // Add keyframes with smooth transitions - prevents "jumps" in animation
                // Start centered
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

                // Ease to the right
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.25)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Ease to the left
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(-width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.75)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Return to center
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Set to repeat forever
                horizontalAnimation.RepeatBehavior = RepeatBehavior.Forever;

                // Apply the animation
                translateTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnimation);
            }
            catch (Exception ex)
            {
                _logger.Error("Error applying standardized cloud animation", ex);
            }
        }
    }

    /// <summary>
    /// Creates animated moving clouds that pass by in the background
    /// </summary>
    public class EnhancedMovingCloudsElement : AnimatedWeatherElement
    {
        private double _scale;
        private double _offsetX;
        private bool _useGrayerClouds;
        private const double CLOUD_ANIMATION_DURATION = 30.0; // Standard 30 seconds for all cloud types

        public EnhancedMovingCloudsElement(double scale = 1.0, double offsetX = 0, bool useGrayerClouds = true)
        {
            _scale = scale;
            _offsetX = offsetX;
            _useGrayerClouds = useGrayerClouds;
        }

        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create container for all clouds to manage z-order
                Canvas cloudsContainer = new Canvas
                {
                    Width = width,
                    Height = height,
                    ClipToBounds = true,
                    Background = Brushes.Transparent
                };

                // Always use grayer clouds for consistency across all weather types
                bool isDarkCloud = true;

                // Add background cloud 
                var backgroundCloud = new EnhancedCloudElement(
                    offsetX: width * 0.2 + _offsetX,
                    offsetY: height * -0.1,
                    scale: 0.9 * _scale,
                    isDarkCloud: isDarkCloud);

                // Add foreground cloud
                var foregroundCloud = new EnhancedCloudElement(
                    offsetX: width * -0.1 + _offsetX,
                    offsetY: height * 0.05,
                    scale: 0.85 * _scale,
                    isDarkCloud: isDarkCloud);

                // Render clouds
                UIElement cloud1 = backgroundCloud.Render(width, height);
                UIElement cloud2 = foregroundCloud.Render(width, height);

                // Add them to the container
                cloudsContainer.Children.Add(cloud1);
                cloudsContainer.Children.Add(cloud2);

                // Apply the standardized animation to the entire container instead of individual clouds
                ApplyStandardizedCloudAnimation(cloudsContainer, width, height);

                _logger.Trace($"Added enhanced moving clouds with scale {_scale}");
                return cloudsContainer;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating enhanced moving clouds", ex);
                return null;
            }
        }

        private void ApplyStandardizedCloudAnimation(Canvas cloudCanvas, double width, double height)
        {
            try
            {
                // Create a transform for cloud movement
                TranslateTransform translateTransform = new TranslateTransform();
                cloudCanvas.RenderTransform = translateTransform;

                // Create standardized keyframe animation
                DoubleAnimationUsingKeyFrames horizontalAnimation = new DoubleAnimationUsingKeyFrames();

                // Add keyframes with smooth transitions
                // Start centered
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

                // Ease to the right
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.25)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Ease to the left
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(-width * 0.12,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION * 0.75)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Return to center
                horizontalAnimation.KeyFrames.Add(
                    new EasingDoubleKeyFrame(0,
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(CLOUD_ANIMATION_DURATION)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut }));

                // Set to repeat forever
                horizontalAnimation.RepeatBehavior = RepeatBehavior.Forever;

                // Apply the animation
                translateTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnimation);
            }
            catch (Exception ex)
            {
                _logger.Error("Error applying standardized cloud animation", ex);
            }
        }
    }
}