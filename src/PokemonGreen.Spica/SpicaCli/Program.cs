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

        var textureManifest = new List<object>();
        foreach (H3DTexture tex in scene.Textures)
        {
            try
            {
                string texPath = Path.Combine(texturesDir, $"{tex.Name}.png");
                Bitmap bmp = tex.ToBitmap();
                bmp.Save(texPath, ImageFormat.Png);
                textureManifest.Add(new { name = tex.Name, file = $"textures/{tex.Name}.png", width = (int)tex.Width, height = (int)tex.Height });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Texture \"{tex.Name}\": {ex.Message}");
            }
        }

        // 2. Static models (no animation baked in)
        var modelManifest = new List<object>();
        for (int m = 0; m < scene.Models.Count; m++)
        {
            var dae = new DAE(scene, m, -1);
            string name = m == 0 ? "model" : "model_lowpoly";
            string daePath = Path.Combine(pokemonDir, $"{name}.dae");
            dae.Save(daePath);

            var mdl = scene.Models[m];
            modelManifest.Add(new { file = $"{name}.dae", meshCount = mdl.Meshes.Count, boneCount = mdl.Skeleton.Count });
            Console.WriteLine($"  {name}.dae ({mdl.Meshes.Count} meshes, {mdl.Skeleton.Count} bones)");
        }

        // 3. Clip-only DAEs → clips/
        string clipsDir = Path.Combine(pokemonDir, "clips");
        Directory.CreateDirectory(clipsDir);

        var clipManifest = new List<object>();
        for (int a = 0; a < scene.SkeletalAnimations.Count; a++)
        {
            try
            {
                var dae = new DAE(scene, 0, a, clipOnly: true);
                string clipFile = $"clip_{a:D3}.dae";
                string clipPath = Path.Combine(clipsDir, clipFile);
                dae.Save(clipPath);

                var anim = scene.SkeletalAnimations[a];
                string clipName = anim.Name ?? $"clip_{a}";
                clipManifest.Add(new { index = a, name = clipName, file = $"clips/{clipFile}", frameCount = (int)anim.FramesCount, fps = 30 });
                Console.WriteLine($"  clips/{clipFile} \"{clipName}\" ({(int)anim.FramesCount} frames)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  clip_{a}: {ex.Message}");
            }
        }

        // 4. manifest.json
        var manifest = new
        {
            version = 1,
            pokemonId = groupId,
            models = modelManifest,
            textures = textureManifest,
            clips = clipManifest
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
}
