using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;
using System.Threading;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Provides helper methods for standardized error handling throughout the application.
    /// Rather than replacing existing try/catch blocks, this class supplements them with
    /// consistent patterns.
    /// </summary>
    public static class ErrorHelper
    {
        /// <summary>
        /// Handles exceptions from component initialization with consistent logging and user feedback.
        /// </summary>
        /// <param name="componentName">Name of the component being initialized</param>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="isCritical">Whether this is a critical component required for the application to function</param>
        public static void HandleInitializationError(string componentName, Exception ex, bool isCritical = false)
        {
            string message = $"Error initializing {componentName}";
            Logger.LogError(message, ex);

            if (isCritical && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"A critical error occurred while initializing {componentName}: {ex.Message}\n\nThe application may not function correctly.",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Handles exceptions during resource disposal with consistent logging.
        /// </summary>
        /// <param name="componentName">Name of the component being disposed</param>
        /// <param name="resourceName">Name of the specific resource being disposed</param>
        /// <param name="ex">The exception that occurred</param>
        public static void HandleDisposalError(string componentName, string resourceName, Exception ex)
        {
            Logger.LogError($"{componentName}: Error disposing {resourceName}", ex);
        }

        /// <summary>
        /// Executes a resource cleanup action with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component being cleaned up</param>
        /// <param name="resourceName">Name of the specific resource being cleaned up</param>
        /// <param name="cleanupAction">The cleanup action to execute</param>
        public static void SafelyCleanupResource(string componentName, string resourceName, Action cleanupAction)
        {
            try
            {
                Logger.LogInfo($"{componentName}: Beginning cleanup of {resourceName}");
                cleanupAction();
                Logger.LogInfo($"{componentName}: Successfully cleaned up {resourceName}");
            }
            catch (Exception ex)
            {
                HandleDisposalError(componentName, resourceName, ex);
            }
        }

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
        /// Safely executes a UI operation with proper error handling.
        /// </summary>
        /// <param name="operationName">Name of the UI operation</param>
        /// <param name="uiAction">The UI action to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool SafelyExecuteUIOperation(string operationName, Action uiAction, bool showErrorToUser = false)
        {
            if (Application.Current == null) return false;

            try
            {
                Logger.LogDebug($"UI Operation Started: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    uiAction();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(uiAction);
                }

                Logger.LogDebug($"UI Operation Completed: {operationName}");
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
        /// <param name="operationName">Name of the async UI operation</param>
        /// <param name="uiAsyncAction">The async UI action to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteUIOperationAsync(string operationName, Func<Task> uiAsyncAction, bool showErrorToUser = false)
        {
            if (Application.Current == null) return false;

            try
            {
                Logger.LogDebug($"Async UI Operation Started: {operationName}");

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    await uiAsyncAction();
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () => await uiAsyncAction());
                }

                Logger.LogDebug($"Async UI Operation Completed: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleUIError(operationName, ex, showErrorToUser);
                return false;
            }
        }
        /// <summary>
        /// Handles exceptions related to external process operations.
        /// </summary>
        /// <param name="processOperation">Description of the process operation</param>
        /// <param name="processName">Name of the process</param>
        /// <param name="processId">ID of the process, if available</param>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="showToUser">Whether to show a message to the user</param>
        public static void HandleProcessError(string processOperation, string processName, int? processId, Exception ex, bool showToUser = false)
        {
            string processIdentifier = processId.HasValue
                ? $"{processName} (ID: {processId})"
                : processName;

            string message = $"Process Error: {processOperation} failed for {processIdentifier}";
            Logger.LogError(message, ex);

            if (showToUser && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"An error occurred while {processOperation} {processName}: {ex.Message}",
                        "Process Operation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Safely terminates a process with proper error handling.
        /// </summary>
        /// <param name="process">The Process object to terminate</param>
        /// <param name="timeoutMs">Timeout in milliseconds to wait for the process to exit</param>
        /// <returns>True if the process was successfully terminated, false otherwise</returns>
        public static bool SafelyTerminateProcess(System.Diagnostics.Process process, int timeoutMs = 3000)
        {
            if (process == null) return false;

            string processName = "Unknown";
            int processId = -1;

            try
            {
                processName = process.ProcessName;
                processId = process.Id;

                Logger.LogInfo($"Attempting to terminate process {processName} (ID: {processId})");

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(timeoutMs);

                    if (process.HasExited)
                    {
                        Logger.LogInfo($"Successfully terminated process {processName} (ID: {processId})");
                        return true;
                    }
                    else
                    {
                        Logger.LogWarning($"Process {processName} (ID: {processId}) did not exit within timeout period");
                        return false;
                    }
                }
                else
                {
                    Logger.LogInfo($"Process {processName} (ID: {processId}) has already exited");
                    return true;
                }
            }
            catch (Exception ex)
            {
                HandleProcessError("terminating", processName, processId, ex);

                // Try fallback using taskkill
                return FallbackProcessTermination(processName, processId);
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing process object for {processName} (ID: {processId})", ex);
                }
            }
        }

        /// <summary>
        /// Fallback method to terminate a process using taskkill when normal termination fails.
        /// </summary>
        /// <param name="processName">Name of the process to terminate</param>
        /// <param name="processId">ID of the process, if available</param>
        /// <returns>True if the fallback method was executed without exceptions, false otherwise</returns>
        private static bool FallbackProcessTermination(string processName, int processId)
        {
            try
            {
                Logger.LogInfo($"Attempting fallback termination for {processName} (ID: {processId})");

                if (processId > 0)
                {
                    // Try to kill by PID first
                    ExecuteTaskKill($"/F /PID {processId}");
                }

                // Also try by name if we have it
                if (!string.IsNullOrEmpty(processName))
                {
                    ExecuteTaskKill($"/F /IM \"{processName}.exe\"");
                }

                Logger.LogInfo($"Fallback termination completed for {processName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallback process termination also failed for {processName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Helper method to execute the taskkill command.
        /// </summary>
        /// <param name="arguments">Arguments for taskkill</param>
        private static void ExecuteTaskKill(string arguments)
        {
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                process.Start();
                process.WaitForExit(3000);
            }
        }

        /// <summary>
        /// Handles exceptions related to file operations.
        /// </summary>
        /// <param name="fileOperation">Description of the file operation</param>
        /// <param name="filePath">Path of the file</param>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="showToUser">Whether to show a message to the user</param>
        public static void HandleFileError(string fileOperation, string filePath, Exception ex, bool showToUser = false)
        {
            string message = $"File Error: {fileOperation} failed for {filePath}";
            Logger.LogError(message, ex);

            if (showToUser && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"An error occurred while {fileOperation} file {System.IO.Path.GetFileName(filePath)}: {ex.Message}",
                        "File Operation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Safely performs a file operation with proper error handling.
        /// </summary>
        /// <param name="fileOperation">Description of the file operation</param>
        /// <param name="filePath">Path of the file</param>
        /// <param name="fileAction">The file operation to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool SafelyExecuteFileOperation(string fileOperation, string filePath, Action fileAction, bool showErrorToUser = false)
        {
            try
            {
                Logger.LogDebug($"File Operation Started: {fileOperation} on {filePath}");
                fileAction();
                Logger.LogDebug($"File Operation Completed: {fileOperation} on {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                HandleFileError(fileOperation, filePath, ex, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async file operation with proper error handling.
        /// </summary>
        /// <param name="fileOperation">Description of the file operation</param>
        /// <param name="filePath">Path of the file</param>
        /// <param name="fileAsyncAction">The async file operation to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteFileOperationAsync(string fileOperation, string filePath, Func<Task> fileAsyncAction, bool showErrorToUser = false)
        {
            try
            {
                Logger.LogDebug($"Async File Operation Started: {fileOperation} on {filePath}");
                await fileAsyncAction();
                Logger.LogDebug($"Async File Operation Completed: {fileOperation} on {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                HandleFileError(fileOperation, filePath, ex, showErrorToUser);
                return false;
            }
        }
        /// <summary>
        /// Handles exceptions related to network operations.
        /// </summary>
        /// <param name="networkOperation">Description of the network operation</param>
        /// <param name="url">URL or network resource identifier</param>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="showToUser">Whether to show a message to the user</param>
        public static void HandleNetworkError(string networkOperation, string url, Exception ex, bool showToUser = false)
        {
            string message = $"Network Error: {networkOperation} failed for {url}";
            Logger.LogError(message, ex);

            if (showToUser && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"A network error occurred while {networkOperation}: {ex.Message}\n\nPlease check your internet connection.",
                        "Network Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }

        /// <summary>
        /// Safely performs a network operation with proper error handling.
        /// </summary>
        /// <param name="networkOperation">Description of the network operation</param>
        /// <param name="url">URL or network resource identifier</param>
        /// <param name="networkAction">The network operation to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool SafelyExecuteNetworkOperation(string networkOperation, string url, Action networkAction, bool showErrorToUser = false)
        {
            try
            {
                Logger.LogDebug($"Network Operation Started: {networkOperation} to {url}");
                networkAction();
                Logger.LogDebug($"Network Operation Completed: {networkOperation} to {url}");
                return true;
            }
            catch (Exception ex)
            {
                HandleNetworkError(networkOperation, url, ex, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async network operation with proper error handling.
        /// </summary>
        /// <param name="networkOperation">Description of the network operation</param>
        /// <param name="url">URL or network resource identifier</param>
        /// <param name="networkAsyncAction">The async network operation to execute</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteNetworkOperationAsync(string networkOperation, string url, Func<Task> networkAsyncAction, bool showErrorToUser = false)
        {
            try
            {
                Logger.LogDebug($"Async Network Operation Started: {networkOperation} to {url}");
                await networkAsyncAction();
                Logger.LogDebug($"Async Network Operation Completed: {networkOperation} to {url}");
                return true;
            }
            catch (Exception ex)
            {
                HandleNetworkError(networkOperation, url, ex, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Handles exceptions during state transitions in components that implement state machines.
        /// </summary>
        /// <param name="componentName">Name of the component</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="ex">The exception that occurred</param>
        public static void HandleStateTransitionError(string componentName, string fromState, string toState, Exception ex)
        {
            string message = $"State Transition Error: {componentName} failed to transition from {fromState} to {toState}";
            Logger.LogError(message, ex);
        }

        /// <summary>
        /// Safely performs a state transition with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="transitionAction">The state transition action to execute</param>
        /// <returns>True if the transition completed successfully, false otherwise</returns>
        public static bool SafelyExecuteStateTransition(string componentName, string fromState, string toState, Action transitionAction)
        {
            try
            {
                Logger.LogDebug($"State Transition Started: {componentName} from {fromState} to {toState}");
                transitionAction();
                Logger.LogDebug($"State Transition Completed: {componentName} from {fromState} to {toState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, fromState, toState, ex);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async state transition with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="transitionAsyncAction">The async state transition action to execute</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteStateTransitionAsync(string componentName, string fromState, string toState, Func<Task> transitionAsyncAction)
        {
            try
            {
                Logger.LogDebug($"Async State Transition Started: {componentName} from {fromState} to {toState}");
                await transitionAsyncAction();
                Logger.LogDebug($"Async State Transition Completed: {componentName} from {fromState} to {toState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, fromState, toState, ex);
                return false;
            }
        }
        /// <summary>
        /// Handles exceptions during event handling.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="ex">The exception that occurred</param>
        public static void HandleEventError(string componentName, string eventName, Exception ex)
        {
            string message = $"Event Handling Error: {componentName} failed to handle {eventName} event";
            Logger.LogError(message, ex);
        }

        /// <summary>
        /// Safely handles an event with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="eventHandler">The event handler to execute</param>
        /// <returns>True if the event was handled successfully, false otherwise</returns>
        public static bool SafelyHandleEvent(string componentName, string eventName, Action eventHandler)
        {
            try
            {
                Logger.LogTrace($"Event Handling Started: {componentName} handling {eventName}");
                eventHandler();
                Logger.LogTrace($"Event Handling Completed: {componentName} handling {eventName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex);
                return false;
            }
        }

        /// <summary>
        /// Safely handles an async event with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="eventHandlerAsync">The async event handler to execute</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyHandleEventAsync(string componentName, string eventName, Func<Task> eventHandlerAsync)
        {
            try
            {
                Logger.LogTrace($"Async Event Handling Started: {componentName} handling {eventName}");
                await eventHandlerAsync();
                Logger.LogTrace($"Async Event Handling Completed: {componentName} handling {eventName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex);
                return false;
            }
        }
        // Additional methods to be added to the existing ErrorHelper class

        /// <summary>
        /// Executes an operation with consistent logging and error handling.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool ExecuteWithLogging(
            string operationName,
            Action operation,
            ContextLogger logger = null)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                logger.Debug($"Operation Started: {operationName}");
                operation();
                logger.Debug($"Operation Completed: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Operation Failed: {operationName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Executes an operation with consistent logging and error handling, returning a result.
        /// </summary>
        /// <typeparam name="T">The return type of the operation</typeparam>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <param name="defaultValue">Default value to return if operation fails</param>
        /// <returns>The result of the operation or the default value if an exception occurs</returns>
        public static T ExecuteWithLogging<T>(
            string operationName,
            Func<T> operation,
            ContextLogger logger = null,
            T defaultValue = default)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                logger.Debug($"Operation Started: {operationName}");
                T result = operation();
                logger.Debug($"Operation Completed: {operationName}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error($"Operation Failed: {operationName}", ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an async operation with consistent logging and error handling.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="asyncOperation">The async operation to execute</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <returns>A task representing the operation's success</returns>
        public static async Task<bool> ExecuteAsyncWithLogging(
            string operationName,
            Func<Task> asyncOperation,
            ContextLogger logger = null)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                logger.Debug($"Async Operation Started: {operationName}");
                await asyncOperation();
                logger.Debug($"Async Operation Completed: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Async Operation Failed: {operationName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Executes an async operation with consistent logging and error handling, returning a result.
        /// </summary>
        /// <typeparam name="T">The return type of the async operation</typeparam>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="asyncOperation">The async operation to execute</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <param name="defaultValue">Default value to return if operation fails</param>
        /// <returns>A task representing the operation's result</returns>
        public static async Task<T> ExecuteAsyncWithLogging<T>(
            string operationName,
            Func<Task<T>> asyncOperation,
            ContextLogger logger = null,
            T defaultValue = default)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                logger.Debug($"Async Operation Started: {operationName}");
                T result = await asyncOperation();
                logger.Debug($"Async Operation Completed: {operationName}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error($"Async Operation Failed: {operationName}", ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an operation with cancellation support and consistent logging.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token to support operation cancellation</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool ExecuteWithCancellationHandling(
            string operationName,
            Action operation,
            CancellationToken cancellationToken,
            ContextLogger logger = null)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Debug($"Operation Started: {operationName}");
                operation();
                logger.Debug($"Operation Completed: {operationName}");
                return true;
            }
            catch (OperationCanceledException)
            {
                logger.Info($"Operation Cancelled: {operationName}");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error($"Operation Failed: {operationName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Executes an async operation with cancellation support and consistent logging.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="asyncOperation">The async operation to execute</param>
        /// <param name="cancellationToken">Cancellation token to support operation cancellation</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <returns>A task representing the operation's success</returns>
        public static async Task<bool> ExecuteAsyncWithCancellationHandling(
            string operationName,
            Func<Task> asyncOperation,
            CancellationToken cancellationToken,
            ContextLogger logger = null)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Debug($"Async Operation Started: {operationName}");
                await asyncOperation();
                logger.Debug($"Async Operation Completed: {operationName}");
                return true;
            }
            catch (OperationCanceledException)
            {
                logger.Info($"Async Operation Cancelled: {operationName}");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error($"Async Operation Failed: {operationName}", ex);
                return false;
            }
        }
        /// <summary>
        /// Executes an async operation with cancellation support and consistent logging,
        /// returning a result.
        /// </summary>
        /// <typeparam name="T">The return type of the async operation</typeparam>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="asyncOperation">The async operation to execute</param>
        /// <param name="cancellationToken">Cancellation token to support operation cancellation</param>
        /// <param name="logger">Optional logger for detailed tracking</param>
        /// <param name="defaultValue">Default value to return if operation fails or is cancelled</param>
        /// <returns>A task representing the operation's result</returns>
        public static async Task<T> ExecuteAsyncWithCancellationHandling<T>(
            string operationName,
            Func<Task<T>> asyncOperation,
            CancellationToken cancellationToken,
            ContextLogger logger = null,
            T defaultValue = default)
        {
            logger = logger ?? Logger.GetLogger(nameof(ErrorHelper));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Debug($"Async Operation Started: {operationName}");
                T result = await asyncOperation();
                logger.Debug($"Async Operation Completed: {operationName}");
                return result;
            }
            catch (OperationCanceledException)
            {
                logger.Info($"Async Operation Cancelled: {operationName}");
                return defaultValue;
            }
            catch (Exception ex)
            {
                logger.Error($"Async Operation Failed: {operationName}", ex);
                return defaultValue;
            }
        }
    }
}