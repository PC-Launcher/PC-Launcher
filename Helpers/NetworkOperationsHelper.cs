using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides centralized, robust methods for network-related operations with consistent error handling.
    /// </summary>
    public static class NetworkOperationHelper
    {
        /// <summary>
        /// Handles and logs network-related errors with optional user notification.
        /// </summary>
        /// <param name="networkOperation">Description of the network operation being performed</param>
        /// <param name="url">URL or network resource identifier</param>
        /// <param name="ex">Exception that occurred</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showToUser">Whether to show an error message to the user</param>
        public static void HandleNetworkError(
            string networkOperation,
            string url,
            Exception ex,
            ContextLogger logger,
            bool showToUser = false)
        {
            string message = $"Network Error: {networkOperation} failed for {url}";
            logger.Error(message, ex);

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
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>True if the operation completed successfully, false otherwise</returns>
        public static bool SafelyExecuteNetworkOperation(
            string networkOperation,
            string url,
            Action networkAction,
            ContextLogger logger,
            bool showErrorToUser = false)
        {
            try
            {
                logger.Debug($"Network Operation Started: {networkOperation} to {url}");
                networkAction();
                logger.Debug($"Network Operation Completed: {networkOperation} to {url}");
                return true;
            }
            catch (Exception ex)
            {
                HandleNetworkError(networkOperation, url, ex, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async network operation with proper error handling.
        /// </summary>
        /// <param name="networkOperation">Description of the network operation</param>
        /// <param name="url">URL or network resource identifier</param>
        /// <param name="networkAsyncAction">The async network operation to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="showErrorToUser">Whether to show error message boxes to the user</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteNetworkOperationAsync(
            string networkOperation,
            string url,
            Func<Task> networkAsyncAction,
            ContextLogger logger,
            bool showErrorToUser = false)
        {
            try
            {
                logger.Debug($"Async Network Operation Started: {networkOperation} to {url}");
                await networkAsyncAction();
                logger.Debug($"Async Network Operation Completed: {networkOperation} to {url}");
                return true;
            }
            catch (Exception ex)
            {
                HandleNetworkError(networkOperation, url, ex, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// Safely sends an HTTP GET request with error handling.
        /// </summary>
        /// <param name="url">URL to send the GET request to</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="timeoutSeconds">Timeout in seconds (default 30)</param>
        /// <returns>Response content as string, or null if an error occurs</returns>
        public static async Task<string> SafelyGetAsync(
            string url,
            ContextLogger logger,
            int timeoutSeconds = 30)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            {
                try
                {
                    logger.Debug($"Sending GET request to {url}");
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    logger.Debug($"Successfully received response from {url}");
                    return content;
                }
                catch (Exception ex)
                {
                    HandleNetworkError("sending GET request", url, ex, logger);
                    return null;
                }
            }
        }

        /// <summary>
        /// Safely sends an HTTP POST request with error handling.
        /// </summary>
        /// <param name="url">URL to send the POST request to</param>
        /// <param name="content">HTTP content to send</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <param name="timeoutSeconds">Timeout in seconds (default 30)</param>
        /// <returns>Response content as string, or null if an error occurs</returns>
        public static async Task<string> SafelyPostAsync(
            string url,
            HttpContent content,
            ContextLogger logger,
            int timeoutSeconds = 30)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            {
                try
                {
                    logger.Debug($"Sending POST request to {url}");
                    var response = await client.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();
                    logger.Debug($"Successfully received response from {url}");
                    return responseContent;
                }
                catch (Exception ex)
                {
                    HandleNetworkError("sending POST request", url, ex, logger);
                    return null;
                }
            }
        }

        /// <summary>
        /// Checks internet connectivity with a reliable URL.
        /// </summary>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if internet is available, false otherwise</returns>
        public static async Task<bool> CheckInternetConnectivityAsync(ContextLogger logger)
        {
            string[] testUrls = new[]
            {
                "https://www.google.com",
                "https://www.microsoft.com",
                "https://www.cloudflare.com"
            };

            foreach (var url in testUrls)
            {
                try
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                    {
                        var response = await client.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            logger.Info($"Internet connectivity confirmed via {url}");
                            return true;
                        }
                    }
                }
                catch
                {
                    // Suppress individual connection errors, we'll check all URLs
                }
            }

            logger.Warning("No internet connectivity detected");
            return false;
        }
    }
}
