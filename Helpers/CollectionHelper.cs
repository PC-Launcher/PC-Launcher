using System;
using System.Collections.Generic;
using System.Linq;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides helper methods for safely managing collections and event handlers.
    /// </summary>
    public static class CollectionHelper
    {
        /// <summary>
        /// Safely clears a collection with proper error handling and logging.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection</typeparam>
        /// <param name="collection">The collection to clear</param>
        /// <param name="logger">The logger to use for logging operations</param>
        /// <param name="collectionName">A descriptive name for the collection (for logging)</param>
        /// <returns>True if the collection was cleared successfully, false otherwise</returns>
        public static bool SafelyClearCollection<T>(
            ICollection<T> collection,
            ContextLogger logger,
            string collectionName = "collection")
        {
            if (collection == null)
            {
                logger?.Warning($"Cannot clear {collectionName}: Collection is null");
                return false;
            }

            try
            {
                logger?.Debug($"Clearing {collectionName} ({collection.Count} items)");
                
                // If collection contains disposable items, dispose them first
                if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
                {
                    foreach (var item in collection)
                    {
                        if (item is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                                logger?.Trace($"Disposed item in {collectionName}");
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"Error disposing item in {collectionName}", ex);
                            }
                        }
                    }
                }

                collection.Clear();
                logger?.Debug($"Successfully cleared {collectionName}");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error clearing {collectionName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely removes an event handler with proper error handling and logging.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of event arguments</typeparam>
        /// <param name="eventHandler">The event handler to remove</param>
        /// <param name="handlerName">A descriptive name for the event handler (for logging)</param>
        /// <param name="logger">The logger to use for logging operations</param>
        /// <returns>True if the event handler was removed successfully, false otherwise</returns>
        public static bool SafelyRemoveEventHandler<TEventArgs>(
            ref EventHandler<TEventArgs> eventHandler,
            string handlerName,
            ContextLogger logger)
        {
            try
            {
                if (eventHandler != null)
                {
                    logger?.Debug($"Removing event handler: {handlerName}");
                    
                    // The delegate invocation list contains all handlers
                    int handlerCount = eventHandler.GetInvocationList().Length;
                    
                    // Set to null to remove all handlers
                    eventHandler = null;
                    
                    logger?.Debug($"Successfully removed {handlerCount} handlers for {handlerName}");
                    return true;
                }
                
                logger?.Debug($"Event handler {handlerName} is already null, nothing to remove");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error removing event handler {handlerName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely removes an event handler with proper error handling and logging.
        /// </summary>
        /// <param name="eventHandler">The event handler to remove</param>
        /// <param name="handlerName">A descriptive name for the event handler (for logging)</param>
        /// <param name="logger">The logger to use for logging operations</param>
        /// <returns>True if the event handler was removed successfully, false otherwise</returns>
        public static bool SafelyRemoveEventHandler(
            ref EventHandler eventHandler,
            string handlerName,
            ContextLogger logger)
        {
            try
            {
                if (eventHandler != null)
                {
                    logger?.Debug($"Removing event handler: {handlerName}");
                    
                    // The delegate invocation list contains all handlers
                    int handlerCount = eventHandler.GetInvocationList().Length;
                    
                    // Set to null to remove all handlers
                    eventHandler = null;
                    
                    logger?.Debug($"Successfully removed {handlerCount} handlers for {handlerName}");
                    return true;
                }
                
                logger?.Debug($"Event handler {handlerName} is already null, nothing to remove");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"Error removing event handler {handlerName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely disposes all resources in a collection with proper error handling and logging.
        /// </summary>
        /// <typeparam name="T">The type of disposable resources</typeparam>
        /// <param name="resources">The collection of resources to dispose</param>
        /// <param name="logger">The logger to use for logging operations</param>
        /// <returns>True if all resources were disposed successfully, false if any errors occurred</returns>
        public static bool SafelyDisposeResourcesInList<T>(
            IEnumerable<T> resources,
            ContextLogger logger) where T : IDisposable
        {
            if (resources == null)
            {
                logger?.Warning("Cannot dispose resources: Collection is null");
                return false;
            }

            bool allSuccessful = true;
            int resourceCount = 0;
            int successCount = 0;

            try
            {
                // Convert to list to avoid potential enumeration issues if collection changes
                var resourceList = resources.ToList();
                resourceCount = resourceList.Count;
                
                logger?.Debug($"Disposing {resourceCount} resources");

                foreach (var resource in resourceList)
                {
                    try
                    {
                        if (resource != null)
                        {
                            resource.Dispose();
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        allSuccessful = false;
                        logger?.Error("Error disposing resource", ex);
                    }
                }

                logger?.Debug($"Disposed {successCount} of {resourceCount} resources successfully");
                return allSuccessful;
            }
            catch (Exception ex)
            {
                logger?.Error("Error during resource disposal process", ex);
                return false;
            }
        }
    }
}
