using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public partial class App : Application
    {
        private readonly ContextLogger _logger = Logger.GetLogger<App>();

        public App()
        {
            // Set up global exception handling.
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Clear existing listeners to avoid conflicts
            Debug.Listeners.Clear();

            // Add back the default trace listener for Visual Studio output window
            Debug.Listeners.Add(new DefaultTraceListener());

            // Add our custom fixed trace listener
            Debug.Listeners.Add(new LoggerTraceListener());

            // Log startup information.
            _logger.Info("Application starting...");
            _logger.Info($"Application Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");

            try
            {
                // Since config.ini is in the root, use that path.
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                _logger.Info($"config.ini exists: {File.Exists(configPath)}");

                string nircmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nircmd.dll");
                _logger.Info($"nircmd.dll exists: {File.Exists(nircmdPath)}");

                string reactivePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System.Reactive.dll");
                _logger.Info($"System.Reactive.dll exists: {File.Exists(reactivePath)}");

                string newtonsoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Newtonsoft.Json.dll");
                _logger.Info($"Newtonsoft.Json.dll exists: {File.Exists(newtonsoftPath)}");

                string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                _logger.Info($"Images directory exists: {Directory.Exists(imagesDir)}");

                string soundsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");
                _logger.Info($"Sounds directory exists: {Directory.Exists(soundsDir)}");

                _logger.Info($"CLR Version: {Environment.Version}");
                _logger.Info($"OS Version: {Environment.OSVersion}");
                _logger.Info($"Is 64-bit process: {Environment.Is64BitProcess}");
                _logger.Info($"Is 64-bit OS: {Environment.Is64BitOperatingSystem}");
            }
            catch (Exception ex)
            {
                _logger.Error("Error during startup logging", ex);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = $"Unhandled Exception: {e.Exception.Message}\n{e.Exception.StackTrace}";
            _logger.Error(errorMessage, e.Exception);

            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe error has been logged.",
                            "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            string errorMessage = ex != null
                ? $"Fatal Exception: {ex.Message}\n{ex.StackTrace}"
                : "A fatal error occurred, but the exception object could not be retrieved.";

            _logger.Error(errorMessage, ex);

            try
            {
                MessageBox.Show($"A fatal error has occurred:\n\n{(ex != null ? ex.Message : "Unknown error")}\n\nThe application will now terminate.",
                                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
}