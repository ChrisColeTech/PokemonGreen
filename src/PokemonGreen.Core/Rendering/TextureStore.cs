using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Assets;

namespace PokemonGreen.Core.Rendering;

public static class TextureStore
{
    private static readonly Dictionary<string, Texture2D> _cache = new();
    private static readonly Dictionary<int, Texture2D> _tileCache = new();
    private static GraphicsDevice? _graphicsDevice;

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        AssetLoader.Initialize(graphicsDevice);
    }

public static Texture2D? GetTileTexture(int tileId, string tileName)
{
    if (_tileCache.TryGetValue(tileId, out var cached))
        return cached;

    var texture = LoadTileSprite(tileName);
    if (texture != null)
    {
        _tileCache[tileId] = texture;
    }
    return texture;
}

public static Texture2D? GetItemTexture(int itemId, string spriteName)
{
    string cacheKey = $"item_{itemId}";
    if (_cache.TryGetValue(cacheKey, out var cached))
        return cached;

    var texture = AssetLoader.LoadItemSprite(spriteName);
    if (texture != null)
        _cache[cacheKey] = texture;

    return texture;
}

public static Texture2D? GetNPCTexture(int npcId, string spriteName)
{
    string cacheKey = $"npc_{npcId}";
    if (_cache.TryGetValue(cacheKey, out var cached))
        return cached;

    var texture = AssetLoader.LoadNPCSprite(spriteName);
    if (texture != null)
        _cache[cacheKey] = texture;

    return texture;
}

    public static Texture2D? GetAnimatedTileFrame(int tileId, string baseName, int frameIndex)
    {
        string cacheKey = $"{tileId}_{frameIndex}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var texture = AssetLoader.LoadSprite($"{baseName}_{frameIndex}");
        if (texture != null)
        {
            _cache[cacheKey] = texture;
        }
        return texture;
    }

    public static Texture2D? GetTexture(string spriteName)
    {
        if (_cache.TryGetValue(spriteName, out var cached))
            return cached;

        var texture = AssetLoader.LoadSprite(spriteName);
        if (texture != null)
        {
            _cache[spriteName] = texture;
        }
        return texture;
    }

    public static Texture2D? LoadPlayerSheet(string name)
    {
        if (_cache.TryGetValue($"player_{name}", out var cached))
            return cached;

        var texture = AssetLoader.LoadPlayerSprite(name);
        if (texture != null)
        {
            _cache[$"player_{name}"] = texture;
        }
        return texture;
    }

    private static Texture2D? LoadTileSprite(string tileName)
    {
        string spriteName = TileNameToSpriteName(tileName);
        var texture = AssetLoader.LoadSprite(spriteName);
        return texture;
    }

    private static string TileNameToSpriteName(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append('_');
            result.Append(char.ToLower(c));
        }
        return $"tile_{result}";
    }

    public static Texture2D CreateColorTexture(Color color)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("TextureStore must be initialized first.");

        string key = $"color_{color.R}_{color.G}_{color.B}_{color.A}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var texture = new Texture2D(_graphicsDevice, 1, 1);
        texture.SetData(new[] { color });
        _cache[key] = texture;
        return texture;
    }

    public static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r, g, b);
        }
        else if (hex.Length == 8)
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            int a = Convert.ToInt32(hex.Substring(6, 2), 16);
            return new Color(r, g, b, a);
        }

        return Color.Magenta;
    }

    public static void Clear()
    {
        foreach (var texture in _cache.Values)
            texture.Dispose();
        foreach (var texture in _tileCache.Values)
            texture.Dispose();
        _cache.Clear();
        _tileCache.Clear();
    }
}
