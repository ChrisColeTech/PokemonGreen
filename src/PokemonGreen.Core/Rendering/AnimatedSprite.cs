using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Rendering;

public class AnimatedSprite
{
    public string BaseName { get; }
    public int FrameCount { get; }
    public float FrameDuration { get; set; } = 0.3f;
    public bool IsAnimated => _frames.Length > 1;
    
    private readonly Texture2D[] _frames;
    private float _timer;
    private int _currentFrame;
    
    public AnimatedSprite(string baseName, Texture2D[] frames)
    {
        BaseName = baseName;
        _frames = frames;
        FrameCount = frames.Length;
    }
    
    public void Update(float deltaTime)
    {
        if (!IsAnimated)
            return;
            
        _timer += deltaTime;
        if (_timer >= FrameDuration)
        {
            _timer -= FrameDuration;
            _currentFrame = (_currentFrame + 1) % FrameCount;
        }
    }
    
    public Texture2D GetCurrentFrame() => _frames[_currentFrame];
    
    public void Reset()
    {
        _timer = 0;
        _currentFrame = 0;
    }
}

public static class AnimatedSpriteCache
{
    private static readonly Dictionary<string, AnimatedSprite> _cache = new();
    
    public static AnimatedSprite? GetOrLoad(string baseName)
    {
        if (_cache.TryGetValue(baseName, out var cached))
            return cached;
            
        var frames = LoadFrames(baseName);
        if (frames.Count == 0)
            return null;
            
        var sprite = new AnimatedSprite(baseName, frames.ToArray());
        _cache[baseName] = sprite;
        return sprite;
    }
    
    public static AnimatedSprite? Get(string baseName)
    {
        return _cache.TryGetValue(baseName, out var sprite) ? sprite : null;
    }
    
    public static void UpdateAll(float deltaTime)
    {
        foreach (var sprite in _cache.Values)
            sprite.Update(deltaTime);
    }
    
    private static List<Texture2D> LoadFrames(string baseName)
    {
        var frames = new List<Texture2D>();
        
        var baseFrame = TextureStore.GetTexture(baseName);
        if (baseFrame != null)
            frames.Add(baseFrame);
        
        for (int i = 0; ; i++)
        {
            var frame = TextureStore.GetTexture($"{baseName}_{i}");
            if (frame == null)
                break;
            frames.Add(frame);
        }
        
        return frames;
    }
    
    public static void Clear() => _cache.Clear();
}
