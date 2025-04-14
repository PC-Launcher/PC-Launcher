using System;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides centralized, robust methods for event handling with consistent error management.
    /// </summary>
    public static class EventHandlingHelper
    {
        /// <summary>
        /// Handles and logs event-related errors.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="ex">Exception that occurred during event handling</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void HandleEventError(
            string componentName,
            string eventName,
            Exception ex,
            ContextLogger logger)
        {
            string message = $"Event Handling Error: {componentName} failed to handle {eventName} event";
            logger.Error(message, ex);
        }

        /// <summary>
        /// Safely handles an event with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="eventHandler">The event handler to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if the event was handled successfully, false otherwise</returns>
        public static bool SafelyHandleEvent(
            string componentName,
            string eventName,
            Action eventHandler,
            ContextLogger logger)
        {
            try
            {
                logger.Trace($"Event Handling Started: {componentName} handling {eventName}");
                eventHandler();
                logger.Trace($"Event Handling Completed: {componentName} handling {eventName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex, logger);
                return false;
            }
        }

        /// <summary>
        /// Safely handles an async event with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component handling the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="eventHandlerAsync">The async event handler to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyHandleEventAsync(
            string componentName,
            string eventName,
            Func<Task> eventHandlerAsync,
            ContextLogger logger)
        {
            try
            {
                logger.Trace($"Async Event Handling Started: {componentName} handling {eventName}");
                await eventHandlerAsync();
                logger.Trace($"Async Event Handling Completed: {componentName} handling {eventName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex, logger);
                return false;
            }
        }

        /// <summary>
        /// Safely detaches an event handler with logging.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of event arguments</typeparam>
        /// <param name="eventField">Reference to the event field</param>
        /// <param name="componentName">Name of the component</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void SafelyDetachEventHandler<TEventArgs>(
            ref EventHandler<TEventArgs> eventField,
            string componentName,
            string eventName,
            ContextLogger logger)
        {
            try
            {
                eventField = null;
                logger.Info($"Detached event handler {eventName} for {componentName}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error detaching event handler {eventName} for {componentName}", ex);
            }
        }

        /// <summary>
        /// Safely detaches a standard event handler with logging.
        /// </summary>
        /// <param name="eventField">Reference to the event field</param>
        /// <param name="componentName">Name of the component</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void SafelyDetachEventHandler(
            ref EventHandler eventField,
            string componentName,
            string eventName,
            ContextLogger logger)
        {
            try
            {
                eventField = null;
                logger.Info($"Detached event handler {eventName} for {componentName}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error detaching event handler {eventName} for {componentName}", ex);
            }
        }

        /// <summary>
        /// Creates a thread-safe event invoker with error handling.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of event arguments</typeparam>
        /// <param name="eventHandler">The event handler to invoke</param>
        /// <param name="sender">The sender of the event</param>
        /// <param name="args">The event arguments</param>
        /// <param name="componentName">Name of the component raising the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void SafelyInvokeEvent<TEventArgs>(
            EventHandler<TEventArgs> eventHandler,
            object sender,
            TEventArgs args,
            string componentName,
            string eventName,
            ContextLogger logger)
            where TEventArgs : EventArgs
        {
            if (eventHandler == null) return;

            try
            {
                logger.Trace($"Invoking event {eventName} for {componentName}");
                eventHandler(sender, args);
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex, logger);
            }
        }

        /// <summary>
        /// Creates a thread-safe standard event invoker with error handling.
        /// </summary>
        /// <param name="eventHandler">The event handler to invoke</param>
        /// <param name="sender">The sender of the event</param>
        /// <param name="args">The event arguments</param>
        /// <param name="componentName">Name of the component raising the event</param>
        /// <param name="eventName">Name of the event</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void SafelyInvokeEvent(
            EventHandler eventHandler,
            object sender,
            EventArgs args,
            string componentName,
            string eventName,
            ContextLogger logger)
        {
            if (eventHandler == null) return;

            try
            {
                logger.Trace($"Invoking event {eventName} for {componentName}");
                eventHandler(sender, args);
            }
            catch (Exception ex)
            {
                HandleEventError(componentName, eventName, ex, logger);
            }
        }
    }
}
