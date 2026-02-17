#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Assets;

/// <summary>
/// Loads Pokemon 3D models on demand from the Pokemon3D/ asset directory.
/// Each species has a folder (e.g. Pokemon3D/pm0001_00/) containing:
///   model.dae + clips/ + textures + manifest.json
///
/// Lazy loading: models are only loaded when requested and cached with LRU eviction.
/// For thousands of models, only a small working set stays in GPU memory.
/// </summary>
public static class PokemonModelLoader
{
    private static readonly Dictionary<string, SkeletalModelData> _cache = new();
    private static readonly LinkedList<string> _lruOrder = new();
    private static readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new();

    /// <summary>Maximum models to keep in GPU memory. Oldest are evicted when exceeded.</summary>
    public static int MaxCachedModels { get; set; } = 16;

    /// <summary>
    /// External base directory for Pokemon3D assets. When set, models are loaded from
    /// this path instead of the build output directory. This avoids copying thousands
    /// of model folders into the build output on every build.
    /// Set this before loading any models (e.g. in Game1.Initialize()).
    /// </summary>
    public static string? ExternalAssetsPath { get; set; }

    /// <summary>Number of models currently cached in GPU memory.</summary>
    public static int CachedCount => _cache.Count;

    /// <summary>
    /// Auto-detect the Pokemon3D source directory relative to this assembly's location.
    /// In dev builds, the assembly is at src/PokemonGreen.Assets/bin/Debug/net9.0/ so
    /// walking 3 levels up reaches the project root where Pokemon3D/ lives.
    /// Call this once at startup (e.g. Game1.Initialize()) instead of hardcoding paths.
    /// </summary>
    public static void InitializeDevPaths()
    {
        if (ExternalAssetsPath != null) return; // already configured

        string assemblyDir = Path.GetDirectoryName(typeof(PokemonModelLoader).Assembly.Location) ?? "";
        // bin/Debug/net9.0 → project root (3 levels up)
        string projectRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
        string pokemon3d = Path.Combine(projectRoot, "Pokemon3D");
        if (Directory.Exists(pokemon3d))
            ExternalAssetsPath = pokemon3d;
    }

    /// <summary>
    /// Load a Pokemon model by its folder name (e.g. "pm0001_00").
    /// Returns null if the model folder or model.dae doesn't exist.
    /// Automatically loads animation clips from manifest.json if present.
    /// Evicts least-recently-used models when cache exceeds MaxCachedModels.
    /// </summary>
    public static SkeletalModelData? Load(string modelFolder, GraphicsDevice graphicsDevice)
    {
        if (string.IsNullOrEmpty(modelFolder))
            return null;

        if (_cache.TryGetValue(modelFolder, out var cached))
        {
            // Move to front of LRU
            if (_lruNodes.TryGetValue(modelFolder, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddFirst(node);
            }
            return cached;
        }

        string basePath = ResolveBasePath(modelFolder);
        string modelPath = Path.Combine(basePath, "model.dae");

        if (!File.Exists(modelPath))
            return null;

        // Evict LRU entries if cache is full
        while (_cache.Count >= MaxCachedModels && _lruOrder.Count > 0)
        {
            var oldest = _lruOrder.Last!.Value;
            EvictModel(oldest);
        }

        var model = SkeletalModelData.Load(modelPath, graphicsDevice);

        // Load animation clips from manifest if available
        string manifestPath = Path.Combine(basePath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                LoadClipsFromManifest(model, basePath, manifestPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load clips for {modelFolder}: {ex.Message}");
            }
        }

        _cache[modelFolder] = model;
        var lruNode = _lruOrder.AddFirst(modelFolder);
        _lruNodes[modelFolder] = lruNode;

        return model;
    }

    /// <summary>
    /// Resolve the base path for a model folder. Checks ExternalAssetsPath first,
    /// then falls back to the build output directory.
    /// </summary>
    private static string ResolveBasePath(string modelFolder)
    {
        if (ExternalAssetsPath != null)
        {
            string extPath = Path.Combine(ExternalAssetsPath, modelFolder);
            if (Directory.Exists(extPath))
                return extPath;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pokemon3D", modelFolder);
    }

    /// <summary>
    /// Parse manifest.json and load all referenced animation clip DAEs.
    /// Handles both formats:
    ///   - New: models[].clips[] (nested under each model entry)
    ///   - Legacy: clips[] at root level
    /// Property names are case-insensitive (handles both camelCase and PascalCase).
    /// Sets the first clip as the active animation.
    /// </summary>
    private static void LoadClipsFromManifest(SkeletalModelData model, string basePath, string manifestPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        string? firstClipName = null;

        // Try new format: models[].clips[] (from TS extractor and C# OhanaCli split mode)
        if (TryGetProp(root, "models", out var modelsArray))
        {
            foreach (var modelEntry in modelsArray.EnumerateArray())
            {
                if (TryGetProp(modelEntry, "clips", out var clips))
                {
                    foreach (var clipEntry in clips.EnumerateArray())
                        LoadSingleClip(model, basePath, clipEntry, ref firstClipName);
                }
            }
        }
        // Fallback: root-level clips[] (older CLI format)
        else if (TryGetProp(root, "clips", out var clipsArray))
        {
            foreach (var clipEntry in clipsArray.EnumerateArray())
                LoadSingleClip(model, basePath, clipEntry, ref firstClipName);
        }

        // Activate the first clip (typically the idle animation)
        if (firstClipName != null)
            model.Play(firstClipName);
    }

    private static void LoadSingleClip(SkeletalModelData model, string basePath,
        JsonElement clipEntry, ref string? firstClipName)
    {
        string? clipFile = TryGetString(clipEntry, "file");
        string? clipName = TryGetString(clipEntry, "name");

        if (clipFile == null || clipName == null) return;

        string clipPath = Path.Combine(basePath, clipFile);
        if (!File.Exists(clipPath)) return;

        model.RegisterClip(clipName, clipPath);
        firstClipName ??= clipName;
    }

    /// <summary>Try to get a property by camelCase or PascalCase name.</summary>
    private static bool TryGetProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;
        // Try PascalCase
        string pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
        return element.TryGetProperty(pascal, out value);
    }

    /// <summary>Try to get a string property by camelCase or PascalCase name.</summary>
    private static string? TryGetString(JsonElement element, string name)
    {
        if (TryGetProp(element, name, out var prop))
            return prop.GetString();
        return null;
    }

    /// <summary>Evict a single model from the cache and free GPU resources.</summary>
    private static void EvictModel(string modelFolder)
    {
        if (_cache.Remove(modelFolder, out var model))
            model.Dispose();
        if (_lruNodes.Remove(modelFolder, out var node))
            _lruOrder.Remove(node);
    }

    /// <summary>
    /// Unload a cached model to free GPU resources.
    /// </summary>
    public static void Unload(string modelFolder)
    {
        EvictModel(modelFolder);
    }

    /// <summary>
    /// Unload all cached models.
    /// </summary>
    public static void UnloadAll()
    {
        foreach (var model in _cache.Values)
            model.Dispose();
        _cache.Clear();
        _lruOrder.Clear();
        _lruNodes.Clear();
    }
}
