using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Globalization;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    public class GamepadHandler : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<GamepadHandler>();

        public event EventHandler<GamepadEventArgs> GamepadButtonPressed;
        public event EventHandler<GamepadEventArgs> StartButtonPressed;
        private Timer _pollTimer;
        private XInputState _previousState;
        private DateTime _lastTerminateButtonTime = DateTime.MinValue;
        private DateTime _lastStartButtonTime = DateTime.MinValue;
        private const int TerminateButtonDelayMs = 3000;
        private const int StartButtonDelayMs = 2000;

        // For directional buttons, track if they are currently held
        private readonly Dictionary<string, bool> _directionActive = new Dictionary<string, bool>
        {
            { "Up", false },
            { "Down", false },
            { "Left", false },
            { "Right", false },
            { "Start", false }
        };

        private ushort ACTION_BUTTON = 0x1000;
        private ushort TERMINATE_BUTTON = 0x2000;
        private ushort DPAD_UP = 0x0001;
        private ushort DPAD_DOWN = 0x0002;
        private ushort DPAD_LEFT = 0x0004;
        private ushort DPAD_RIGHT = 0x0008;
        private ushort START_BUTTON = 0x0010;
        private bool _isTerminateEventInProgress = false;

        public GamepadHandler() : this(null) { }

        public GamepadHandler(Dictionary<string, string> gamepadConfig)
        {
            if (gamepadConfig != null)
            {
                LoadButtonMappings(gamepadConfig);
            }
            InitializeTimer();
        }

        private void LoadButtonMappings(Dictionary<string, string> gamepadConfig)
        {
            try
            {
                ACTION_BUTTON = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.ActionButton, 0x1000);
                TERMINATE_BUTTON = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.TerminateButton, 0x2000);
                DPAD_UP = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.UpButton, 0x0001);
                DPAD_DOWN = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.DownButton, 0x0002);
                DPAD_LEFT = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.LeftButton, 0x0004);
                DPAD_RIGHT = ParseHexValue(gamepadConfig, ConfigKeys.Gamepad.RightButton, 0x0008);
                START_BUTTON = ParseHexValue(gamepadConfig, "StartButton", 0x0010);
                _logger.Info("Loaded button mappings from config");
                _logger.Info($"Action: 0x{ACTION_BUTTON:X4}, Terminate: 0x{TERMINATE_BUTTON:X4}");
                _logger.Info($"Up: 0x{DPAD_UP:X4}, Down: 0x{DPAD_DOWN:X4}, Left: 0x{DPAD_LEFT:X4}, Right: 0x{DPAD_RIGHT:X4}");
                _logger.Info($"Start: 0x{START_BUTTON:X4}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading button mappings - {ex.Message}", ex);
            }
        }

        private ushort ParseHexValue(Dictionary<string, string> config, string key, ushort defaultValue)
        {
            if (config.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    value = value.Trim().ToLowerInvariant();
                    int commentIndex = value.IndexOf(';');
                    if (commentIndex > 0)
                        value = value.Substring(0, commentIndex).Trim();
                    if (value.StartsWith("0x"))
                        value = value.Substring(2);
                    if (ushort.TryParse(value, NumberStyles.HexNumber, null, out ushort result))
                        return result;
                    else
                        _logger.Warning($"Unable to parse hex value '{value}' for {key}, using default: 0x{defaultValue:X4}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error parsing value for {key}: {ex.Message}", ex);
                }
            }
            return defaultValue;
        }

        private void InitializeTimer()
        {
            _pollTimer = new Timer(50);
            _pollTimer.Elapsed += PollTimer_Elapsed;
            _pollTimer.Start();
            _logger.Info("Initialized and polling started");
            XInputState state = new XInputState();
            int result = XInputGetState(0, ref state);
            _logger.Info($"Initial controller check - Connected: {result == 0}");
        }

        private void PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsDisposed)
                return;
            XInputState state = new XInputState();
            int result = XInputGetState(0, ref state);
            if (result == 0)
            {
                // Process non-directional buttons as before
                CheckButtonTransition(state, _previousState, ACTION_BUTTON, "A");
                CheckTerminateButtonTransition(state, _previousState);
                
                // Process directional buttons and Start button with edge detection based on our active state
                ProcessDirectional("Up", state.Gamepad.wButtons, DPAD_UP);
                ProcessDirectional("Down", state.Gamepad.wButtons, DPAD_DOWN);
                ProcessDirectional("Left", state.Gamepad.wButtons, DPAD_LEFT);
                ProcessDirectional("Right", state.Gamepad.wButtons, DPAD_RIGHT);
                ProcessDirectional("Start", state.Gamepad.wButtons, START_BUTTON);

                _previousState = state;
            }
        }

        private void CheckButtonTransition(XInputState current, XInputState previous, ushort buttonMask, string buttonName)
        {
            bool isPressed = (current.Gamepad.wButtons & buttonMask) != 0;
            bool wasPressedBefore = (previous.Gamepad.wButtons & buttonMask) != 0;
            
            if (isPressed && !wasPressedBefore)
            {
                FireButtonEvent(buttonName);
            }
        }



        private void CheckTerminateButtonTransition(XInputState current, XInputState previous)
        {
            bool isPressed = (current.Gamepad.wButtons & TERMINATE_BUTTON) != 0;
            bool wasPressedBefore = (previous.Gamepad.wButtons & TERMINATE_BUTTON) != 0;
            if (isPressed && !wasPressedBefore && !_isTerminateEventInProgress &&
                (DateTime.Now - _lastTerminateButtonTime).TotalMilliseconds > TerminateButtonDelayMs)
            {
                _logger.Info("Terminate button press detected and debounced");
                _lastTerminateButtonTime = DateTime.Now;
                _isTerminateEventInProgress = true;
                try
                {
                    FireButtonEvent("Y");
                }
                finally
                {
                    Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        if (IsDisposed)
                            return;
                        await Task.Delay(2000);
                        _isTerminateEventInProgress = false;
                        _logger.Info("Terminate event processing complete, allowing new terminate events");
                    });
                }
            }
        }

        private void ProcessDirectional(string direction, ushort currentButtons, ushort mask)
        {
            bool isPressed = (currentButtons & mask) != 0;
            // Only fire if button becomes pressed and wasn't already active
            if (isPressed && !_directionActive[direction])
            {
                // Add special handling for Start button to prevent rapid toggling
                if (direction == "Start" && 
                    (DateTime.Now - _lastStartButtonTime).TotalMilliseconds < StartButtonDelayMs)
                {
                    _logger.Info($"Start button press ignored due to debounce ({(DateTime.Now - _lastStartButtonTime).TotalMilliseconds}ms < {StartButtonDelayMs}ms)");
                    _directionActive[direction] = true;
                    return;
                }
                
                if (direction == "Start")
                {
                    _lastStartButtonTime = DateTime.Now;
                    _logger.Info($"Start button debounced and accepted");
                }
                
                FireButtonEvent(direction);
                _directionActive[direction] = true;
            }
            else if (!isPressed && _directionActive[direction])
            {
                // Reset state when button is released
                _directionActive[direction] = false;
            }
        }

        private void FireButtonEvent(string button)
        {
            _logger.Info($"Button press detected - {button}");
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (!IsDisposed)
                {
                    GamepadButtonPressed?.Invoke(this, new GamepadEventArgs(button));
                    
                    // Also fire the specific event for Start button, but only if it's not debounced
                    if (button == "Start")
                    {
                        // The StartButtonPressed event is specifically for handling the help overlay
                        // We don't check debounce time here because that's already handled in ProcessDirectional
                        StartButtonPressed?.Invoke(this, new GamepadEventArgs(button));
                    }
                }
            });
        }

        protected override void ReleaseManagedResources()
        {
            try
            {
                _logger.Info("Beginning resource cleanup");

                // Clean up timer
                try
                {
                    if (_pollTimer != null)
                    {
                        _pollTimer.Elapsed -= PollTimer_Elapsed;
                        _pollTimer.Stop();
                        _pollTimer.Dispose();
                        _pollTimer = null;
                        _logger.Info("Poll timer stopped and disposed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error disposing poll timer", ex);
                }

                // Clear event handlers
                try
                {
                    GamepadButtonPressed = null;
                    StartButtonPressed = null;
                    _logger.Info("Event handlers cleared");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error clearing event handlers", ex);
                }

                // Clear state tracking
                try
                {
                    _directionActive.Clear();
                    _isTerminateEventInProgress = false;
                    _logger.Info("State tracking cleared");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error clearing state tracking", ex);
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

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState(int dwUserIndex, ref XInputState pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }
    }

    public class GamepadEventArgs : EventArgs
    {
        public string Button { get; }
        public GamepadEventArgs(string button)
        {
            Button = button;
        }
    }
}