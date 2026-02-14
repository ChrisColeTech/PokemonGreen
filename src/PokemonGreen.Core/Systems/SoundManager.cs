namespace PokemonGreen.Core.Systems;

/// <summary>
/// Singleton audio manager for background music and sound effects.
/// Provides the interface for audio playback; actual audio loading and
/// playback implementation is handled by the game project.
/// </summary>
public class SoundManager
{
    private static SoundManager? _instance;
    private static readonly object _lock = new();

    private float _bgmVolume = 1.0f;
    private float _sfxVolume = 1.0f;
    private string? _currentBGM;

    /// <summary>
    /// Gets the singleton instance of the SoundManager.
    /// </summary>
    public static SoundManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SoundManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the current BGM volume (0.0 to 1.0).
    /// </summary>
    public float BGMVolume => _bgmVolume;

    /// <summary>
    /// Gets the current SFX volume (0.0 to 1.0).
    /// </summary>
    public float SFXVolume => _sfxVolume;

    /// <summary>
    /// Gets the name of the currently playing BGM, or null if none.
    /// </summary>
    public string? CurrentBGM => _currentBGM;

    /// <summary>
    /// Event raised when BGM should start playing.
    /// The game project should subscribe to this and handle actual audio playback.
    /// </summary>
    public event Action<string, float>? OnPlayBGM;

    /// <summary>
    /// Event raised when BGM should stop.
    /// </summary>
    public event Action? OnStopBGM;

    /// <summary>
    /// Event raised when a sound effect should play.
    /// </summary>
    public event Action<string, float>? OnPlaySFX;

    /// <summary>
    /// Event raised when BGM volume changes.
    /// </summary>
    public event Action<float>? OnBGMVolumeChanged;

    /// <summary>
    /// Event raised when SFX volume changes.
    /// </summary>
    public event Action<float>? OnSFXVolumeChanged;

    /// <summary>
    /// Private constructor for singleton pattern.
    /// </summary>
    private SoundManager()
    {
    }

    /// <summary>
    /// Plays background music in a loop.
    /// </summary>
    /// <param name="name">The name/identifier of the music track to play.</param>
    public void PlayBGM(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        // Don't restart if already playing the same track
        if (_currentBGM == name)
            return;

        _currentBGM = name;
        OnPlayBGM?.Invoke(name, _bgmVolume);
    }

    /// <summary>
    /// Stops the currently playing background music.
    /// </summary>
    public void StopBGM()
    {
        _currentBGM = null;
        OnStopBGM?.Invoke();
    }

    /// <summary>
    /// Plays a one-shot sound effect.
    /// </summary>
    /// <param name="name">The name/identifier of the sound effect to play.</param>
    public void PlaySFX(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        OnPlaySFX?.Invoke(name, _sfxVolume);
    }

    /// <summary>
    /// Sets the background music volume.
    /// </summary>
    /// <param name="volume">Volume level from 0.0 (silent) to 1.0 (full volume).</param>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Math.Clamp(volume, 0.0f, 1.0f);
        OnBGMVolumeChanged?.Invoke(_bgmVolume);
    }

    /// <summary>
    /// Sets the sound effects volume.
    /// </summary>
    /// <param name="volume">Volume level from 0.0 (silent) to 1.0 (full volume).</param>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Math.Clamp(volume, 0.0f, 1.0f);
        OnSFXVolumeChanged?.Invoke(_sfxVolume);
    }

    /// <summary>
    /// Resets the singleton instance. Useful for testing or game restart.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }
}
