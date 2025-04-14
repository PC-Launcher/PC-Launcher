using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Provides standardized methods for cleaning up various types of resources.
    /// This centralizes cleanup logic to ensure consistent resource disposal throughout the application.
    /// </summary>
    public static class ResourceCleanup
    {
        private static readonly ContextLogger _logger = Logger.GetLogger(nameof(ResourceCleanup));

        #region Animation Resources

        /// <summary>
        /// Safely stops animations on a transform.
        /// </summary>
        /// <param name="transform">The transform to clean up.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopTransformAnimations(Transform transform, string contextName = "")
        {
            if (transform == null) return;

            try
            {
                if (transform is ScaleTransform scaleTransform && !scaleTransform.IsFrozen)
                {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                }
                else if (transform is TranslateTransform translateTransform && !translateTransform.IsFrozen)
                {
                    translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                    translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                }
                else if (transform is RotateTransform rotateTransform && !rotateTransform.IsFrozen)
                {
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                }

                _logger.Info($"Stopped transform animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping transform animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely stops animations on a UIElement.
        /// </summary>
        /// <param name="element">The UI element to clean up.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopElementAnimations(UIElement element, string contextName = "")
        {
            if (element == null) return;

            try
            {
                // Stop any animations on the element itself
                element.BeginAnimation(UIElement.OpacityProperty, null);

                // Stop animations on the transform if it exists
                if (element.RenderTransform != null)
                {
                    StopTransformAnimations(element.RenderTransform, contextName);
                }

                _logger.Info($"Stopped element animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping element animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely stops animations on a brush.
        /// </summary>
        /// <param name="brush">The brush to clean up.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopBrushAnimations(Brush brush, string contextName = "")
        {
            if (brush == null) return;

            try
            {
                if (brush is SolidColorBrush solidBrush && !solidBrush.IsFrozen)
                {
                    solidBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                }

                _logger.Info($"Stopped brush animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping brush animations {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely stops animations on a storyboard.
        /// </summary>
        /// <param name="storyboard">The storyboard to clean up.</param>
        /// <param name="target">The target element for the storyboard.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopStoryboard(Storyboard storyboard, FrameworkElement target = null, string contextName = "")
        {
            if (storyboard == null) return;

            try
            {
                storyboard.Stop(target);
                storyboard.Remove();

                _logger.Info($"Stopped storyboard {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping storyboard {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }
        #endregion

        #region UI Element Resources

        /// <summary>
        /// Safely disconnects a UI element from its parent panel.
        /// </summary>
        /// <param name="element">The element to remove.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void RemoveFromParentPanel(UIElement element, string contextName = "")
        {
            if (element == null) return;

            try
            {
                // Get the parent directly - no recursion
                DependencyObject parent = VisualTreeHelper.GetParent(element);
                if (parent is Panel panel)
                {
                    if (panel.Children.Contains(element))
                    {
                        panel.Children.Remove(element);
                        _logger.Info($"Removed element from parent panel {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error removing UIElement from parent panel {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }
        /// <summary>
        /// Safely cleans up resources for an Image control.
        /// </summary>
        /// <param name="image">The image to clean up.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void CleanupImage(Image image, string contextName = "")
        {
            if (image == null) return;

            try
            {
                // First, stop any animations on the image
                StopElementAnimations(image, contextName);

                // Then clean up the image source
                if (image.Source is BitmapImage bitmapImage)
                {
                    CleanupBitmapImage(bitmapImage, contextName);
                }

                // Clear the source to allow GC
                image.Source = null;

                _logger.Info($"Cleaned up image {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error cleaning up image {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely cleans up a BitmapImage.
        /// </summary>
        /// <param name="bitmap">The bitmap to clean up.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void CleanupBitmapImage(BitmapImage bitmap, string contextName = "")
        {
            if (bitmap == null) return;

            try
            {
                if (bitmap.StreamSource != null)
                {
                    try
                    {
                        bitmap.StreamSource.Close();
                        bitmap.StreamSource.Dispose();
                    }
                    catch (Exception streamEx)
                    {
                        _logger.Error($"Error disposing bitmap stream {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", streamEx);
                    }
                }

                // Only try to clear the UriSource if the bitmap is not frozen
                if (!bitmap.IsFrozen)
                {
                    try
                    {
                        bitmap.UriSource = null;
                    }
                    catch (Exception uriEx)
                    {
                        _logger.Warning($"Could not clear UriSource on bitmap {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", uriEx);
                    }
                }

                _logger.Info($"Cleaned up bitmap image {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error cleaning up bitmap image {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion

        #region Timer Resources

        /// <summary>
        /// Safely stops and disposes a DispatcherTimer.
        /// </summary>
        /// <param name="timer">The timer to dispose.</param>
        /// <param name="tickHandler">Optional event handler to detach.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopAndDisposeTimer(DispatcherTimer timer, EventHandler tickHandler = null, string contextName = "")
        {
            if (timer == null) return;

            try
            {
                // Stop the timer first
                if (timer.IsEnabled)
                {
                    timer.Stop();
                }

                // Remove the event handler if provided
                if (tickHandler != null)
                {
                    timer.Tick -= tickHandler;
                }

                _logger.Info($"Stopped and disposed timer {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping and disposing timer {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely stops and disposes a System.Timers.Timer.
        /// </summary>
        /// <param name="timer">The timer to dispose.</param>
        /// <param name="elapsedHandler">Optional event handler to detach.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void StopAndDisposeTimer(System.Timers.Timer timer, System.Timers.ElapsedEventHandler elapsedHandler = null, string contextName = "")
        {
            if (timer == null) return;

            try
            {
                // Stop the timer first
                timer.Stop();

                // Remove the event handler if provided
                if (elapsedHandler != null)
                {
                    timer.Elapsed -= elapsedHandler;
                }

                // Dispose the timer
                timer.Dispose();

                _logger.Info($"Stopped and disposed System.Timers.Timer {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping and disposing System.Timers.Timer {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion

        #region Process Resources

        /// <summary>
        /// Safely disposes a Process object.
        /// </summary>
        /// <param name="process">The process to dispose.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DisposeProcess(Process process, string contextName = "")
        {
            if (process == null) return;

            try
            {
                // Try to close process handles and dispose the object
                process.Dispose();
                _logger.Info($"Disposed process {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disposing process {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely disposes an array of Process objects.
        /// </summary>
        /// <param name="processes">The processes to dispose.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DisposeProcesses(Process[] processes, string contextName = "")
        {
            if (processes == null) return;

            try
            {
                foreach (var proc in processes)
                {
                    if (proc != null)
                    {
                        DisposeProcess(proc, $"{contextName} (Process ID: {proc.Id})");
                    }
                }

                _logger.Info($"Disposed {processes.Length} processes {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disposing processes {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion

        #region Synchronization Resources

        /// <summary>
        /// Safely disposes a SemaphoreSlim.
        /// </summary>
        /// <param name="semaphore">The semaphore to dispose.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DisposeSemaphore(SemaphoreSlim semaphore, string contextName = "")
        {
            if (semaphore == null) return;

            try
            {
                semaphore.Dispose();
                _logger.Info($"Disposed semaphore {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disposing semaphore {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely disposes a CancellationTokenSource.
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source to dispose.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DisposeCancellationTokenSource(CancellationTokenSource cancellationTokenSource, string contextName = "")
        {
            if (cancellationTokenSource == null) return;

            try
            {
                // Cancel the token source before disposing
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource.Cancel();
                }

                cancellationTokenSource.Dispose();
                _logger.Info($"Canceled and disposed cancellation token source {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disposing cancellation token source {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion

        #region Collection Resources

        /// <summary>
        /// Safely clears and optionally disposes items in a collection.
        /// </summary>
        /// <typeparam name="T">The type of items in the collection.</typeparam>
        /// <param name="collection">The collection to clear.</param>
        /// <param name="disposeItems">Whether to dispose items that implement IDisposable.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void ClearCollection<T>(ICollection<T> collection, bool disposeItems = true, string contextName = "")
        {
            if (collection == null) return;

            try
            {
                if (disposeItems)
                {
                    foreach (var item in collection)
                    {
                        if (item is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch (Exception itemEx)
                            {
                                _logger.Error($"Error disposing item in collection {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", itemEx);
                            }
                        }
                    }
                }

                collection.Clear();
                _logger.Info($"Cleared collection {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error clearing collection {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely clears a dictionary and optionally disposes its values.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to clear.</param>
        /// <param name="disposeValues">Whether to dispose values that implement IDisposable.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void ClearDictionary<TKey, TValue>(IDictionary<TKey, TValue> dictionary, bool disposeValues = true, string contextName = "")
        {
            if (dictionary == null) return;

            try
            {
                if (disposeValues)
                {
                    foreach (var item in dictionary.Values)
                    {
                        if (item is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch (Exception itemEx)
                            {
                                _logger.Error($"Error disposing value in dictionary {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", itemEx);
                            }
                        }
                    }
                }

                dictionary.Clear();
                _logger.Info($"Cleared dictionary {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error clearing dictionary {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion

        #region Event Resources

        /// <summary>
        /// Safely detaches an event handler from an event.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of event arguments.</typeparam>
        /// <param name="eventField">Reference to the event field.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DetachEventHandler<TEventArgs>(ref EventHandler<TEventArgs> eventField, string contextName = "")
            where TEventArgs : EventArgs
        {
            try
            {
                eventField = null;
                _logger.Info($"Detached event handler {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error detaching event handler {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        /// <summary>
        /// Safely detaches an event handler from an event.
        /// </summary>
        /// <param name="eventField">Reference to the event field.</param>
        /// <param name="contextName">Context name for logging.</param>
        public static void DetachEventHandler(ref EventHandler eventField, string contextName = "")
        {
            try
            {
                eventField = null;
                _logger.Info($"Detached event handler {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error detaching event handler {(!string.IsNullOrEmpty(contextName) ? $"for {contextName}" : string.Empty)}", ex);
            }
        }

        #endregion
    }
}
