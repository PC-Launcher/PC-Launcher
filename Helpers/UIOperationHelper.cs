using System;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides helper methods for UI operations and error handling.
    /// </summary>
    public static class UIOperationHelper
    {
        /// <summary>
        /// Handles exceptions during UI operations with consistent logging and optional user feedback.
        /// </summary>
        /// <param name="operationName">Name of the UI operation</param>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="showToUser">Whether to show a message box to the user</param>
        public static void HandleUIError(string operationName, Exception ex, bool showToUser = false)
        {
            string message = $"UI Error: {operationName}";
            Logger.LogError(message, ex);

            if (showToUser && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"An error occurred during {operationName}: {ex.Message}",
                        "Operation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Executes an action on the UI thread with error handling.
        /// </summary>
        /// <param name="action">The action to execute on the UI thread</param>
        /// <param name="operationName">Name or description of the UI operation</param>
        /// <param name="logger">The logger to use (optional)</param>
        public static void InvokeOnUIThread(Action action, string operationName = null, ContextLogger logger = null)
        {
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return;

            try
            {
                if (string.IsNullOrEmpty(operationName))
                    operationName = "UI operation";

                logger?.Debug($"Starting UI thread operation: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(action);
                }

                logger?.Debug($"Completed UI thread operation: {operationName}");
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error($"Error in UI thread operation: {operationName}", ex);
                }
                else
                {
                    Logger.LogError($"Error in UI thread operation: {operationName}", ex);
                }
            }
        }

        /// <summary>
        /// Executes an action on the UI thread asynchronously with error handling.
        /// </summary>
        /// <param name="action">The action to execute on the UI thread</param>
        /// <param name="operationName">Name or description of the UI operation</param>
        /// <param name="logger">The logger to use (optional)</param>
        /// <returns>A task representing the async operation</returns>
        public static async Task InvokeOnUIThreadAsync(Action action, string operationName = null, ContextLogger logger = null)
        {
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return;

            try
            {
                if (string.IsNullOrEmpty(operationName))
                    operationName = "async UI operation";

                logger?.Debug($"Starting async UI thread operation: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(action);
                }

                logger?.Debug($"Completed async UI thread operation: {operationName}");
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error($"Error in async UI thread operation: {operationName}", ex);
                }
                else
                {
                    Logger.LogError($"Error in async UI thread operation: {operationName}", ex);
                }
            }
        }

        /// <summary>
        /// Safely executes a UI operation with proper error handling.
        /// </summary>
        /// <param name="action">The UI action to execute</param>
        /// <param name="operationName">Name of the UI operation</param>
        /// <param name="logger">The logger to use (optional)</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool TryInvokeOnUIThread(Action action, string operationName = null, ContextLogger logger = null, bool showErrorToUser = false)
        {
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return false;

            if (string.IsNullOrEmpty(operationName))
                operationName = "UI operation";

            try
            {
                logger?.Debug($"UI Operation Started: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(action);
                }

                logger?.Debug($"UI Operation Completed: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleUIError(operationName, ex, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely executes an asynchronous UI operation with proper error handling.
        /// </summary>
        /// <param name="action">The async UI action to execute</param>
        /// <param name="operationName">Name of the async UI operation</param>
        /// <param name="logger">The logger to use (optional)</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> TryInvokeOnUIThreadAsync(Action action, string operationName = null, ContextLogger logger = null, bool showErrorToUser = false)
        {
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return false;

            if (string.IsNullOrEmpty(operationName))
                operationName = "async UI operation";

            try
            {
                logger?.Debug($"Async UI Operation Started: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(action);
                }

                logger?.Debug($"Async UI Operation Completed: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleUIError(operationName, ex, showErrorToUser);
                return false;
            }
        }
    }
}