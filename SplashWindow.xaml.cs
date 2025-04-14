using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public partial class SplashWindow : Window
    {
        private SoundManager _soundManager;
        private readonly ContextLogger _logger = Logger.GetLogger<SplashWindow>();

        public SplashWindow()
        {
            try
            {
                _logger.Info("Initializing splash screen");
                InitializeComponent();
                Loaded += SplashWindow_Loaded;

                // Log some startup diagnostics
                _logger.Info("Constructor executed");
                _logger.Info($"Application Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");

                // Check for critical files
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
                if (File.Exists(configPath))
                {
                    _logger.Info("Config.ini found");
                }
                else
                {
                    _logger.Warning("WARNING - Config.ini not found!");
                    MessageBox.Show("Configuration file (Config.ini) not found. The application may not function correctly.",
                                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                string nircmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nircmd.dll");
                if (File.Exists(nircmdPath))
                {
                    _logger.Info("nircmd.dll found");
                }
                else
                {
                    _logger.Warning("WARNING - nircmd.dll not found!");
                    MessageBox.Show("Required library (nircmd.dll) not found. The application may not function correctly.",
                                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in constructor", ex);
                MessageBox.Show($"Error in SplashWindow constructor: {ex.Message}\n\n{ex.StackTrace}",
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.Info("SplashWindow_Loaded event triggered");

                // Initialize sound manager and play startup sound
                string soundsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");
                _logger.Info($"Initializing sound manager with path: {soundsPath}");

                _soundManager = new SoundManager(soundsPath);

                // Check if Startup.wav exists and log before playing
                string startupSoundPath = Path.Combine(soundsPath, "Startup.wav");
                if (File.Exists(startupSoundPath))
                {
                    _logger.Info("Startup.wav found, playing startup sound");
                    _soundManager.PlaySound("Startup");
                }
                else
                {
                    _logger.Warning($"Startup.wav not found at {startupSoundPath}");
                }

                // Wait 4.5 seconds to allow the neon animation and initialization to complete.
                _logger.Info("Starting delay timer for 4.5 seconds");
                await Task.Delay(4500);
                _logger.Info("Delay timer completed");

                // Create the main window
                _logger.Info("Creating MainWindow instance");
                MainWindow mainWindow = new MainWindow();

                // Show the main window
                _logger.Info("Showing MainWindow");
                mainWindow.Show();
                _logger.Info("MainWindow.Show() completed");

                // Dispose sound manager before closing
                if (_soundManager != null)
                {
                    _logger.Info("Disposing sound manager");
                    _soundManager.Dispose();
                    _soundManager = null;
                }

                // Close the splash screen
                _logger.Info("Closing splash screen");
                this.Close();
                _logger.Info("Splash screen closed");
            }
            catch (Exception ex)
            {
                _logger.Error("ERROR in SplashWindow_Loaded", ex);

                // Show a message box with the error details
                MessageBox.Show($"Error during startup: {ex.Message}\n\n{ex.StackTrace}",
                               "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Clean up sound manager if it was created
                if (_soundManager != null)
                {
                    try
                    {
                        _soundManager.Dispose();
                        _soundManager = null;
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.Error("Error disposing sound manager", disposeEx);
                    }
                }

                // In case of error, try to ensure the splash screen is closed
                try
                {
                    _logger.Info("Attempting to close after error");
                    this.Close();
                }
                catch (Exception closeEx)
                {
                    _logger.Error("Error closing splash screen", closeEx);
                }
            }
        }
    }
}