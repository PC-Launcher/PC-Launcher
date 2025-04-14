using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace PCStreamerLauncher.Logging
{
    /// <summary>
    /// Nested TraceListener class that routes Debug output to our Logger.
    /// </summary>
    public class LoggerTraceListener : TraceListener
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string TraceLogFilePath = Path.Combine(LogDirectory, $"trace_{DateTime.Now:yyyyMMdd}.log");
        private static readonly object _fileLock = new object();
        private static bool _directWriteInProgress = false;
        private static bool _initialized = false;
        private static readonly ContextLogger _logger = Logger.GetLogger(nameof(LoggerTraceListener));

        public LoggerTraceListener()
        {
            InitializeTraceListener();
        }

        private void InitializeTraceListener()
        {
            if (_initialized) return;

            lock (_fileLock)
            {
                if (_initialized) return;

                try
                {
                    // Ensure log directory exists
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    // Write a header to the trace log file
                    using (var writer = new StreamWriter(TraceLogFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine($"=== Trace log started at {DateTime.Now} ===");
                    }

                    _initialized = true;
                    _logger.Info("Trace listener initialized successfully");
                }
                catch (Exception ex)
                {
                    // Silent fail - we'll try to continue anyway
                    Debug.WriteLine($"Failed to initialize LoggerTraceListener: {ex.Message}");
                }
            }
        }

        public override void Write(string message)
        {
            // CRITICAL: Prevent recursion by checking if we're already in a direct write operation
            if (_directWriteInProgress)
                return;

            try
            {
                // Set flag to prevent recursion
                _directWriteInProgress = true;

                // Write directly to a separate trace log file to avoid conflicts with main log
                WriteToTraceLogFile(message);
            }
            catch (Exception ex)
            {
                // Silently fail - we can't risk exceptions here
                Debug.WriteLine($"Error in LoggerTraceListener.Write: {ex.Message}");
            }
            finally
            {
                // Clear the flag
                _directWriteInProgress = false;
            }
        }

        public override void WriteLine(string message)
        {
            // CRITICAL: Prevent recursion by checking if we're already in a direct write operation
            if (_directWriteInProgress)
                return;

            try
            {
                // Set flag to prevent recursion
                _directWriteInProgress = true;

                // Write directly to a separate trace log file to avoid conflicts with main log
                WriteToTraceLogFile(message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Silently fail - we can't risk exceptions here
                Debug.WriteLine($"Error in LoggerTraceListener.WriteLine: {ex.Message}");
            }
            finally
            {
                // Clear the flag
                _directWriteInProgress = false;
            }
        }

        private void WriteToTraceLogFile(string content)
        {
            try
            {
                // Avoid timestamp prefix for empty lines or very short messages
                string logEntry;
                if (string.IsNullOrWhiteSpace(content) || content.Length < 3)
                {
                    logEntry = content;
                }
                else
                {
                    logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Trace] {content}";
                }

                // Use lock to prevent multiple threads from writing simultaneously
                lock (_fileLock)
                {
                    // Use StreamWriter with append mode
                    using (var writer = new StreamWriter(TraceLogFilePath, true, Encoding.UTF8))
                    {
                        writer.Write(logEntry);
                        writer.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent fail - nothing we can do if this fails
                // We specifically don't want to use Debug.WriteLine here to avoid infinite recursion
                // But as a safety measure, we can still output to console in extreme cases
                try
                {
                    Console.WriteLine($"Fatal error in LoggerTraceListener.WriteToTraceLogFile: {ex.Message}");
                }
                catch
                {
                    // Ultimate fallback - do nothing if even Console.WriteLine fails
                }
            }
        }
    }
}