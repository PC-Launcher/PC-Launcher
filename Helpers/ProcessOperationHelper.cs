using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides helper methods for process operations and error handling.
    /// </summary>
    public static class ProcessOperationHelper
    {
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

            if (showToUser && System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"An error occurred while {processOperation} {processName}: {ex.Message}",
                        "Process Operation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Checks if a process with the specified name is running.
        /// </summary>
        /// <param name="processName">The name of the process to check</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>True if at least one process with the specified name is running</returns>
        public static bool IsProcessRunning(string processName, ContextLogger logger = null)
        {
            try
            {
                if (string.IsNullOrEmpty(processName))
                {
                    logger?.Warning("Cannot check if process is running: processName is null or empty");
                    return false;
                }

                Process[] processes = Process.GetProcessesByName(processName);
                bool isRunning = processes.Length > 0;

                logger?.Debug($"Process '{processName}' is {(isRunning ? "running" : "not running")}");

                // Clean up process array
                SafelyDisposeProcesses(processes, logger);

                return isRunning;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error checking if process '{processName}' is running", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets child processes of a specified parent process.
        /// </summary>
        /// <param name="parentId">The ID of the parent process</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>A list of child processes, or an empty list if none are found or an error occurs</returns>
        public static List<Process> GetChildProcesses(int parentId, ContextLogger logger = null)
        {
            var children = new List<Process>();
            try
            {
                logger?.Debug($"Looking for child processes of parent ID: {parentId}");

                // Get all running processes to examine
                Process[] allProcesses = Process.GetProcesses();

                using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ParentProcessId = {parentId}"))
                {
                    foreach (var proc in searcher.Get())
                    {
                        try
                        {
                            int childId = Convert.ToInt32(proc["ProcessId"]);
                            var childProcess = allProcesses.FirstOrDefault(p => p.Id == childId);
                            if (childProcess != null)
                            {
                                children.Add(childProcess);
                                logger?.Debug($"Found child process: {childProcess.ProcessName} (ID: {childProcess.Id})");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.Error($"Error processing child process info", ex);
                        }
                    }
                }

                logger?.Info($"Found {children.Count} child processes for parent process {parentId}");
                return children;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error retrieving child processes for parent ID {parentId}", ex);

                // Make sure we dispose any processes we collected before the error
                foreach (var process in children)
                {
                    SafelyDisposeProcess(process, process.ProcessName, logger);
                }

                return new List<Process>();
            }
        }

        /// <summary>
        /// Safely disposes a process object.
        /// </summary>
        /// <param name="process">The process to dispose</param>
        /// <param name="processName">The name of the process (for logging)</param>
        /// <param name="logger">Optional logger for logging information</param>
        public static void SafelyDisposeProcess(Process process, string processName = null, ContextLogger logger = null)
        {
            if (process == null)
                return;

            try
            {
                string logProcessName = processName ?? "Unknown";
                int processId = -1;

                try
                {
                    processId = process.Id;
                    logProcessName = process.ProcessName;
                }
                catch { /* Ignore errors getting process information */ }

                logger?.Debug($"Disposing process object for {logProcessName} (ID: {processId})");
                process.Dispose();
            }
            catch (Exception ex)
            {
                logger?.Error($"Error disposing process object", ex);
            }
        }

        /// <summary>
        /// Safely disposes an array of process objects.
        /// </summary>
        /// <param name="processes">Array of processes to dispose</param>
        /// <param name="logger">Optional logger for logging information</param>
        public static void SafelyDisposeProcesses(Process[] processes, ContextLogger logger = null)
        {
            if (processes == null)
                return;

            foreach (var process in processes)
            {
                SafelyDisposeProcess(process, null, logger);
            }

            logger?.Debug($"Disposed {processes.Length} process objects");
        }

        /// <summary>
        /// Safely terminates a process with proper error handling.
        /// </summary>
        /// <param name="process">The Process object to terminate</param>
        /// <param name="timeoutMs">Timeout in milliseconds to wait for the process to exit</param>
        /// <param name="processName">Name of the process (optional, for logging)</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>True if the process was successfully terminated, false otherwise</returns>
        public static bool SafelyKillProcess(Process process, int timeoutMs = 3000, string processName = null, ContextLogger logger = null)
        {
            if (process == null) return false;

            string logProcessName = "Unknown";
            int processId = -1;

            try
            {
                logProcessName = processName ?? process.ProcessName;
                processId = process.Id;

                logger?.Info($"Attempting to terminate process {logProcessName} (ID: {processId})");

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(timeoutMs);

                    if (process.HasExited)
                    {
                        logger?.Info($"Successfully terminated process {logProcessName} (ID: {processId})");
                        return true;
                    }
                    else
                    {
                        logger?.Warning($"Process {logProcessName} (ID: {processId}) did not exit within timeout period");
                        return false;
                    }
                }
                else
                {
                    logger?.Info($"Process {logProcessName} (ID: {processId}) has already exited");
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    HandleProcessError("terminating", logProcessName, processId, ex);
                }
                else
                {
                    Logger.LogError($"Error terminating process {logProcessName} (ID: {processId})", ex);
                }

                // Try fallback using taskkill
                return FallbackProcessTermination(logProcessName, processId, logger);
            }
            finally
            {
                SafelyDisposeProcess(process, logProcessName, logger);
            }
        }

        /// <summary>
        /// Fallback method to terminate a process using taskkill when normal termination fails.
        /// </summary>
        /// <param name="processName">Name of the process to terminate</param>
        /// <param name="processId">ID of the process, if available</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>True if the fallback method was executed without exceptions, false otherwise</returns>
        public static bool FallbackProcessTermination(string processName, int processId, ContextLogger logger = null)
        {
            try
            {
                logger?.Info($"Attempting fallback termination for {processName} (ID: {processId})");

                if (processId > 0)
                {
                    // Try to kill by PID first
                    ExecuteTaskKill($"/F /PID {processId}", logger);
                }

                // Also try by name if we have it
                if (!string.IsNullOrEmpty(processName))
                {
                    ExecuteTaskKill($"/F /IM \"{processName}.exe\"", logger);
                }

                logger?.Info($"Fallback termination completed for {processName}");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"Fallback process termination also failed for {processName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Helper method to execute the taskkill command.
        /// </summary>
        /// <param name="arguments">Arguments for taskkill</param>
        /// <param name="logger">Optional logger for logging information</param>
        public static void ExecuteTaskKill(string arguments, ContextLogger logger = null)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                logger?.Debug($"Executing taskkill with arguments: {arguments}");
                process.Start();
                process.WaitForExit(3000);

                if (process.HasExited)
                {
                    logger?.Debug($"Taskkill process completed with exit code: {process.ExitCode}");
                }
                else
                {
                    logger?.Warning("Taskkill process did not complete within timeout");
                }
            }
        }

        /// <summary>
        /// Terminates a process and all its child processes.
        /// </summary>
        /// <param name="processId">ID of the parent process to terminate</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>True if the process and its children were successfully terminated</returns>
        public static async Task<bool> SafelyTerminateProcessTree(int processId, ContextLogger logger = null)
        {
            try
            {
                logger?.Info($"Terminating process tree for process ID: {processId}");

                Process process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                }
                catch (ArgumentException)
                {
                    logger?.Info($"Process {processId} not found; it may have already exited");
                    return true;
                }

                try
                {
                    // Get child processes
                    var children = GetChildProcesses(processId, logger);
                    logger?.Info($"Found {children.Count} child processes for parent process {processId}");

                    // Process children recursively
                    var killTasks = new List<Task<bool>>();
                    foreach (var child in children)
                    {
                        logger?.Info($"Terminating child process {child.ProcessName} (ID: {child.Id})");
                        killTasks.Add(SafelyTerminateProcessTree(child.Id, logger));
                    }

                    if (killTasks.Count > 0)
                    {
                        await Task.WhenAll(killTasks);
                    }

                    // Now kill the parent process
                    logger?.Info($"Killing parent process {process.ProcessName} (ID: {process.Id})");
                    return SafelyKillProcess(process, 3000, process.ProcessName, logger);
                }
                finally
                {
                    SafelyDisposeProcess(process, process?.ProcessName, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Error in SafelyTerminateProcessTree for process {processId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the command line arguments for a running process.
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <param name="token">Cancellation token to abort the operation</param>
        /// <param name="logger">Optional logger for logging information</param>
        /// <returns>The command line string or empty string if not available</returns>
        public static async Task<string> GetProcessCommandLineAsync(int processId, CancellationToken token, ContextLogger logger = null)
        {
            string commandLine = "";
            ManagementObjectSearcher searcher = null;

            try
            {
                // Check for cancellation first to avoid creating a searcher that won't be used
                token.ThrowIfCancellationRequested();

                logger?.Debug($"Getting command line for process ID: {processId}");

                // Create a searcher with a specific scope and query
                searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");

                // Check for cancellation before executing the query
                token.ThrowIfCancellationRequested();

                // WMI queries can be slow, so let's run it on a background thread
                var results = await Task.Run(() => searcher.Get(), token);

                // Process results
                foreach (ManagementObject obj in results)
                {
                    token.ThrowIfCancellationRequested();
                    commandLine = obj["CommandLine"]?.ToString();
                    break;
                }

                logger?.Debug($"Command line for process ID {processId}: {commandLine}");
                return commandLine;
            }
            catch (OperationCanceledException)
            {
                logger?.Debug($"Command line retrieval for process {processId} canceled.");
                throw;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error getting command line for process id {processId}", ex);
                return "";
            }
            finally
            {
                // Properly dispose of the searcher to release COM resources
                if (searcher != null)
                {
                    try
                    {
                        searcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"Error disposing WMI searcher", ex);
                    }
                }
            }
        }
    }
}