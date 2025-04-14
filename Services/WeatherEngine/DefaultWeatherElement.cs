using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Renders a default weather icon for unknown weather codes
    /// </summary>
    public class DefaultWeatherElement : BaseWeatherElement
    {
        public override UIElement Render(double width, double height)
        {
            try
            {
                // Create a canvas for the default icon
                Canvas defaultCanvas = new Canvas
                {
                    Width = width,
                    Height = height
                };

                // Create a question mark for unknown weather
                TextBlock questionMark = new TextBlock
                {
                    Text = "?",
                    FontSize = Math.Min(width, height) * 0.6,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Position the text in the center
                Canvas.SetLeft(questionMark, width / 2 - questionMark.FontSize / 3);
                Canvas.SetTop(questionMark, height / 2 - questionMark.FontSize / 2);
                defaultCanvas.Children.Add(questionMark);

                // Add a subtle circle background
                Ellipse background = new Ellipse
                {
                    Width = questionMark.FontSize * 1.5,
                    Height = questionMark.FontSize * 1.5,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 200, 200, 200)),
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                    StrokeThickness = 1
                };

                Canvas.SetLeft(background, width / 2 - background.Width / 2);
                Canvas.SetTop(background, height / 2 - background.Height / 2);
                defaultCanvas.Children.Insert(0, background);

                _logger.Info("Added default weather icon (question mark)");
                return defaultCanvas;
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating default weather icon", ex);
                return null;
            }
        }
    }
}
