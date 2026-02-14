using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

#nullable enable

namespace PokemonGreen.Rendering;

/// <summary>
/// Static class for centralized texture management.
/// </summary>
public static class TextureStore
{
    private static readonly Dictionary<string, Texture2D> _cache = new();
    private static GraphicsDevice? _graphicsDevice;

    /// <summary>
    /// Initializes the texture store with a graphics device.
    /// Must be called before using CreateColorTexture.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device to use for creating textures.</param>
    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// Loads a texture from the content pipeline and caches it.
    /// </summary>
    /// <param name="content">The content manager.</param>
    /// <param name="name">The name/path of the texture asset.</param>
    /// <returns>The loaded texture.</returns>
    public static Texture2D LoadTexture(ContentManager content, string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var texture = content.Load<Texture2D>(name);
        _cache[name] = texture;
        return texture;
    }

    /// <summary>
    /// Gets a previously loaded texture from the cache.
    /// </summary>
    /// <param name="name">The name of the texture.</param>
    /// <returns>The cached texture, or null if not found.</returns>
    public static Texture2D? GetTexture(string name)
    {
        return _cache.TryGetValue(name, out var texture) ? texture : null;
    }

    /// <summary>
    /// Creates a 1x1 solid color texture and caches it.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="color">The color of the texture.</param>
    /// <returns>A 1x1 texture of the specified color.</returns>
    public static Texture2D CreateColorTexture(GraphicsDevice graphicsDevice, Color color)
    {
        string key = $"color_{color.R}_{color.G}_{color.B}_{color.A}";

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var texture = new Texture2D(graphicsDevice, 1, 1);
        texture.SetData(new[] { color });
        _cache[key] = texture;
        return texture;
    }

    /// <summary>
    /// Creates a 1x1 solid color texture using the initialized graphics device.
    /// </summary>
    /// <param name="color">The color of the texture.</param>
    /// <returns>A 1x1 texture of the specified color.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called.</exception>
    public static Texture2D CreateColorTexture(Color color)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException("TextureStore must be initialized with a GraphicsDevice first.");

        return CreateColorTexture(_graphicsDevice, color);
    }

    /// <summary>
    /// Parses a hex color string and creates a texture for it.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="hexColor">Hex color string (e.g., "#7ec850" or "7ec850").</param>
    /// <returns>A 1x1 texture of the specified color.</returns>
    public static Texture2D CreateColorTextureFromHex(GraphicsDevice graphicsDevice, string hexColor)
    {
        var color = ParseHexColor(hexColor);
        return CreateColorTexture(graphicsDevice, color);
    }

    /// <summary>
    /// Parses a hex color string to a Color.
    /// </summary>
    /// <param name="hex">Hex color string (e.g., "#7ec850", "7ec850", or "#7ec85080" for alpha).</param>
    /// <returns>The parsed Color.</returns>
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

        return Color.Magenta; // Fallback for invalid colors
    }

    /// <summary>
    /// Clears all cached textures.
    /// </summary>
    public static void Clear()
    {
        foreach (var texture in _cache.Values)
        {
            texture.Dispose();
        }
        _cache.Clear();
    }
}
