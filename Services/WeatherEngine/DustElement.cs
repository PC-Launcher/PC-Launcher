
using PC_Launcher.Services.WeatherEngine;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

public class DustElement : IWeatherElement
{
    public UIElement Render(double width, double height)
    {
        var canvas = new Canvas();
        var random = new Random();
        int wispCount = 35;

        // Night-aware coloring
        bool isNight = DateTime.Now.Hour < 6 || DateTime.Now.Hour > 18;
        Color innerColor = isNight
            ? Color.FromArgb(180, 200, 170, 120) // Slightly brighter and more opaque
            : Color.FromArgb(90, 165, 132, 90);  // Dustier tan during day

        for (int i = 0; i < wispCount; i++)
        {
            double wispWidth = 120 + random.NextDouble() * 180;
            double wispHeight = 10 + random.NextDouble() * 12;
            double initialX = random.NextDouble() * width;
            double initialY = random.NextDouble() * height;

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, innerColor.R, innerColor.G, innerColor.B), 0.0),
                    new GradientStop(innerColor, 0.5),
                    new GradientStop(Color.FromArgb(0, innerColor.R, innerColor.G, innerColor.B), 1.0)
                }
            };

            var wisp = new Rectangle
            {
                Width = wispWidth,
                Height = wispHeight,
                Fill = gradientBrush,
                Opacity = (isNight ? 0.3 : 0.15) + random.NextDouble() * 0.1,
                Effect = new BlurEffect { Radius = 6 + random.NextDouble() * 4 },
                RenderTransform = new RotateTransform(random.NextDouble() * 40 - 20)
            };

            Canvas.SetLeft(wisp, initialX);
            Canvas.SetTop(wisp, initialY);

            var drift = new DoubleAnimation
            {
                From = initialX - 50,
                To = initialX + 50,
                Duration = TimeSpan.FromSeconds(18 + random.NextDouble() * 10),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var verticalDrift = new DoubleAnimation
            {
                From = initialY,
                To = initialY + 8 + random.NextDouble() * 4,
                Duration = TimeSpan.FromSeconds(22 + random.NextDouble() * 15),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            wisp.BeginAnimation(Canvas.LeftProperty, drift);
            wisp.BeginAnimation(Canvas.TopProperty, verticalDrift);

            canvas.Children.Add(wisp);
        }

        return canvas;
    }
}
