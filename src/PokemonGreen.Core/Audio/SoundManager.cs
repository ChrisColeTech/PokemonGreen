using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace PokemonGreen.Core.Audio;

public class SoundManager
{
    private static SoundManager? _instance;
    public static SoundManager Instance => _instance ??= new SoundManager();

    private readonly Dictionary<string, SoundEffect> _bgm = new();
    private readonly Dictionary<string, SoundEffect> _cries = new();
    private SoundEffectInstance? _currentBgm;
    private string? _currentBgmName;
    private float _masterVolume = 1.0f;
    private float _bgmVolume = 0.7f;
    private float _sfxVolume = 1.0f;
    private bool _isMuted;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = MathHelper.Clamp(value, 0f, 1f);
            UpdateVolumes();
        }
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = MathHelper.Clamp(value, 0f, 1f);
            UpdateVolumes();
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = MathHelper.Clamp(value, 0f, 1f);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            UpdateVolumes();
        }
    }

    public void Load(ContentManager content)
    {
        LoadBgm(content);
        LoadCries(content);
        Console.WriteLine($"[SoundManager] Loaded {_bgm.Count} BGM tracks, {_cries.Count} Pokemon cries");
    }

    private void LoadBgm(ContentManager content)
    {
        var bgmFiles = new Dictionary<string, string>
        {
            ["town"] = "Sounds/BGM/Town1",
            ["cave"] = "Sounds/BGM/Cave1",
            ["battle_trainer"] = "Sounds/BGM/BattleTrainer",
            ["battle_gym"] = "Sounds/BGM/BattleGymLeader",
            ["battle_evil"] = "Sounds/BGM/BattleEvil1",
        };

        foreach (var (name, path) in bgmFiles)
        {
            var sound = LoadSoundOrFallback(content, path);
            if (sound != null)
            {
                _bgm[name] = sound;
                Console.WriteLine($"[SoundManager] BGM loaded: {name}");
            }
        }
    }

    private void LoadCries(ContentManager content)
    {
        var cryFiles = new Dictionary<string, string>
        {
            ["pikachu"] = "Sounds/Cries/Pikachu",
            ["bulbasaur"] = "Sounds/Cries/Bulbasaur",
            ["charmander"] = "Sounds/Cries/Charmander",
            ["squirtle"] = "Sounds/Cries/Squirtle",
            ["charizard"] = "Sounds/Cries/Charizard",
            ["pidgey"] = "Sounds/Cries/Pidgey",
            ["rattata"] = "Sounds/Cries/Rattata",
        };

        foreach (var (name, path) in cryFiles)
        {
            var sound = LoadSoundOrFallback(content, path);
            if (sound != null)
            {
                _cries[name] = sound;
            }
        }
    }

    private SoundEffect? LoadSoundOrFallback(ContentManager content, string path)
    {
        try
        {
            return content.Load<SoundEffect>(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SoundManager] Failed to load {path}: {ex.Message}");
            return null;
        }
    }

    public void PlayBgm(string name, bool loop = true)
    {
        if (!_bgm.TryGetValue(name, out var bgm))
        {
            Console.WriteLine($"[SoundManager] BGM not found: {name}");
            return;
        }

        if (_currentBgmName == name && _currentBgm?.State == SoundState.Playing)
            return;

        StopBgm();

        _currentBgm = bgm.CreateInstance();
        _currentBgm.IsLooped = loop;
        _currentBgm.Volume = _isMuted ? 0 : _masterVolume * _bgmVolume;
        _currentBgm.Play();
        _currentBgmName = name;
    }

    public void StopBgm()
    {
        _currentBgm?.Stop();
        _currentBgm?.Dispose();
        _currentBgm = null;
        _currentBgmName = null;
    }

    public void PauseBgm()
    {
        if (_currentBgm?.State == SoundState.Playing)
            _currentBgm.Pause();
    }

    public void ResumeBgm()
    {
        if (_currentBgm?.State == SoundState.Paused)
            _currentBgm.Resume();
    }

    public void PlayCry(string pokemonName)
    {
        if (!_cries.TryGetValue(pokemonName.ToLowerInvariant(), out var cry))
        {
            Console.WriteLine($"[SoundManager] Cry not found: {pokemonName}");
            return;
        }

        var volume = _isMuted ? 0 : _masterVolume * _sfxVolume;
        cry.Play(volume, 0f, 0f);
    }

    public void PlaySfx(string name, float volume = 1.0f)
    {
        if (!_cries.TryGetValue(name.ToLowerInvariant(), out var sfx) && !_bgm.TryGetValue(name.ToLowerInvariant(), out sfx))
            return;

        var finalVolume = _isMuted ? 0 : _masterVolume * _sfxVolume * volume;
        sfx.Play(finalVolume, 0f, 0f);
    }

    private void UpdateVolumes()
    {
        if (_currentBgm != null && !_isMuted)
        {
            _currentBgm.Volume = _masterVolume * _bgmVolume;
        }
    }

    public void Unload()
    {
        StopBgm();
        _bgm.Clear();
        _cries.Clear();
    }
}
