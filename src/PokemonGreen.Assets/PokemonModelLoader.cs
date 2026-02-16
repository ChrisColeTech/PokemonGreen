#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Assets;

/// <summary>
/// Loads Pokemon 3D models from the Pokemon3D/ asset directory.
/// Each species has a folder (e.g. Pokemon3D/pm0001_00/) containing model.dae + textures.
/// Uses SkeletalModelData for bone animation support.
/// </summary>
public static class PokemonModelLoader
{
    private static readonly Dictionary<string, SkeletalModelData> _cache = new();

    /// <summary>
    /// Load a Pokemon model by its folder name (e.g. "pm0001_00").
    /// Returns null if the model folder or model.dae doesn't exist.
    /// </summary>
    public static SkeletalModelData? Load(string modelFolder, GraphicsDevice graphicsDevice)
    {
        if (string.IsNullOrEmpty(modelFolder))
            return null;

        if (_cache.TryGetValue(modelFolder, out var cached))
            return cached;

        string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pokemon3D", modelFolder);
        string modelPath = Path.Combine(basePath, "model.dae");

        if (!File.Exists(modelPath))
            return null;

        var model = SkeletalModelData.Load(modelPath, graphicsDevice);
        _cache[modelFolder] = model;
        return model;
    }

    /// <summary>
    /// Unload a cached model to free GPU resources.
    /// </summary>
    public static void Unload(string modelFolder)
    {
        if (_cache.Remove(modelFolder, out var model))
            model.Dispose();
    }

    /// <summary>
    /// Unload all cached models.
    /// </summary>
    public static void UnloadAll()
    {
        foreach (var model in _cache.Values)
            model.Dispose();
        _cache.Clear();
    }
}
