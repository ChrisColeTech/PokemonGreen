using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace PokemonGreen.Assets;

public static class AssetLoader
{
    private static GraphicsDevice? _graphicsDevice;

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

public static Stream? GetSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.Sprites.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}

public static Stream? GetItemSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.Items.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}

public static Stream? GetPlayerSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.Player.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}

public static Stream? GetNPCSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.NPCs.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}

public static string? LoadNPCJson(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.NPCs.{name}.json";
    using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
        return null;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

public static Texture2D? LoadSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized with a GraphicsDevice first.");

    using var stream = GetSpriteStream(name);
    if (stream == null)
        return null;

    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;

    var texture = LoadTextureFromPngStream(memStream, _graphicsDevice, $"Sprites.{name}");
    LogTextureDiagnostics($"Sprites.{name}", texture);
    return texture;
}

public static Texture2D? LoadItemSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized with a GraphicsDevice first.");

    using var stream = GetItemSpriteStream(name);
    if (stream == null)
        return null;

    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;

    var texture = LoadTextureFromPngStream(memStream, _graphicsDevice, $"Items.{name}");
    LogTextureDiagnostics($"Items.{name}", texture);
    return texture;
}

public static Texture2D? LoadPlayerSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized with a GraphicsDevice first.");

    using var stream = GetPlayerSpriteStream(name);
    if (stream == null)
        return null;

    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;

    var texture = LoadTextureFromPngStream(memStream, _graphicsDevice, $"Player.{name}");
    LogTextureDiagnostics($"Player.{name}", texture);
    return texture;
}

public static Texture2D? LoadNPCSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized with a GraphicsDevice first.");

    using var stream = GetNPCSpriteStream(name);
    if (stream == null)
        return null;

    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;

    var texture = LoadTextureFromPngStream(memStream, _graphicsDevice, $"NPCs.{name}");
    LogTextureDiagnostics($"NPCs.{name}", texture);
    return texture;
}

    private static Texture2D LoadTextureFromPngStream(Stream stream, GraphicsDevice graphicsDevice, string label)
    {
        using var image = Image.Load<Rgba32>(stream);
        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);

        var data = new XnaColor[image.Width * image.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            data[i] = new XnaColor(p.R, p.G, p.B, p.A);
        }

        var texture = new Texture2D(graphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
        texture.SetData(data);
        return texture;
    }

    private static void ApplyBorderTransparencyFallbackIfNeeded(Rgba32[] pixels, int width, int height, string label)
    {
        int transparentCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].A == 0)
                transparentCount++;
        }

        if (transparentCount > 0)
            return;

        var borderColor = pixels[0];
        float borderRatio = GetBorderMatchRatio(pixels, width, height, borderColor);
        if (borderRatio < 0.92f)
            return;

        int cleared = FloodClearBorderColor(pixels, width, height, borderColor);
        Console.WriteLine($"[AssetLoader] Applied border alpha fallback for {label}: cleared={cleared}, borderMatch={borderRatio:P0}");
    }

    private static float GetBorderMatchRatio(Rgba32[] pixels, int width, int height, Rgba32 color)
    {
        int total = 0;
        int matches = 0;

        for (int x = 0; x < width; x++)
        {
            total += 2;
            if (IsSimilar(pixels[x], color))
                matches++;
            if (IsSimilar(pixels[(height - 1) * width + x], color))
                matches++;
        }

        for (int y = 1; y < height - 1; y++)
        {
            total += 2;
            if (IsSimilar(pixels[y * width], color))
                matches++;
            if (IsSimilar(pixels[y * width + (width - 1)], color))
                matches++;
        }

        return total == 0 ? 0f : (float)matches / total;
    }

    private static int FloodClearBorderColor(Rgba32[] pixels, int width, int height, Rgba32 borderColor)
    {
        var visited = new bool[pixels.Length];
        var queue = new Queue<int>();
        EnqueueBorderSeeds(pixels, width, height, borderColor, visited, queue);

        int cleared = 0;
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            var p = pixels[idx];
            if (!IsSimilar(p, borderColor))
                continue;

            if (p.A != 0)
            {
                pixels[idx] = new Rgba32(0, 0, 0, 0);
                cleared++;
            }

            int x = idx % width;
            int y = idx / width;

            TryEnqueue(x - 1, y, width, height, visited, queue);
            TryEnqueue(x + 1, y, width, height, visited, queue);
            TryEnqueue(x, y - 1, width, height, visited, queue);
            TryEnqueue(x, y + 1, width, height, visited, queue);
        }

        return cleared;
    }

    private static void EnqueueBorderSeeds(
        Rgba32[] pixels,
        int width,
        int height,
        Rgba32 borderColor,
        bool[] visited,
        Queue<int> queue)
    {
        for (int x = 0; x < width; x++)
        {
            TryEnqueueSeed(x, 0, width, visited, queue, pixels, borderColor);
            TryEnqueueSeed(x, height - 1, width, visited, queue, pixels, borderColor);
        }

        for (int y = 1; y < height - 1; y++)
        {
            TryEnqueueSeed(0, y, width, visited, queue, pixels, borderColor);
            TryEnqueueSeed(width - 1, y, width, visited, queue, pixels, borderColor);
        }
    }

    private static void TryEnqueueSeed(
        int x,
        int y,
        int width,
        bool[] visited,
        Queue<int> queue,
        Rgba32[] pixels,
        Rgba32 borderColor)
    {
        int idx = y * width + x;
        if (visited[idx] || !IsSimilar(pixels[idx], borderColor))
            return;

        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static void TryEnqueue(int x, int y, int width, int height, bool[] visited, Queue<int> queue)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return;

        int idx = y * width + x;
        if (visited[idx])
            return;

        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static bool IsSimilar(Rgba32 a, Rgba32 b)
    {
        const int tolerance = 8;
        return Math.Abs(a.R - b.R) <= tolerance
            && Math.Abs(a.G - b.G) <= tolerance
            && Math.Abs(a.B - b.B) <= tolerance;
    }

    private static void LogTextureDiagnostics(string label, Texture2D texture)
    {
        var pixels = new XnaColor[texture.Width * texture.Height];
        texture.GetData(pixels);

        int minA = 255;
        int maxA = 0;
        int transparent = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            int a = pixels[i].A;
            if (a < minA)
                minA = a;
            if (a > maxA)
                maxA = a;
            if (a == 0)
                transparent++;
        }

        var p = pixels[0];
        Console.WriteLine(
            $"[AssetLoader] Loaded {label}: {texture.Width}x{texture.Height}, firstPixel=R:{p.R} G:{p.G} B:{p.B} A:{p.A}, alphaMin={minA}, alphaMax={maxA}, alphaZero={transparent}");
    }

    public static string[] GetEmbeddedResourceNames()
    {
        return typeof(AssetLoader).Assembly.GetManifestResourceNames();
    }
}
