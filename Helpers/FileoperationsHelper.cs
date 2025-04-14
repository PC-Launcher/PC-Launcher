using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides centralized, robust methods for file-related operations with consistent error handling.
    /// </summary>
    public static class FileOperationHelper
    {
        /// <summary>
        /// Handles and logs file-related errors with optional user notification.
        /// </summary>
        /// <param name="fileOperation">Description of the file operation being performed</param>
        /// <param name="filePath">Path of the file involved in the operation</param>
        /// <param name="ex">Exception that occurred</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showToUser">Whether to show an error message to the user</param>
        public static void HandleFileError(
            string fileOperation,
            string filePath,
            Exception ex,
            ContextLogger logger,
            bool showToUser = false)
        {
            string message = $"File Error: {fileOperation} failed for {filePath}";
            logger.Error(message, ex);

            if (showToUser && Application.Current != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"An error occurred while {fileOperation} file {Path.GetFileName(filePath)}: {ex.Message}",
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
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool SafelyExecuteFileOperation(
            string fileOperation,
            string filePath,
            Action fileAction,
            ContextLogger logger,
            bool showErrorToUser = false)
        {
            try
            {
                logger.Debug($"File Operation Started: {fileOperation} on {filePath}");
                fileAction();
                logger.Debug($"File Operation Completed: {fileOperation} on {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                HandleFileError(fileOperation, filePath, ex, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async file operation with proper error handling.
        /// </summary>
        /// <param name="fileOperation">Description of the file operation</param>
        /// <param name="filePath">Path of the file</param>
        /// <param name="fileAsyncAction">The async file operation to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteFileOperationAsync(
            string fileOperation,
            string filePath,
            Func<Task> fileAsyncAction,
            ContextLogger logger,
            bool showErrorToUser = false)
        {
            try
            {
                logger.Debug($"Async File Operation Started: {fileOperation} on {filePath}");
                await fileAsyncAction();
                logger.Debug($"Async File Operation Completed: {fileOperation} on {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                HandleFileError(fileOperation, filePath, ex, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely reads all text from a file with error handling.
        /// </summary>
        /// <param name="filePath">Path of the file to read</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>File contents as string, or null if an error occurs</returns>
        public static string SafelyReadAllText(
            string filePath,
            ContextLogger logger)
        {
            return SafelyExecuteFileOperation(
                "reading text from",
                filePath,
                () => File.ReadAllText(filePath),
                logger
            ) ? File.ReadAllText(filePath) : null;
        }

        /// <summary>
        /// Safely writes all text to a file with error handling.
        /// </summary>
        /// <param name="filePath">Path of the file to write</param>
        /// <param name="contents">Text contents to write</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if write was successful, false otherwise</returns>
        public static bool SafelyWriteAllText(
            string filePath,
            string contents,
            ContextLogger logger)
        {
            return SafelyExecuteFileOperation(
                "writing text to",
                filePath,
                () => File.WriteAllText(filePath, contents),
                logger
            );
        }

        /// <summary>
        /// Safely creates a directory with error handling.
        /// </summary>
        /// <param name="directoryPath">Path of the directory to create</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if directory creation was successful, false otherwise</returns>
        public static bool SafelyCreateDirectory(
            string directoryPath,
            ContextLogger logger)
        {
            return SafelyExecuteFileOperation(
                "creating directory",
                directoryPath,
                () => Directory.CreateDirectory(directoryPath),
                logger
            );
        }
    }
}
