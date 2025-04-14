using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.CompilerServices;

namespace PCStreamerLauncher.Logging
{
    /// <summary>
    /// Enhanced version of the Logger class that provides more robust logging capabilities.
    /// </summary>
    public static class Logger
    {
        // Define log severity levels
        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Info = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        // Current minimum log level to record
        private static LogLevel _minimumLogLevel = LogLevel.Info;

        // Define the logs directory and file path
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
        private static readonly string ArchiveDirectory = Path.Combine(LogDirectory, "Archive");

        // Retention periods
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);
        private static readonly int MaxLogSizeMB = 10;

        // Thread-safe queue for asynchronous logging
        private static readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private static readonly ManualResetEventSlim _loggingEvent = new ManualResetEventSlim(false);
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static Task _loggingTask;
        private static readonly object _logFileLock = new object();
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Initializes the logger, creating necessary directories and starting the logging task.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    // Create log directories if they don't exist
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    if (!Directory.Exists(ArchiveDirectory))
                    {
                        Directory.CreateDirectory(ArchiveDirectory);
                    }

                    // Start the background logging task
                    _loggingTask = Task.Run(ProcessLogQueue, _cancellationTokenSource.Token);

                    // Perform initial cleanup of old logs
                    Task.Run(CleanUpOldLogs);

                    // Add handler for unhandled exceptions at the AppDomain level
                    AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                    {
                        LogCritical("Unhandled exception in AppDomain", args.ExceptionObject as Exception);
                    };

                    _initialized = true;
                    LogInfo("Logger initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error initializing logger: {ex.Message}");

                    // Still allow the application to run even if logging fails
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Sets the minimum log level to be recorded.
        /// </summary>
        public static void SetMinimumLogLevel(LogLevel level)
        {
            _minimumLogLevel = level;
            LogInfo($"Log level set to {level}");
        }

        /// <summary>
        /// Gets the current minimum log level.
        /// </summary>
        public static LogLevel GetMinimumLogLevel()
        {
            return _minimumLogLevel;
        }

        /// <summary>
        /// Gets a context-specific logger with a predefined class name.
        /// </summary>
        /// <param name="className">The class name to associate with log messages</param>
        /// <returns>A context logger instance</returns>
        public static ContextLogger GetLogger(string className)
        {
            return new ContextLogger(className);
        }

        /// <summary>
        /// Provides a way to get context-specific logger using the current type.
        /// </summary>
        /// <typeparam name="T">The type to use for naming the logger</typeparam>
        /// <returns>A context logger instance</returns>
        public static ContextLogger GetLogger<T>()
        {
            return new ContextLogger(typeof(T).Name);
        }

        /// <summary>
        /// Logs a message with the specified severity level.
        /// </summary>
        public static void Log(LogLevel level, string message, Exception ex = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (level < _minimumLogLevel)
                return;

            try
            {
                string callerInfo = $"{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}";

                StringBuilder logBuilder = new StringBuilder();
                logBuilder.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{callerInfo}] {message}");

                if (ex != null)
                {
                    logBuilder.AppendLine();
                    logBuilder.Append($"Exception: {ex.GetType().Name}: {ex.Message}");
                    logBuilder.AppendLine();
                    logBuilder.Append($"Stack Trace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        logBuilder.AppendLine();
                        logBuilder.Append($"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }
                }

                string logMessage = logBuilder.ToString();

                // Enqueue for asynchronous writing
                _logQueue.Enqueue(logMessage);
                _loggingEvent.Set();

                // Also log to Debug output for immediate visibility
                Debug.WriteLine(logMessage);
            }
            catch (Exception logEx)
            {
                // Last resort if logging itself fails
                Debug.WriteLine($"Logging error: {logEx.Message}");
            }
        }

        /// <summary>
        /// Logs a message with specified prefix and severity level.
        /// Useful for standardizing component messages.
        /// </summary>
        public static void LogWithPrefix(string prefix, LogLevel level, string message, Exception ex = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(level, $"{prefix}: {message}", ex, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Background task to process the log queue and write to file.
        /// </summary>
        private static async Task ProcessLogQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Wait for something to log or cancellation
                await Task.Run(() => _loggingEvent.Wait(_cancellationTokenSource.Token));

                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                // Reset the event
                _loggingEvent.Reset();

                // Process all queued items
                StringBuilder batchBuilder = new StringBuilder();
                int count = 0;

                while (_logQueue.TryDequeue(out string logMessage))
                {
                    batchBuilder.AppendLine(logMessage);
                    count++;

                    // Process in batches of 100 messages to avoid excessive StringBuilder growth
                    if (count >= 100)
                    {
                        WriteLogBatch(batchBuilder.ToString());
                        batchBuilder.Clear();
                        count = 0;
                    }
                }

                // Write any remaining messages
                if (count > 0)
                {
                    WriteLogBatch(batchBuilder.ToString());
                }

                // Check log file size and rotate if needed
                CheckLogFileSize();
            }
        }

        /// <summary>
        /// Writes a batch of log messages to the log file.
        /// </summary>
        private static void WriteLogBatch(string batchContent)
        {
            try
            {
                lock (_logFileLock)
                {
                    File.AppendAllText(LogFilePath, batchContent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the log file has exceeded the maximum size and rotates it if necessary.
        /// </summary>
        private static void CheckLogFileSize()
        {
            try
            {
                FileInfo logFile = new FileInfo(LogFilePath);
                if (!logFile.Exists) return;

                // Check if log file exceeds max size (convert MB to bytes)
                if (logFile.Length > MaxLogSizeMB * 1024 * 1024)
                {
                    RotateLogFile();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking log file size: {ex.Message}");
            }
        }

        /// <summary>
        /// Rotates the current log file to an archive file.
        /// </summary>
        private static void RotateLogFile()
        {
            try
            {
                lock (_logFileLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string archiveFileName = Path.Combine(
                        ArchiveDirectory,
                        $"app_{timestamp}.log");

                    // Move the current log file to the archive
                    File.Move(LogFilePath, archiveFileName);

                    // Create a new empty log file
                    using (File.Create(LogFilePath)) { }

                    // Log the rotation to the new file
                    string rotationMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Info] Log file rotated. Previous log: {archiveFileName}";
                    File.AppendAllText(LogFilePath, rotationMessage + Environment.NewLine);
                    Debug.WriteLine(rotationMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rotating log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up log files older than the retention period.
        /// </summary>
        private static void CleanUpOldLogs()
        {
            try
            {
                // Get all log files from both the log directory and archive
                var logFiles = Directory.GetFiles(LogDirectory, "*.log");
                var archiveFiles = Directory.Exists(ArchiveDirectory)
                    ? Directory.GetFiles(ArchiveDirectory, "*.log")
                    : Array.Empty<string>();

                var allLogFiles = new string[logFiles.Length + archiveFiles.Length];
                Array.Copy(logFiles, allLogFiles, logFiles.Length);
                Array.Copy(archiveFiles, 0, allLogFiles, logFiles.Length, archiveFiles.Length);

                foreach (var file in allLogFiles)
                {
                    if (file == LogFilePath) continue; // Skip the current log file

                    FileInfo fileInfo = new FileInfo(file);
                    if (DateTime.Now - fileInfo.CreationTime > RetentionPeriod)
                    {
                        fileInfo.Delete();
                        Debug.WriteLine($"Deleted old log file: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Log cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down the logger gracefully, ensuring all messages are written.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                // Log that we're shutting down
                LogInfo("Logger shutting down...");

                // Process any remaining log messages
                _cancellationTokenSource.Cancel();
                _loggingEvent.Set();

                // Wait for the logging task to complete (with timeout)
                if (_loggingTask != null)
                {
                    Task.WaitAny(new[] { _loggingTask }, 2000);
                }

                // Write any remaining messages directly
                StringBuilder finalBatch = new StringBuilder();
                while (_logQueue.TryDequeue(out string message))
                {
                    finalBatch.AppendLine(message);
                }

                if (finalBatch.Length > 0)
                {
                    lock (_logFileLock)
                    {
                        File.AppendAllText(LogFilePath, finalBatch.ToString());
                    }
                }

                // Log final shutdown message directly
                string shutdownMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Info] Logger shutdown complete";
                lock (_logFileLock)
                {
                    File.AppendAllText(LogFilePath, shutdownMessage + Environment.NewLine);
                }
                Debug.WriteLine(shutdownMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during logger shutdown: {ex.Message}");
            }
            finally
            {
                _loggingEvent.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }

        #region Convenience Methods

        /// <summary>
        /// Logs a trace message for detailed debugging.
        /// </summary>
        public static void LogTrace(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Trace, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a debug message for general debugging.
        /// </summary>
        public static void LogDebug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Debug, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Info, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void LogWarning(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Warning, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void LogError(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Error, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a critical error message.
        /// </summary>
        public static void LogCritical(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Critical, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }

        #endregion

        /// <summary>
        /// Log and propagate an exception with consistent handling.
        /// Returns the exception so it can be thrown after logging.
        /// Usage: throw LogAndPropagateException(new Exception("Some error"));
        /// </summary>
        public static Exception LogAndPropagateException(Exception ex, string additionalInfo = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            string message = additionalInfo ?? $"Exception in {memberName}";
            LogError(message, ex, memberName, sourceFilePath, sourceLineNumber);
            return ex;
        }

        /// <summary>
        /// Helper for UI thread exception logging with consistent pattern.
        /// </summary>
        public static void LogUIException(string message, Exception ex, bool showToUser = false)
        {
            LogError(message, ex);

            if (showToUser && Application.Current != null)
            {
                // Ensure message box is shown on UI thread
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"{message}\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Nested TraceListener class that routes Debug output to our Logger.
        /// </summary>
        public class LoggerTraceListener : TraceListener
        {
            public override void Write(string message)
            {
                // Don't use LogTrace here to avoid potential infinite recursion
                Debug.WriteLine(message);

                // Try to log to file directly in case of bootstrap issues
                try
                {
                    if (!_initialized) Initialize();
                    _logQueue.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Trace] {message}");
                    _loggingEvent.Set();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in LoggerTraceListener.Write: {ex.Message}");
                }
            }

            public override void WriteLine(string message)
            {
                Write(message + Environment.NewLine);
            }
        }
    }

    /// <summary>
    /// A context-specific logger that automatically prefixes log messages with a component name.
    /// Helps standardize log messages from a particular component.
    /// </summary>
    public class ContextLogger
    {
        private readonly string _contextName;

        public ContextLogger(string contextName)
        {
            _contextName = contextName;
        }

        /// <summary>
        /// Logs a trace message prefixed with the context name.
        /// </summary>
        public void Trace(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Trace, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a debug message prefixed with the context name.
        /// </summary>
        public void Debug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Debug, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs an info message prefixed with the context name.
        /// </summary>
        public void Info(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Info, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a warning message prefixed with the context name.
        /// </summary>
        public void Warning(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Warning, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs an error message prefixed with the context name.
        /// </summary>
        public void Error(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Error, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs a critical error message prefixed with the context name.
        /// </summary>
        public void Critical(string message, Exception ex = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWithPrefix(_contextName, Logger.LogLevel.Critical, message, ex, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    /// <summary>
    /// Extension methods for try/catch patterns to ensure consistency.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Executes an action with standardized exception handling.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="errorMessage">The error message to log if an exception occurs.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="filePath">The source file path (auto-populated).</param>
        /// <param name="lineNumber">The source line number (auto-populated).</param>
        /// <returns>True if the action executed without exceptions, otherwise false.</returns>
        public static bool TrySafely(this Action action, string errorMessage = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                string message = errorMessage ?? $"Exception in {memberName}";
                Logger.LogError(message, ex, memberName, filePath, lineNumber);
                return false;
            }
        }

        /// <summary>
        /// Executes a function with standardized exception handling and returns a default value if an exception occurs.
        /// </summary>
        /// <typeparam name="T">The type of the return value.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="defaultValue">The default value to return if an exception occurs.</param>
        /// <param name="errorMessage">The error message to log if an exception occurs.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="filePath">The source file path (auto-populated).</param>
        /// <param name="lineNumber">The source line number (auto-populated).</param>
        /// <returns>The result of the function or the default value if an exception occurs.</returns>
        public static T TrySafely<T>(this Func<T> func, T defaultValue = default, string errorMessage = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                string message = errorMessage ?? $"Exception in {memberName}";
                Logger.LogError(message, ex, memberName, filePath, lineNumber);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an async action with standardized exception handling.
        /// </summary>
        /// <param name="action">The async action to execute.</param>
        /// <param name="errorMessage">The error message to log if an exception occurs.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="filePath">The source file path (auto-populated).</param>
        /// <param name="lineNumber">The source line number (auto-populated).</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the action executed without exceptions, otherwise false.</returns>
        public static async Task<bool> TrySafelyAsync(this Func<Task> action, string errorMessage = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                string message = errorMessage ?? $"Exception in async operation {memberName}";
                Logger.LogError(message, ex, memberName, filePath, lineNumber);
                return false;
            }
        }

        /// <summary>
        /// Executes an async function with standardized exception handling and returns a default value if an exception occurs.
        /// </summary>
        /// <typeparam name="T">The type of the return value.</typeparam>
        /// <param name="func">The async function to execute.</param>
        /// <param name="defaultValue">The default value to return if an exception occurs.</param>
        /// <param name="errorMessage">The error message to log if an exception occurs.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="filePath">The source file path (auto-populated).</param>
        /// <param name="lineNumber">The source line number (auto-populated).</param>
        /// <returns>A task that represents the asynchronous operation. The task result is the result of the function or the default value if an exception occurs.</returns>
        public static async Task<T> TrySafelyAsync<T>(this Func<Task<T>> func, T defaultValue = default, string errorMessage = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                string message = errorMessage ?? $"Exception in async operation {memberName}";
                Logger.LogError(message, ex, memberName, filePath, lineNumber);
                return defaultValue;
            }
        }

        /// <summary>
        /// Safely executes an operation on the UI thread with standardized exception handling.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="errorMessage">The error message to log if an exception occurs.</param>
        /// <returns>True if the action executed without exceptions, otherwise false.</returns>
        public static bool TryOnUIThread(this Action action, string errorMessage = "UI operation failed")
        {
            if (Application.Current == null) return false;

            try
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(action);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(errorMessage, ex);
                return false;
            }
        }
    }
}