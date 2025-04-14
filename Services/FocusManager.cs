using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Centralizes focus management to resolve issues between UI focus and logical focus.
    /// </summary>
    public class FocusManager : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<FocusManager>();

        private readonly UniformGrid _buttonGrid;
        private int _currentFocusIndex = 0;
        private Button _lastFocusedButton = null;
        private readonly Window _ownerWindow;
        private readonly object _focusLock = new object();
        private bool _isFocusTemporarilyRedirected = false;

        public event EventHandler<FocusChangedEventArgs> FocusChanged;

        public FocusManager(UniformGrid buttonGrid, Window ownerWindow)
        {
            _buttonGrid = buttonGrid ?? throw new ArgumentNullException(nameof(buttonGrid));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _logger.Info("Initialized with button grid");
        }

        /// <summary>
        /// Gets the currently focused button index.
        /// </summary>
        public int CurrentFocusIndex => _currentFocusIndex;

        /// <summary>
        /// Gets the button that currently has focus.
        /// </summary>
        public Button GetFocusedButton()
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                var buttons = GetAllButtons();
                if (_currentFocusIndex >= 0 && _currentFocusIndex < buttons.Count)
                {
                    return buttons[_currentFocusIndex];
                }
                return null;
            }
        }

        /// <summary>
        /// Sets focus to the specified button index.
        /// </summary>
        public bool SetFocusByIndex(int index)
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                var buttons = GetAllButtons();
                if (index >= 0 && index < buttons.Count)
                {
                    Button button = buttons[index];
                    if (button != null)
                    {
                        _logger.Info($"Setting focus to button at index {index}");
                        _currentFocusIndex = index;
                        UpdateVisualFocus(button);
                        SetActualFocus(button);
                        return true;
                    }
                }
                _logger.Info($"Failed to set focus to index {index}, invalid index");
                return false;
            }
        }

        /// <summary>
        /// Moves focus in the specified direction.
        /// </summary>
        public bool MoveFocus(NavigationDirection direction)
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                var buttons = GetAllButtons();
                if (buttons.Count == 0)
                {
                    _logger.Info("No buttons found in grid");
                    return false;
                }

                // Ensure current index is valid
                if (_currentFocusIndex < 0 || _currentFocusIndex >= buttons.Count)
                {
                    _currentFocusIndex = 0;
                    _logger.Info($"Reset invalid focus index to 0");
                }

                int newIndex = CalculateNewIndex(direction, buttons.Count);
                _logger.Info($"Calculated new index: {newIndex} from current: {_currentFocusIndex}");

                if (newIndex != _currentFocusIndex && newIndex >= 0 && newIndex < buttons.Count)
                {
                    _logger.Info($"Moving focus from {_currentFocusIndex} to {newIndex}");
                    _currentFocusIndex = newIndex;
                    Button button = buttons[newIndex];
                    UpdateVisualFocus(button);

                    // Improve focus setting to be more reliable
                    _ownerWindow.Dispatcher.Invoke(() => {
                        _ownerWindow.UpdateLayout();
                        Keyboard.ClearFocus();
                        button.Focus();
                        Keyboard.Focus(button);
                    });

                    OnFocusChanged(new FocusChangedEventArgs(button, newIndex));
                    return true;
                }
                else
                {
                    _logger.Info($"Unable to move focus - calculated index {newIndex} is invalid or unchanged");
                }

                return false;
            }
        }
        /// <summary>
        /// Sets initial focus to the first button.
        /// </summary>
        public bool SetInitialFocus()
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                var buttons = GetAllButtons();
                if (buttons.Count > 0)
                {
                    _currentFocusIndex = 0;  // Ensure we're setting to the first button (index 0)
                    Button button = buttons[0];
                    _logger.Info("Setting initial focus to first button");

                    // Make sure we clear any previous focus first
                    foreach (var btn in buttons)
                    {
                        if (!(btn.Tag is CommandInfo))
                        {
                            btn.Tag = "NotFocused";
                        }
                    }

                    // Set visual and keyboard focus explicitly
                    UpdateVisualFocus(button);

                    // Force UI update before setting focus
                    _ownerWindow.Dispatcher.Invoke(() => {
                        _ownerWindow.UpdateLayout();
                        SetActualFocus(button);

                        // Add extra focus check to verify focus was set correctly
                        if (!button.IsFocused)
                        {
                            _logger.Info("Focus not set properly, trying again with delay");
                            _ownerWindow.Dispatcher.BeginInvoke(new Action(() => {
                                Keyboard.ClearFocus();
                                button.Focus();
                                Keyboard.Focus(button);
                            }), System.Windows.Threading.DispatcherPriority.Input);
                        }
                    });

                    OnFocusChanged(new FocusChangedEventArgs(button, 0));
                    return true;
                }
                _logger.Info("Cannot set initial focus, no buttons found");
                return false;
            }
        }

        /// <summary>
        /// Temporarily redirects focus away from the button grid.
        /// Use this before showing modal UI elements.
        /// </summary>
        public void BeginFocusRedirection()
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                if (!_isFocusTemporarilyRedirected)
                {
                    _lastFocusedButton = GetFocusedButton();
                    _isFocusTemporarilyRedirected = true;
                    _logger.Info("Beginning focus redirection");
                }
            }
        }

        /// <summary>
        /// Restores focus to the last focused button.
        /// Use this after hiding modal UI elements.
        /// </summary>
        public void EndFocusRedirection()
        {
            ThrowIfDisposed();
            lock (_focusLock)
            {
                if (_isFocusTemporarilyRedirected)
                {
                    _isFocusTemporarilyRedirected = false;
                    _logger.Info("Ending focus redirection");

                    if (_lastFocusedButton != null)
                    {
                        // Ensure button is still in the collection
                        var buttons = GetAllButtons();
                        int index = buttons.IndexOf(_lastFocusedButton);
                        if (index >= 0)
                        {
                            _currentFocusIndex = index;
                            UpdateVisualFocus(_lastFocusedButton);

                            // Use dispatcher to ensure we're on the UI thread and
                            // to give the UI a chance to update before focusing
                            _ownerWindow.Dispatcher.InvokeAsync(() =>
                            {
                                // Ensure we haven't been disposed in the meantime
                                if (!IsDisposed)
                                {
                                    SetActualFocus(_lastFocusedButton);
                                    OnFocusChanged(new FocusChangedEventArgs(_lastFocusedButton, index));
                                }
                            }, System.Windows.Threading.DispatcherPriority.Input);
                        }
                        else
                        {
                            // Button no longer exists, try to focus the current index
                            SetFocusByIndex(_currentFocusIndex);
                        }
                    }
                    else
                    {
                        // No last focused button, try to focus the current index
                        SetFocusByIndex(_currentFocusIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Logs the current focus state for debugging.
        /// </summary>
        public void LogFocusState()
        {
            ThrowIfDisposed();
            try
            {
                var buttons = GetAllButtons();
                int logicallyFocusedIndex = -1;
                int visuallyFocusedIndex = -1;

                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i].IsFocused)
                        logicallyFocusedIndex = i;
                    if (buttons[i].Tag?.ToString() == "VisuallyFocused")
                        visuallyFocusedIndex = i;
                }

                _logger.Info($"Focus State: Current Index = {_currentFocusIndex}, " +
                              $"Logical Focus = {logicallyFocusedIndex}, " +
                              $"Visual Focus = {visuallyFocusedIndex}, " +
                              $"Total Buttons = {buttons.Count}, " +
                              $"Redirected = {_isFocusTemporarilyRedirected}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in LogFocusState: {ex.Message}", ex);
            }
        }
        // Helper method to get all buttons from the grid
        private System.Collections.Generic.List<Button> GetAllButtons()
        {
            var result = new System.Collections.Generic.List<Button>();
            foreach (var child in _buttonGrid.Children)
            {
                if (child is Button button)
                {
                    result.Add(button);
                }
            }
            return result;
        }

        // Helper method to calculate new index based on direction
        private int CalculateNewIndex(NavigationDirection direction, int buttonCount)
        {
            int columns = _buttonGrid.Columns > 0 ? _buttonGrid.Columns : 1;
            int newIndex = _currentFocusIndex;

            // Log the grid layout for debugging
            _logger.Info($"Grid has {columns} columns and {buttonCount} total buttons");

            switch (direction)
            {
                case NavigationDirection.Up:
                    newIndex = _currentFocusIndex - columns;
                    break;
                case NavigationDirection.Down:
                    newIndex = _currentFocusIndex + columns;
                    break;
                case NavigationDirection.Left:
                    // Allow navigation to button 1 (index 0) when at button 2 (index 1)
                    if (_currentFocusIndex > 0)
                    {
                        newIndex = _currentFocusIndex - 1;
                        _logger.Info($"Moving left from {_currentFocusIndex} to {newIndex}");
                    }
                    break;
                case NavigationDirection.Right:
                    if (_currentFocusIndex < buttonCount - 1)
                    {
                        newIndex = _currentFocusIndex + 1;
                        _logger.Info($"Moving right from {_currentFocusIndex} to {newIndex}");
                    }
                    break;
            }

            // Ensure the new index is within bounds
            if (newIndex < 0 || newIndex >= buttonCount)
            {
                _logger.Info($"Calculated index {newIndex} is out of bounds, staying at {_currentFocusIndex}");
                return _currentFocusIndex; // No change
            }

            return newIndex;
        }

        // Helper method to update visual focus indicators
        private void UpdateVisualFocus(Button button)
        {
            if (button == null)
                return;

            // Clear visual focus from all buttons
            var buttons = GetAllButtons();
            foreach (var btn in buttons)
            {
                if (!(btn.Tag is CommandInfo))
                {
                    btn.Tag = "NotFocused";
                }
            }

            // Set visual focus on the target button
            button.Tag = "VisuallyFocused";
        }

        // Helper method to set actual keyboard focus
        private void SetActualFocus(Button button)
        {
            if (button == null)
                return;

            try
            {
                // Clear focus from other elements first
                Keyboard.ClearFocus();

                // Set keyboard focus using both WPF mechanisms
                button.Focus();
                Keyboard.Focus(button);

                // Ensure the window is updated
                _ownerWindow.UpdateLayout();

                // Log the focus state
                _logger.Info($"SetActualFocus called for button, IsFocused = {button.IsFocused}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error setting actual focus: {ex.Message}", ex);
            }
        }

        // Helper method to raise the FocusChanged event
        private void OnFocusChanged(FocusChangedEventArgs e)
        {
            FocusChanged?.Invoke(this, e);
        }

        protected override void ReleaseManagedResources()
        {
            try
            {
                _logger.Info("Beginning resource cleanup");

                // Clear event handlers
                try
                {
                    FocusChanged = null;
                    _logger.Info("Cleared event handlers");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error clearing event handlers", ex);
                }

                // Clear button references
                try
                {
                    _lastFocusedButton = null;
                    _logger.Info("Cleared button references");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error clearing button references", ex);
                }

                // Reset state
                try
                {
                    _isFocusTemporarilyRedirected = false;
                    _currentFocusIndex = 0;
                    _logger.Info("Reset state variables");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error resetting state variables", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in ReleaseManagedResources", ex);
            }
            finally
            {
                _logger.Info("Resource cleanup completed");
            }
        }
    }

    /// <summary>
    /// Represents direction for focus navigation.
    /// </summary>
    public enum NavigationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Event arguments for focus changes.
    /// </summary>
    public class FocusChangedEventArgs : EventArgs
    {
        public Button FocusedButton { get; }
        public int FocusIndex { get; }

        public FocusChangedEventArgs(Button focusedButton, int focusIndex)
        {
            FocusedButton = focusedButton;
            FocusIndex = focusIndex;
        }
    }
}