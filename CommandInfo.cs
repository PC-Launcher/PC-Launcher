using PCStreamerLauncher.Logging;

namespace PCStreamerLauncher
{
    /// <summary>
    /// Represents a command to launch an application.
    /// For web apps, RawCommand is a URL; for local apps, it's an executable path.
    /// </summary>
    public class CommandInfo
    {
        private static readonly ContextLogger _logger = Logger.GetLogger(nameof(CommandInfo));

        private string _rawCommand;
        public string RawCommand
        {
            get { return _rawCommand; }
            set
            {
                _logger.Trace($"Setting RawCommand: {value}");
                _rawCommand = value;
            }
        }

        private bool _isWeb;
        public bool IsWeb
        {
            get { return _isWeb; }
            set
            {
                _logger.Trace($"Setting IsWeb: {value}");
                _isWeb = value;
            }
        }
    }
}

