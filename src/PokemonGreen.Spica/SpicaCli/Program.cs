using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.Formats.Generic.COLLADA;

using SpicaCli.Formats;

using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLower();

        switch (command)
        {
            case "info":    return RunInfo(args);
            case "convert": return RunConvert(args);
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintUsage();
                return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("SpicaCli - 3DS Model/Texture/Animation Converter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  SpicaCli info <file>                          Inspect a file");
        Console.WriteLine("  SpicaCli convert <file> -o <dir> [options]    Convert to DAE + PNG");
        Console.WriteLine();
        Console.WriteLine("Convert options:");
        Console.WriteLine("  -o <dir>                 Output directory (required)");
        Console.WriteLine("  -n <count>               Limit to first N GARC entries");
        Console.WriteLine("  --anim <idx>             Animation index to export (default: 0, -1 = none)");
        Console.WriteLine("  --split-model-anims      Split export: model.dae + clips/ + textures/ + manifest");
    }

    static int RunInfo(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: SpicaCli info <file>");
            return 1;
        }

        string filePath = args[1];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        var fi = new FileInfo(filePath);
        Console.WriteLine($"File: {filePath}");
        Console.WriteLine($"Size: {fi.Length:N0} bytes");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        if (GARC.IsGARC(fs))
        {
            var entries = GARC.GetEntries(fs);
            Console.WriteLine($"Format: GARC container");
            Console.WriteLine($"Entries: {entries.Length}");

            int show = Math.Min(entries.Length, 10);
            for (int i = 0; i < show; i++)
            {
                Console.WriteLine($"  [{i}] {entries[i].Length:N0} bytes @ 0x{entries[i].Offset:X}");
            }
            if (entries.Length > show)
                Console.WriteLine($"  ... ({entries.Length - show} more)");
        }
        else
        {
            H3D scene = FormatIdentifier.IdentifyAndOpen(filePath);

            if (scene == null)
            {
                Console.WriteLine("Format: unknown");
                return 1;
            }

            Console.WriteLine($"Models:     {scene.Models.Count}");
            Console.WriteLine($"Textures:   {scene.Textures.Count}");
            Console.WriteLine($"Skeletal Animations: {scene.SkeletalAnimations.Count}");
            Console.WriteLine($"Material Animations: {scene.MaterialAnimations.Count}");

            for (int i = 0; i < scene.Models.Count; i++)
            {
                var mdl = scene.Models[i];
                Console.WriteLine($"  Model[{i}] \"{mdl.Name}\" — {mdl.Meshes.Count} meshes, {mdl.Skeleton.Count} bones");
            }

            for (int i = 0; i < scene.Textures.Count; i++)
            {
                var tex = scene.Textures[i];
                Console.WriteLine($"  Texture[{i}] \"{tex.Name}\" — {tex.Width}x{tex.Height}");
            }
        }

        return 0;
    }

    static int RunConvert(string[] args)
    {
        string filePath = null;
        string outputDir = null;
        int maxEntries = -1;
        int animIndex = 0;
        bool splitMode = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                    if (++i < args.Length) outputDir = args[i];
                    break;
                case "-n":
                    if (++i < args.Length) int.TryParse(args[i], out maxEntries);
                    break;
                case "--anim":
                    if (++i < args.Length) int.TryParse(args[i], out animIndex);
                    break;
                case "--split-model-anims":
                    splitMode = true;
                    break;
                default:
                    if (filePath == null) filePath = args[i];
                    break;
            }
        }

        if (filePath == null || outputDir == null)
        {
            Console.Error.WriteLine("Usage: SpicaCli convert <file> -o <dir> [-n count] [--anim idx]");
            return 1;
        }

        if (splitMode && animIndex != 0)
        {
            Console.Error.WriteLine("--split-model-anims is incompatible with --anim");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        if (GARC.IsGARC(fs))
        {
            return ConvertGARC(fs, outputDir, maxEntries, animIndex, splitMode);
        }
        else
        {
            H3D scene = FormatIdentifier.IdentifyAndOpen(filePath);
            if (scene == null)
            {
                Console.Error.WriteLine("Could not identify file format.");
                return 1;
            }

            ExportScene(scene, outputDir, animIndex);
            return 0;
        }
    }

    static int ConvertGARC(FileStream fs, string outputDir, int maxEntries, int animIndex, bool splitMode)
    {
        var entries = GARC.GetEntries(fs);
        int count = maxEntries > 0 ? Math.Min(maxEntries, entries.Length) : entries.Length;

        Console.WriteLine($"GARC with {entries.Length} entries. Processing {count}...");

        if (splitMode)
            return ConvertGARCSplit(fs, entries, outputDir, count);

        H3DDict<H3DBone> lastSkeleton = null;
        int exported = 0;
        int errors = 0;

        for (int i = 0; i < count; i++)
        {
            if (entries[i].Length < 4) continue;

            try
            {
                byte[] data = GARC.ReadEntry(fs, entries[i]);

                // Decompress LZ11 if needed
                if (LZSS.IsCompressed(data))
                {
                    try { data = LZSS.Decompress(data); }
                    catch { continue; } // Skip entries that fail to decompress
                }

                using var ms = new MemoryStream(data);
                H3D scene = FormatIdentifier.IdentifyAndOpen(ms, lastSkeleton);

                if (scene == null) continue;

                // Track skeleton for subsequent animation entries
                if (scene.Models.Count > 0 && scene.Models[0].Skeleton.Count > 0)
                {
                    lastSkeleton = scene.Models[0].Skeleton;
                }

                // Skip entries with no exportable content
                if (scene.Models.Count == 0 && scene.Textures.Count == 0) continue;

                ExportScene(scene, outputDir, animIndex);
                exported++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Entry [{i}] error: {ex.Message}");
                errors++;
            }
        }

        Console.WriteLine($"Done. Exported {exported} entries, {errors} errors.");
        return errors > 0 ? 1 : 0;
    }

    static int ConvertGARCSplit(FileStream fs, GARC.GARCEntry[] entries, string outputDir, int count)
    {
        H3DDict<H3DBone> lastSkeleton = null;
        int exported = 0;
        int errors = 0;
        int groupNum = 0;

        // Accumulate current group
        H3D currentScene = null;
        string currentId = null;

        for (int i = 0; i < count; i++)
        {
            if (entries[i].Length < 4) continue;

            try
            {
                byte[] data = GARC.ReadEntry(fs, entries[i]);

                if (LZSS.IsCompressed(data))
                {
                    try { data = LZSS.Decompress(data); }
                    catch { continue; }
                }

                using var ms = new MemoryStream(data);
                H3D scene = FormatIdentifier.IdentifyAndOpen(ms, lastSkeleton);

                if (scene == null) continue;

                bool hasModels = scene.Models.Count > 0 && scene.Models[0].Skeleton.Count > 0;

                if (hasModels)
                {
                    // New model entry — export previous group if any
                    if (currentScene != null && currentScene.Models.Count > 0)
                    {
                        try
                        {
                            ExportSplitGroup(currentScene, outputDir, currentId ?? $"group_{groupNum:D4}");
                            exported++;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"  Group {currentId ?? groupNum.ToString()} error: {ex.Message}");
                            errors++;
                        }
                        groupNum++;
                    }

                    currentScene = scene;
                    lastSkeleton = scene.Models[0].Skeleton;
                    currentId = ExtractPokemonId(scene.Models[0]);
                }
                else if (currentScene != null)
                {
                    // Merge textures/animations into current group
                    currentScene.Merge(scene);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Entry [{i}] error: {ex.Message}");
                errors++;
            }
        }

        // Export final group
        if (currentScene != null && currentScene.Models.Count > 0)
        {
            try
            {
                ExportSplitGroup(currentScene, outputDir, currentId ?? $"group_{groupNum:D4}");
                exported++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Group {currentId ?? groupNum.ToString()} error: {ex.Message}");
                errors++;
            }
        }

        Console.WriteLine($"Done. Exported {exported} groups, {errors} errors.");
        return errors > 0 ? 1 : 0;
    }

    static void ExportSplitGroup(H3D scene, string outputDir, string groupId)
    {
        Console.WriteLine($"Exporting {groupId}...");
        string pokemonDir = Path.Combine(outputDir, groupId);
        Directory.CreateDirectory(pokemonDir);

        // 1. Textures → textures/
        string texturesDir = Path.Combine(pokemonDir, "textures");
        Directory.CreateDirectory(texturesDir);

        var textureFileNames = new List<string>();
        foreach (H3DTexture tex in scene.Textures)
        {
            try
            {
                string texPath = Path.Combine(texturesDir, $"{tex.Name}.png");
                Bitmap bmp = tex.ToBitmap();
                bmp.Save(texPath, ImageFormat.Png);
                textureFileNames.Add($"textures/{tex.Name}.png");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Texture \"{tex.Name}\": {ex.Message}");
            }
        }

        // 2. Static models (no animation baked in) + clip-only DAEs
        string clipsDir = Path.Combine(pokemonDir, "clips");
        Directory.CreateDirectory(clipsDir);

        string assetType = DetectAssetType(scene);
        var modelEntries = new List<object>();

        for (int m = 0; m < scene.Models.Count; m++)
        {
            var dae = new DAE(scene, m, -1);
            string name = m == 0 ? "model" : "model_lowpoly";
            string daePath = Path.Combine(pokemonDir, $"{name}.dae");
            dae.Save(daePath);

            var mdl = scene.Models[m];
            Console.WriteLine($"  {name}.dae ({mdl.Meshes.Count} meshes, {mdl.Skeleton.Count} bones)");

            // 3. Clip-only DAEs → clips/ (attached to first model)
            var clipEntries = new List<object>();
            if (m == 0)
            {
                for (int a = 0; a < scene.SkeletalAnimations.Count; a++)
                {
                    try
                    {
                        var clipDae = new DAE(scene, 0, a, clipOnly: true);
                        string clipFile = $"clip_{a:D3}.dae";
                        string clipPath = Path.Combine(clipsDir, clipFile);
                        clipDae.Save(clipPath);

                        var anim = scene.SkeletalAnimations[a];
                        string clipName = anim.Name ?? $"clip_{a}";
                        string clipId = $"clip_{a:D3}";
                        var (semName, semSource) = ResolveSemanticMetadata(clipName, a, assetType);
                        clipEntries.Add(new { index = a, id = clipId, name = clipId, sourceName = clipName, file = $"clips/{clipFile}", frameCount = (int)anim.FramesCount, fps = 30, semanticName = semName, semanticSource = semSource });
                        Console.WriteLine($"  clips/{clipFile} \"{clipName}\" ({(int)anim.FramesCount} frames){(semName != null ? $" [{semName}]" : "")}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  clip_{a}: {ex.Message}");
                    }
                }
            }

            modelEntries.Add(new { name, modelFile = $"{name}.dae", clips = clipEntries });
        }

        // 4. manifest.json — same format as OhanaCli
        var manifest = new
        {
            version = 1,
            mode = "split-model-anims",
            textures = textureFileNames,
            models = modelEntries
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(pokemonDir, "manifest.json"), manifestJson);
    }

    static string ExtractPokemonId(H3DModel model)
    {
        foreach (var mat in model.Materials)
        {
            string name = mat.Texture0Name;
            if (name != null && name.StartsWith("pm"))
            {
                int idx = name.IndexOf('_', 2);
                if (idx > 0)
                {
                    int idx2 = name.IndexOf('_', idx + 1);
                    if (idx2 > 0) return name.Substring(0, idx2);
                }
            }
        }
        return null;
    }

    static void ExportScene(H3D scene, string outputDir, int animIndex)
    {
        // Export textures as PNG
        foreach (H3DTexture tex in scene.Textures)
        {
            try
            {
                string texPath = Path.Combine(outputDir, $"{tex.Name}.png");
                Bitmap bmp = tex.ToBitmap();
                bmp.Save(texPath, ImageFormat.Png);
                Console.WriteLine($"  Texture: {texPath} ({tex.Width}x{tex.Height})");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Texture \"{tex.Name}\" error: {ex.Message}");
            }
        }

        // Export models as DAE
        for (int m = 0; m < scene.Models.Count; m++)
        {
            try
            {
                int anim = animIndex;
                if (anim >= scene.SkeletalAnimations.Count) anim = -1;

                DAE dae = new DAE(scene, m, anim);

                string suffix = scene.Models.Count > 1 ? $"_{m}" : "";
                string daePath = Path.Combine(outputDir, $"model{suffix}.dae");
                dae.Save(daePath);

                var mdl = scene.Models[m];
                Console.WriteLine($"  Model: {daePath} ({mdl.Meshes.Count} meshes, {mdl.Skeleton.Count} bones, anim={anim})");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Model[{m}] error: {ex.Message}");
            }
        }
    }

    // Detect asset type from H3D scene — checks texture/model names for "pm" vs "tr" prefix.
    static string DetectAssetType(H3D scene)
    {
        foreach (var tex in scene.Textures)
        {
            if (tex.Name.StartsWith("pm", StringComparison.OrdinalIgnoreCase)) return "pokemon";
            if (tex.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return "overworld";
            if (tex.Name.Contains("_fi", StringComparison.OrdinalIgnoreCase)) return "overworld";
        }
        foreach (var mdl in scene.Models)
        {
            if (mdl.Name.StartsWith("pm", StringComparison.OrdinalIgnoreCase)) return "pokemon";
            if (mdl.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return "overworld";
        }
        return "unknown";
    }

    // Semantic name resolution — shared slot maps with OhanaCli and Spica.Registry.
    // See SPICA-README.md "Overworld Animation Slots" for full documentation.
    static (string? Name, string? Source) ResolveSemanticMetadata(string sourceName, int clipIndex, string assetType)
    {
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            if (sourceName.Contains("idle", StringComparison.OrdinalIgnoreCase)) return ("Idle", "source-name");
            if (sourceName.Contains("walk", StringComparison.OrdinalIgnoreCase)) return ("Walk", "source-name");
            if (sourceName.Contains("run", StringComparison.OrdinalIgnoreCase)) return ("Run", "source-name");
            if (sourceName.Contains("jump", StringComparison.OrdinalIgnoreCase)) return ("Jump", "source-name");
        }

        int sourceIndex = ParseSourceAnimIndex(sourceName, clipIndex);

        string? mapped = assetType switch
        {
            "overworld" => MapOverworldSlot(sourceIndex),
            "pokemon" => MapPokemonSlot(sourceIndex),
            _ => sourceIndex switch { 0 => "Idle", _ => null }
        };

        return (mapped, mapped is null ? null : "slot-map-v1");
    }

    static string? MapOverworldSlot(int slot) => slot switch
    {
        0   => "Idle",
        1   => "Walk",
        2   => "Run",
        4   => "Jump",
        5   => "Land",
        7   => "ShortAction1",
        8   => "LongAction1",
        9   => "ShortAction2",
        17  => "MediumAction",
        20  => "Action",
        23  => "Action2",
        30  => "ShortAction3",
        31  => "ShortAction4",
        52  => "IdleVariant",
        54  => "ShortAction5",
        55  => "LongAction2",
        56  => "ShortAction6",
        59  => "Action3",
        61  => "Action4",
        72  => "Action5",
        123 => "LongAction3",
        124 => "Action6",
        125 => "Action7",
        127 => "Action8",
        128 => "Action9",
        _ => null
    };

    static string? MapPokemonSlot(int slot) => slot switch
    {
        0 => "Idle",
        _ => null
    };

    static int ParseSourceAnimIndex(string sourceName, int fallback)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return fallback;
        int lastUnderscore = sourceName.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < sourceName.Length - 1)
        {
            if (int.TryParse(sourceName.AsSpan(lastUnderscore + 1), out int parsed))
                return parsed;
        }
        return fallback;
    }
}
