
using PC_Launcher.Services.WeatherEngine;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

public class FogElement : IWeatherElement
{
    public UIElement Render(double width, double height)
    {
        var canvas = new Canvas();
        var random = new Random();
        int puffCount = 20;

        // Detect time-based color shift (basic heuristic)
        bool isNight = DateTime.Now.Hour < 6 || DateTime.Now.Hour > 18;
        Color fogColor = isNight
            ? Color.FromArgb(200, 220, 220, 220)  // brighter gray for night
            : Color.FromArgb(200, 160, 160, 160); // darker gray for daytime

        for (int i = 0; i < puffCount; i++)
        {
            double size = 100 + random.NextDouble() * 120;
            double initialX = random.NextDouble() * width;
            double initialY = random.NextDouble() * height * 0.8;

            var ellipse = new Ellipse
            {
                Width = size,
                Height = size * 0.6,
                Opacity = 0.12 + random.NextDouble() * 0.15,
                Fill = new SolidColorBrush(fogColor),
                Effect = new BlurEffect { Radius = 15 + random.NextDouble() * 10 }
            };

            Canvas.SetLeft(ellipse, initialX);
            Canvas.SetTop(ellipse, initialY);

            // Horizontal drift animation
            var xDrift = new DoubleAnimation
            {
                From = initialX - 40,
                To = initialX + 40,
                Duration = TimeSpan.FromSeconds(20 + random.NextDouble() * 10),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Vertical float animation
            var yDrift = new DoubleAnimation
            {
                From = initialY,
                To = initialY + 15 + random.NextDouble() * 10,
                Duration = TimeSpan.FromSeconds(30 + random.NextDouble() * 20),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            ellipse.BeginAnimation(Canvas.LeftProperty, xDrift);
            ellipse.BeginAnimation(Canvas.TopProperty, yDrift);

            canvas.Children.Add(ellipse);
        }

        return canvas;
    }
}
