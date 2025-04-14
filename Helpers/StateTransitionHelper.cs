using System;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher.Helpers
{
    /// <summary>
    /// Provides centralized, robust methods for state transition operations with consistent error handling.
    /// </summary>
    public static class StateTransitionHelper
    {
        /// <summary>
        /// Handles and logs state transition errors.
        /// </summary>
        /// <param name="componentName">Name of the component experiencing the state transition error</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="ex">Exception that occurred during state transition</param>
        /// <param name="logger">Context logger for detailed logging</param>
        public static void HandleStateTransitionError(
            string componentName,
            string fromState,
            string toState,
            Exception ex,
            ContextLogger logger)
        {
            string message = $"State Transition Error: {componentName} failed to transition from {fromState} to {toState}";
            logger.Error(message, ex);
        }

        /// <summary>
        /// Safely performs a state transition with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component performing the state transition</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="transitionAction">The state transition action to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if the transition completed successfully, false otherwise</returns>
        public static bool SafelyExecuteStateTransition(
            string componentName,
            string fromState,
            string toState,
            Action transitionAction,
            ContextLogger logger)
        {
            try
            {
                logger.Debug($"State Transition Started: {componentName} from {fromState} to {toState}");
                transitionAction();
                logger.Debug($"State Transition Completed: {componentName} from {fromState} to {toState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, fromState, toState, ex, logger);
                return false;
            }
        }

        /// <summary>
        /// Safely performs an async state transition with proper error handling.
        /// </summary>
        /// <param name="componentName">Name of the component performing the state transition</param>
        /// <param name="fromState">Current state before transition</param>
        /// <param name="toState">Target state after transition</param>
        /// <param name="transitionAsyncAction">The async state transition action to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>A task representing the asynchronous operation, which returns true if successful</returns>
        public static async Task<bool> SafelyExecuteStateTransitionAsync(
            string componentName,
            string fromState,
            string toState,
            Func<Task> transitionAsyncAction,
            ContextLogger logger)
        {
            try
            {
                logger.Debug($"Async State Transition Started: {componentName} from {fromState} to {toState}");
                await transitionAsyncAction();
                logger.Debug($"Async State Transition Completed: {componentName} from {fromState} to {toState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, fromState, toState, ex, logger);
                return false;
            }
        }

        /// <summary>
        /// Creates a safe state transition method with additional validation and logging.
        /// </summary>
        /// <param name="componentName">Name of the component performing the state transition</param>
        /// <param name="currentState">Current state of the component</param>
        /// <param name="targetState">Target state for transition</param>
        /// <param name="transitionValidation">Validation method to check if transition is allowed</param>
        /// <param name="transitionAction">The state transition action to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>True if the transition is valid and completed successfully, false otherwise</returns>
        public static bool SafelyTransitionState(
            string componentName,
            string currentState,
            string targetState,
            Func<string, string, bool> transitionValidation,
            Action transitionAction,
            ContextLogger logger)
        {
            try
            {
                // Validate state transition
                if (!transitionValidation(currentState, targetState))
                {
                    logger.Warning($"Invalid state transition attempted: {componentName} from {currentState} to {targetState}");
                    return false;
                }

                // Execute transition
                logger.Debug($"Validated State Transition: {componentName} from {currentState} to {targetState}");
                transitionAction();
                logger.Debug($"State Transition Completed: {componentName} from {currentState} to {targetState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, currentState, targetState, ex, logger);
                return false;
            }
        }

        /// <summary>
        /// Creates a safe async state transition method with additional validation and logging.
        /// </summary>
        /// <param name="componentName">Name of the component performing the state transition</param>
        /// <param name="currentState">Current state of the component</param>
        /// <param name="targetState">Target state for transition</param>
        /// <param name="transitionValidation">Validation method to check if transition is allowed</param>
        /// <param name="transitionAsyncAction">The async state transition action to execute</param>
        /// <param name="logger">Context logger for detailed logging</param>
        /// <returns>A task representing the asynchronous operation, which returns true if the transition is valid and successful</returns>
        public static async Task<bool> SafelyTransitionStateAsync(
            string componentName,
            string currentState,
            string targetState,
            Func<string, string, bool> transitionValidation,
            Func<Task> transitionAsyncAction,
            ContextLogger logger)
        {
            try
            {
                // Validate state transition
                if (!transitionValidation(currentState, targetState))
                {
                    logger.Warning($"Invalid state transition attempted: {componentName} from {currentState} to {targetState}");
                    return false;
                }

                // Execute transition
                logger.Debug($"Validated State Transition: {componentName} from {currentState} to {targetState}");
                await transitionAsyncAction();
                logger.Debug($"State Transition Completed: {componentName} from {currentState} to {targetState}");
                return true;
            }
            catch (Exception ex)
            {
                HandleStateTransitionError(componentName, currentState, targetState, ex, logger);
                return false;
            }
        }
    }
}
