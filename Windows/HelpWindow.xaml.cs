using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher;

namespace PCStreamerLauncher.Windows
{
    /// <summary>
    /// Interaction logic for HelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window
    {
        private readonly ContextLogger _logger = Logger.GetLogger<HelpWindow>();
        private Storyboard _animationStoryboard;
        private SoundManager _soundManager;
        private GamepadHandler _gamepadHandler;
        private EventHandler<GamepadEventArgs> _startButtonHandler;
        private DateTime _openTime;
        private const int InitialDelayMs = 1000; // Ignore Start button presses for this many ms after opening

        // We'll use a custom event to avoid hiding the Window.Closed event
        public event EventHandler HelpWindowClosed;

        // This constructor is now overloaded to accept a GamepadHandler for gamepad support
        public HelpWindow(SoundManager soundManager, GamepadHandler gamepadHandler = null)
        {
            InitializeComponent();
            _soundManager = soundManager;
            _logger.Info("HelpWindow initialized");

            // Create and set up animations
            SetupBorderAnimations();

            // Set up gamepad handling if provided
            if (gamepadHandler != null)
            {
                _logger.Info("Setting up gamepad support for help window");
                _gamepadHandler = gamepadHandler;
                _openTime = DateTime.Now; // Record when the window was opened
                
                _startButtonHandler = (s, e) => {
                    // Ignore Start button presses that occur too soon after opening
                    if ((DateTime.Now - _openTime).TotalMilliseconds < InitialDelayMs)
                    {
                        _logger.Info($"Start button press ignored - too soon after opening ({(DateTime.Now - _openTime).TotalMilliseconds}ms < {InitialDelayMs}ms)");
                        return;
                    }
                    
                    _logger.Info("Start button pressed - closing help window");
                    CloseWindow();
                };
                _gamepadHandler.StartButtonPressed += _startButtonHandler;
            }

            // Center window on screen
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // Add fade-in animation when loaded
            this.Loaded += (s, e) =>
            {
                _logger.Info("HelpWindow loaded, starting animations");
                // Start with 0 opacity
                this.Opacity = 0;
                
                // Animate fade-in
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                _animationStoryboard.Begin(this);
            };
        }

        private void SetupBorderAnimations()
        {
            try
            {
                // Find the border element
                var border = this.Content as Border;
                if (border == null)
                {
                    _logger.Error("Failed to find Border in HelpWindow");
                    return;
                }

                // Create storyboard for the animation
                _animationStoryboard = new Storyboard();
                _animationStoryboard.RepeatBehavior = RepeatBehavior.Forever;
                _animationStoryboard.AutoReverse = true;

                // Border color animation
                var borderBrush = border.BorderBrush as SolidColorBrush;
                if (borderBrush != null)
                {
                    var colorAnimation = new ColorAnimation
                    {
                        From = (Color)ColorConverter.ConvertFromString("#FF00AAFF"), // DeepSkyBlue
                        To = (Color)ColorConverter.ConvertFromString("#FF00FFFF"),   // Cyan
                        Duration = new Duration(TimeSpan.FromSeconds(1.0))
                    };
                    Storyboard.SetTarget(colorAnimation, border);
                    Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
                    _animationStoryboard.Children.Add(colorAnimation);
                }

                // Shadow effect animation
                var effect = border.Effect as System.Windows.Media.Effects.DropShadowEffect;
                if (effect != null)
                {
                    var blurAnimation = new DoubleAnimation
                    {
                        From = 30,
                        To = 60,
                        Duration = new Duration(TimeSpan.FromSeconds(1.0))
                    };
                    Storyboard.SetTarget(blurAnimation, effect);
                    Storyboard.SetTargetProperty(blurAnimation, new PropertyPath("BlurRadius"));
                    _animationStoryboard.Children.Add(blurAnimation);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error setting up border animations", ex);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control))
            {
                _logger.Info($"Closing HelpWindow due to key press: {e.Key}");
                CloseWindow();
                e.Handled = true;
            }
        }

        public void CloseWindow()
        {
            try
            {
                _logger.Info("Closing HelpWindow with animation");
                _soundManager?.PlaySound("Navigate");
                
                // Stop the border animation
                _animationStoryboard?.Stop(this);
                
                // Create fade-out animation
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                
                // When animation completes, close the window
                fadeOut.Completed += (s, e) => 
                {
                    try
                    {
                        // Raise closed event before actually closing
                        HelpWindowClosed?.Invoke(this, EventArgs.Empty);
                        
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error closing window after animation", ex);
                    }
                };
                
                // Start the animation
                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                _logger.Error("Error in CloseWindow", ex);
                
                // Fallback to direct close if animation fails
                try
                {
                HelpWindowClosed?.Invoke(this, EventArgs.Empty);
                this.Close();
                }
                catch
                {
                    // Last resort
                    _logger.Error("Failed to close window even with fallback method");
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _logger.Info("HelpWindow.OnClosed called");
                
                // Cleanup animation resources
                if (_animationStoryboard != null)
                {
                    _animationStoryboard.Stop(this);
                    
                    foreach (var animation in _animationStoryboard.Children)
                    {
                        if (animation is AnimationTimeline timeline)
                        {
                            // Get target and clear the animation
                            var target = Storyboard.GetTarget(timeline);
                            var targetProperty = Storyboard.GetTargetProperty(timeline);
                            
                            if (target != null && targetProperty != null)
                            {
                                // Find a way to cleanup property path animation
                                if (target is DependencyObject dependencyObject)
                                {
                                    // Attempt to clear animation on the property path
                                    try
                                    {
                                        if (timeline is DoubleAnimation)
                                        {
                                            AnimationHelper.ClearDoubleAnimation(dependencyObject, targetProperty.Path);
                                        }
                                        else if (timeline is ColorAnimation)
                                        {
                                            AnimationHelper.ClearColorAnimation(dependencyObject, targetProperty.Path);
                                        }
                                    }
                                    catch (Exception animEx)
                                    {
                                        _logger.Error($"Error clearing animation on {targetProperty.Path}", animEx);
                                    }
                                }
                            }
                        }
                    }
                    
                    _animationStoryboard.Children.Clear();
                    _animationStoryboard = null;
                }
                
                // Clear event handlers
                HelpWindowClosed = null;
                
                // Clean up gamepad handler
                if (_gamepadHandler != null && _startButtonHandler != null)
                {
                    _gamepadHandler.StartButtonPressed -= _startButtonHandler;
                    _startButtonHandler = null;
                    _gamepadHandler = null;
                }
                
                // Clear sound manager reference
                _soundManager = null;
            }
            catch (Exception ex)
            {
                _logger.Error("Error in HelpWindow.OnClosed", ex);
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }

    /// <summary>
    /// Helper class for animation cleanup
    /// </summary>
    public static class AnimationHelper
    {
        public static void ClearDoubleAnimation(DependencyObject target, string propertyPath)
        {
            if (target is UIElement element)
            {
                if (propertyPath == "Opacity")
                {
                    element.BeginAnimation(UIElement.OpacityProperty, null);
                }
            }
            else if (target is System.Windows.Media.Effects.DropShadowEffect effect)
            {
                if (propertyPath == "BlurRadius")
                {
                    effect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, null);
                }
            }
        }
        
        public static void ClearColorAnimation(DependencyObject target, string propertyPath)
        {
            if (target is Border border && propertyPath.Contains("BorderBrush"))
            {
                if (border.BorderBrush is SolidColorBrush brush)
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                }
            }
        }
    }
}
