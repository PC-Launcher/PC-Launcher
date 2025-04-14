using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using PCStreamerLauncher.Logging;
using PCStreamerLauncher.Helpers;

namespace PCStreamerLauncher
{
    public class SoundManager : DisposableBase
    {
        private readonly ContextLogger _logger = Logger.GetLogger<SoundManager>();
        private readonly Dictionary<string, SoundPlayer> _soundPlayers = new Dictionary<string, SoundPlayer>();
        private bool _soundEnabled = true;
        private readonly string _soundsDirectory;
        private readonly object _loadLock = new object();

        public bool IsSoundEnabled => _soundEnabled;

        public SoundManager(string soundsDirectoryPath)
        {
            _soundsDirectory = soundsDirectoryPath;
            _logger.Info($"Initializing with sounds directory: {_soundsDirectory}");

            if (!Directory.Exists(_soundsDirectory))
            {
                FileOperationHelper.SafelyExecuteFileOperation(
                    "creating sounds directory",
                    _soundsDirectory,
                    () => Directory.CreateDirectory(_soundsDirectory),
                    _logger);
            }
            LoadSoundsFromDirectory(_soundsDirectory);
        }

        public void LoadSoundsFromDirectory(string directoryPath)
        {
            ThrowIfDisposed();

            ErrorHelper.ExecuteWithLogging(
                $"Loading sounds from directory {directoryPath}",
                () =>
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        _logger.Warning($"Directory '{directoryPath}' does not exist.");
                        return;
                    }

                    var soundFiles = Directory.GetFiles(directoryPath, "*.wav");
                    _logger.Info($"Found {soundFiles.Length} sound files in {directoryPath}");

                    foreach (var file in soundFiles)
                    {
                        var soundName = Path.GetFileNameWithoutExtension(file);
                        _ = LoadSoundAsync(soundName, file);
                    }
                },
                _logger);
        }

        private async Task<bool> LoadSoundAsync(string soundName, string filePath)
        {
            ThrowIfDisposed();

            return await ErrorHelper.ExecuteAsyncWithLogging(
                $"Loading sound '{soundName}' from {filePath}",
                async () =>
                {
                    if (!File.Exists(filePath))
                    {
                        _logger.Warning($"Sound file not found: {filePath}");
                        return false;
                    }

                    lock (_loadLock)
                    {
                        if (_soundPlayers.TryGetValue(soundName, out SoundPlayer existingPlayer))
                        {
                            existingPlayer.Dispose();
                            _soundPlayers.Remove(soundName);
                            _logger.Info($"Disposed existing player for sound '{soundName}'");
                        }
                    }

                    byte[] soundData = await Task.Run(() =>
                    {
                        return FileOperationHelper.SafelyExecuteFileOperation(
                            "reading sound file",
                            filePath,
                            () => File.ReadAllBytes(filePath),
                            _logger,
                            false) ? File.ReadAllBytes(filePath) : null;
                    });

                    if (soundData == null)
                    {
                        return false;
                    }

                    var player = await Task.Run(() =>
                    {
                        return ErrorHelper.ExecuteWithLogging(
                            $"initializing sound player for '{soundName}'",
                            () =>
                            {
                                var stream = new MemoryStream(soundData);
                                var newPlayer = new SoundPlayer(stream);
                                newPlayer.Load();
                                return newPlayer;
                            },
                            _logger,
                            null);
                    });

                    if (player == null)
                    {
                        return false;
                    }

                    lock (_loadLock)
                    {
                        if (!IsDisposed)
                        {
                            _soundPlayers[soundName] = player;
                            _logger.Info($"Successfully loaded sound '{soundName}' from {filePath}");
                            return true;
                        }
                        else
                        {
                            player.Dispose();
                            return false;
                        }
                    }
                },
                _logger,
                false);
        }

        /// <summary>
        /// Plays a sound asynchronously.
        /// </summary>
        /// <param name="soundName">Name of the sound to play</param>
        public void PlaySound(string soundName)
        {
            ThrowIfDisposed();

            ErrorHelper.ExecuteWithLogging(
                $"Setting up sound '{soundName}' for playback",
                () =>
                {
                    if (!_soundEnabled)
                    {
                        _logger.Info($"Sound '{soundName}' not played because sound is disabled");
                        return;
                    }

                    SoundPlayer player = null;
                    bool needsLoading = false;

                    lock (_loadLock)
                    {
                        if (_soundPlayers.TryGetValue(soundName, out player))
                        {
                        }
                        else
                        {
                            needsLoading = true;
                        }
                    }

                    // Use the fire-and-forget pattern with proper error handling
                    PlaySoundAsync(soundName, player, needsLoading).FireAndForgetWithLogging(_logger);
                },
                _logger);
        }

        /// <summary>
        /// Internal async implementation to play a sound.
        /// </summary>
        private async Task PlaySoundAsync(string soundName, SoundPlayer player, bool needsLoading)
        {
            await ErrorHelper.ExecuteAsyncWithLogging(
                $"Playing sound '{soundName}'",
                async () =>
                {
                    if (needsLoading)
                    {
                        string filePath = Path.Combine(_soundsDirectory, $"{soundName}.wav");

                        if (File.Exists(filePath))
                        {
                            _logger.Info($"Sound '{soundName}' not loaded yet, loading on-demand");
                            bool loaded = await LoadSoundAsync(soundName, filePath);
                            if (!loaded)
                            {
                                _logger.Warning($"Failed to load sound '{soundName}' on-demand");
                                return;
                            }

                            if (!_soundEnabled || IsDisposed)
                            {
                                _logger.Info($"Sound '{soundName}' loaded but playback now disabled or disposed");
                                return;
                            }

                            lock (_loadLock)
                            {
                                if (_soundPlayers.TryGetValue(soundName, out SoundPlayer loadedPlayer))
                                {
                                    player = loadedPlayer;
                                }
                                else
                                {
                                    _logger.Warning($"Failed to retrieve loaded sound '{soundName}'");
                                    return;
                                }
                            }
                        }
                        else
                        {
                            _logger.Warning($"Sound file '{soundName}.wav' not found in {_soundsDirectory}");
                            return;
                        }
                    }

                    // SoundPlayer.Play is a quick operation that doesn't block for long,
                    // so we can call it directly without Task.Run
                    ErrorHelper.ExecuteWithLogging(
                        $"Playing sound '{soundName}'",
                        () =>
                        {
                            player.Play();
                            _logger.Info($"Played sound '{soundName}'");
                        },
                        _logger);
                },
                _logger);
        }

        public void SetEnabled(bool enabled)
        {
            ThrowIfDisposed();
            _soundEnabled = enabled;
            _logger.Info($"Sound playback {(enabled ? "enabled" : "disabled")}");
        }

        public void UnloadSound(string soundName)
        {
            ThrowIfDisposed();

            ErrorHelper.ExecuteWithLogging(
                $"Unloading sound '{soundName}'",
                () =>
                {
                    lock (_loadLock)
                    {
                        if (_soundPlayers.TryGetValue(soundName, out SoundPlayer player))
                        {
                            player.Dispose();
                            _soundPlayers.Remove(soundName);
                            _logger.Info($"Unloaded sound '{soundName}'");
                        }
                    }
                },
                _logger);
        }

        protected override void ReleaseManagedResources()
        {
            ErrorHelper.ExecuteWithLogging(
                "Releasing managed resources in SoundManager",
                () =>
                {
                    _logger.Info("Beginning resource cleanup");

                    // Dispose all sound players
                    ErrorHelper.SafelyCleanupResource(
                        nameof(SoundManager),
                        "sound players",
                        () =>
                        {
                            lock (_loadLock)
                            {
                                // First stop all currently playing sounds
                                foreach (var playerEntry in _soundPlayers)
                                {
                                    ErrorHelper.SafelyCleanupResource(
                                        nameof(SoundManager),
                                        $"sound '{playerEntry.Key}'",
                                        () =>
                                        {
                                            string soundName = playerEntry.Key;
                                            SoundPlayer player = playerEntry.Value;

                                            if (player != null)
                                            {
                                                // Stop the sound first to prevent hanging during disposal
                                                player.Stop();
                                                _logger.Info($"Stopped playback for sound '{soundName}'");
                                            }
                                        });
                                }

                                // Now clear and dispose all sound players
                                CollectionHelper.SafelyClearCollection(
                                    _soundPlayers,
                                    _logger,
                                    "SoundManager._soundPlayers");
                            }
                        });
                },
                _logger);
        }

        protected override void ReleaseUnmanagedResources()
        {
            // SoundManager primarily uses managed resources,
            // but this method is implemented to ensure any potential
            // unmanaged resources from the SoundPlayer are properly released
            ErrorHelper.ExecuteWithLogging(
                "Releasing unmanaged resources in SoundManager",
                () =>
                {
                    _logger.Info("Releasing unmanaged resources");

                    // Force a garbage collection after disposing sound players
                    // to ensure any native COM objects are released
                    ErrorHelper.SafelyCleanupResource(
                        nameof(SoundManager),
                        "audio resources via GC",
                        () =>
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            _logger.Info("GC collect completed to release unmanaged audio resources");
                        });
                },
                _logger);
        }
    }
}