using PCStreamerLauncher.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows;


namespace PC_Launcher.Services.WeatherEngine
{
    /// <summary>
    /// Base class for all weather elements providing common functionality
    /// </summary>
    public abstract class BaseWeatherElement : IWeatherElement
    {
        // Logger for this element
        protected readonly ContextLogger _logger;

        protected BaseWeatherElement()
        {
            _logger = Logger.GetLogger<BaseWeatherElement>();


        }

        /// <summary>
        /// Renders the weather element
        /// </summary>
        public abstract UIElement Render(double width, double height);

        /// <summary>
        /// Applies a double animation to a UI element
        /// </summary>
        /// <param name="element">Element to animate</param>
        /// <param name="property">Property to animate</param>
        /// <param name="from">Start value</param>
        /// <param name="to">End value</param>
        /// <param name="duration">Duration of animation</param>
        /// <param name="autoReverse">Whether animation should auto-reverse</param>
        /// <param name="repeat">Repeat behavior (defaults to Forever)</param>
        protected void ApplyAnimation(UIElement element, DependencyProperty property,
                                     double from, double to, TimeSpan duration,
                                     bool autoReverse = false,
                                     RepeatBehavior? repeat = null)
        {
            try
            {
                var animation = new DoubleAnimation
                {
                    From = from,
                    To = to,
                    Duration = duration,
                    AutoReverse = autoReverse,
                    RepeatBehavior = repeat ?? RepeatBehavior.Forever
                };

                element.BeginAnimation(property, animation);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error applying animation to {property.Name}", ex);
            }
        }

        /// <summary>
        /// Applies an animation with a delay
        /// </summary>
        protected void ApplyAnimationWithDelay(UIElement element, DependencyProperty property,
                                              double from, double to, TimeSpan duration,
                                              TimeSpan delay, bool autoReverse = false,
                                              RepeatBehavior? repeat = null)
        {
            try
            {
                var animation = new DoubleAnimation
                {
                    From = from,
                    To = to,
                    Duration = duration,
                    BeginTime = delay,
                    AutoReverse = autoReverse,
                    RepeatBehavior = repeat ?? RepeatBehavior.Forever
                };

                element.BeginAnimation(property, animation);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error applying animation with delay to {property.Name}", ex);
            }
        }

        /// <summary>
        /// Gets a random value between min and max
        /// </summary>
        protected static double GetRandomValue(double min, double max)
        {
            return min + (new Random().NextDouble() * (max - min));
        }
    }
}