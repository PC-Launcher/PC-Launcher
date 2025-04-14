using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Handles focus transitions between the launcher window, launched applications, and the operating system.
    /// </summary>
    public static class WindowFocusHelper
    {
        private static readonly ContextLogger _logger = Logger.GetLogger("WindowFocusHelper");
        private static IntPtr _lastFocusedWindowHandle = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);

        /// <summary>
        /// Sets focus to the launcher window.
        /// </summary>
        public static async Task FocusLauncherWindowAsync(Window launcherWindow)
        {
            if (launcherWindow == null)
                return;

            _logger.Info("Setting focus to launcher window");

            await launcherWindow.Dispatcher.InvokeAsync(() =>
            {
                // First capture current focused window for possible later restoration
                _lastFocusedWindowHandle = GetForegroundWindow();

                // Show and activate the window
                if (!launcherWindow.IsVisible)
                    launcherWindow.Show();

                if (launcherWindow.WindowState == WindowState.Minimized)
                    launcherWindow.WindowState = WindowState.Normal;

                // Multiple focus methods for reliability
                launcherWindow.Topmost = true;
                launcherWindow.Activate();
                launcherWindow.Focus();

                // Get and use the window handle
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(launcherWindow).Handle;
                SetForegroundWindow(hwnd);

                // Reset topmost after focus
                Task.Delay(100).ContinueWith(_ =>
                {
                    try
                    {
                        launcherWindow.Dispatcher.Invoke(() =>
                        {
                            if (launcherWindow != null)
                                launcherWindow.Topmost = false;
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error resetting Topmost property", ex);
                    }
                });
            });
        }

        /// <summary>
        /// Capture current foreground window before showing or hiding windows
        /// </summary>
        public static void CaptureCurrentFocus()
        {
            _lastFocusedWindowHandle = GetForegroundWindow();
            _logger.Info($"Captured focus of window handle: {_lastFocusedWindowHandle}");
        }

        /// <summary>
        /// Sets focus to a launched application process
        /// </summary>
        public static async Task FocusAppProcessAsync(Process process, int attempts = 5)
        {
            if (process == null || process.HasExited)
                return;

            _logger.Info($"Setting focus to process {process.ProcessName} (ID: {process.Id})");

            // Try multiple times with increasing delays
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    // Refresh process info to get window handle
                    process.Refresh();
                    IntPtr hwnd = process.MainWindowHandle;

                    if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                    {
                        bool success = SetForegroundWindow(hwnd);
                        _logger.Info($"Focus set to process window: {success}");

                        if (success)
                            return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error focusing process window on attempt {i + 1}", ex);
                }

                // Wait before retry with increasing delay
                await Task.Delay(100 * (i + 1));
            }

            _logger.Warning($"Failed to set focus to process after {attempts} attempts");
        }

        /// <summary>
        /// Sets focus back to the OS desktop or shell
        /// </summary>
        public static bool FocusDesktop()
        {
            _logger.Info("Setting focus to desktop/OS");

            // First try restoring to previously captured window
            if (_lastFocusedWindowHandle != IntPtr.Zero && IsWindowVisible(_lastFocusedWindowHandle))
            {
                bool success = SetForegroundWindow(_lastFocusedWindowHandle);
                _logger.Info($"Focus restored to previous window: {success}");
                if (success) return true;
            }

            // Try shell window
            IntPtr shellHwnd = FindWindow("Shell_TrayWnd", null);
            if (shellHwnd != IntPtr.Zero)
            {
                bool success = SetForegroundWindow(shellHwnd);
                _logger.Info($"Focus set to shell window: {success}");
                if (success) return true;
            }

            // Try desktop
            IntPtr desktopHwnd = FindWindow("Progman", null);
            if (desktopHwnd != IntPtr.Zero)
            {
                bool success = SetForegroundWindow(desktopHwnd);
                _logger.Info($"Focus set to desktop window: {success}");
                if (success) return true;
            }

            // Try Explorer as last resort
            try
            {
                var explorerProcs = Process.GetProcessesByName("explorer");
                foreach (var explorer in explorerProcs)
                {
                    if (explorer.MainWindowHandle != IntPtr.Zero)
                    {
                        bool success = SetForegroundWindow(explorer.MainWindowHandle);
                        if (success)
                        {
                            _logger.Info("Focus set to explorer window");
                            foreach (var proc in explorerProcs) proc.Dispose();
                            return true;
                        }
                    }
                    explorer.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error focusing explorer", ex);
            }

            return false;
        }
    }
}
