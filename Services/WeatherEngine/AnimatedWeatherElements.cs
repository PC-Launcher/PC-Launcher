using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows;

namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Base class for animated weather elements
    /// </summary>
    public abstract class AnimatedWeatherElement : BaseWeatherElement
    {
        /// <summary>
        /// Applies an opacity pulsing animation
        /// </summary>
        protected void ApplyPulseAnimation(UIElement element, double minOpacity = 0.4,
                                         double maxOpacity = 1.0, double durationSeconds = 2.0)
        {
            ApplyAnimation(element, UIElement.OpacityProperty, maxOpacity, minOpacity,
                TimeSpan.FromSeconds(durationSeconds), true);
        }

        /// <summary>
        /// Applies a floating animation (vertical movement)
        /// </summary>
        protected void ApplyFloatAnimation(UIElement element, double transformOffsetY = 0,
                                         double amplitude = 5, double durationSeconds = 3.0)
        {
            try
            {
                var transform = new TranslateTransform(0, transformOffsetY);
                element.RenderTransform = transform;

                ApplyAnimation(element, TranslateTransform.YProperty, transformOffsetY,
                    transformOffsetY + amplitude, TimeSpan.FromSeconds(durationSeconds), true);
            }
            catch (Exception ex)
            {
                _logger.Error("Error applying float animation", ex);
            }
        }

        /// <summary>
        /// Applies a drifting animation (horizontal movement)
        /// </summary>
        protected void ApplyDriftAnimation(UIElement element, double transformOffsetX = 0,
                                         double amplitude = 10, double durationSeconds = 5.0)
        {
            try
            {
                var transform = new TranslateTransform(transformOffsetX, 0);
                element.RenderTransform = transform;

                ApplyAnimation(element, TranslateTransform.XProperty, transformOffsetX,
                    transformOffsetX + amplitude, TimeSpan.FromSeconds(durationSeconds), true);
            }
            catch (Exception ex)
            {
                _logger.Error("Error applying drift animation", ex);
            }
        }
    }
}