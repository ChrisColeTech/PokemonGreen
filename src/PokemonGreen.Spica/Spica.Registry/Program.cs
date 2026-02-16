using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Animation;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.Formats.GFL2;
using SPICA.Formats.GFL2.Model;
using SPICA.Formats.GFL2.Motion;
using SPICA.Formats.GFL2.Texture;
using SPICA.Formats.Generic.COLLADA;

using Spica.Registry;

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        switch (args[0].ToLower())
        {
            case "scan":   return RunScan(args);
            case "export": return RunExport(args);
            default:
                Console.Error.WriteLine($"Unknown command: {args[0]}");
                PrintUsage();
                return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Spica.Registry - Pokemon GARC Registry & Exporter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Spica.Registry scan <garc-file> [-o registry.json] [-n count]");
        Console.WriteLine("    Scan a GARC and classify every entry into a registry.");
        Console.WriteLine();
        Console.WriteLine("  Spica.Registry export <registry.json> <garc-file> -o <dir>");
        Console.WriteLine("    Export per-Pokemon folders (model + textures + animations).");
    }

    // ── scan ──────────────────────────────────────────────────────────

    static int RunScan(string[] args)
    {
        string garcPath = null;
        string outputPath = "registry.json";
        int maxEntries = -1;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": if (++i < args.Length) outputPath = args[i]; break;
                case "-n": if (++i < args.Length) int.TryParse(args[i], out maxEntries); break;
                default:   if (garcPath == null) garcPath = args[i]; break;
            }
        }

        if (garcPath == null)
        {
            Console.Error.WriteLine("Usage: Spica.Registry scan <garc-file> [-o registry.json] [-n count]");
            return 1;
        }

        using var fs = new FileStream(garcPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (!GARC.IsGARC(fs))
        {
            Console.Error.WriteLine("Not a GARC file.");
            return 1;
        }

        var garcEntries = GARC.GetEntries(fs);
        int count = maxEntries > 0 ? Math.Min(maxEntries, garcEntries.Length) : garcEntries.Length;

        Console.WriteLine($"Scanning {count} of {garcEntries.Length} entries...");

        var entries = new List<EntryInfo>();
        H3DDict<H3DBone> lastSkeleton = null;

        for (int i = 0; i < count; i++)
        {
            if (i % 100 == 0 && i > 0)
                Console.Write($"\r  {i}/{count}...");

            byte[] raw = GARC.ReadEntry(fs, garcEntries[i]);
            var info = EntryClassifier.Classify(i, raw, lastSkeleton);
            entries.Add(info);

            // Track skeleton for animations
            if (info.Type == EntryType.Model && info.BoneCount > 0)
            {
                // Re-load to get the actual skeleton dict
                byte[] data = raw;
                if (LZSS.IsCompressed(data))
                    try { data = LZSS.Decompress(data); } catch { continue; }

                lastSkeleton = LoadSkeleton(data);
            }

            string typeStr = info.Type.ToString().PadRight(10);
            string extra = info.Type switch
            {
                EntryType.Model => $"{info.Pokemon ?? "?"} — {string.Join(", ", info.ModelNames)} ({info.BoneCount} bones)",
                EntryType.Texture => $"{info.Pokemon ?? "?"} — {info.TextureNames.Count} textures",
                EntryType.Animation => $"{info.AnimationNames.Count} anims, {info.FrameCount} frames",
                _ => ""
            };
            Console.WriteLine($"  [{i,5}] {typeStr} {info.CompressedSize,10:N0} -> {info.DecompressedSize,10:N0}  {extra}");
        }

        var groups = EntryClassifier.BuildGroups(entries);

        Console.WriteLine();
        Console.WriteLine($"Found {groups.Count} Pokemon:");
        foreach (var g in groups)
        {
            Console.WriteLine($"  {g.Id}: model={g.ModelEntry}, textures=[{string.Join(",", g.TextureEntries)}], anims=[{string.Join(",", g.AnimationEntries)}]");
        }

        // Write registry
        var registry = new RegistryFile
        {
            Source = Path.GetFullPath(garcPath),
            TotalEntries = garcEntries.Length,
            ScannedEntries = count,
            Entries = entries,
            Pokemon = groups
        };

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        string json = JsonSerializer.Serialize(registry, jsonOpts);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"\nRegistry written to {outputPath}");
        return 0;
    }

    // ── export ────────────────────────────────────────────────────────

    static int RunExport(string[] args)
    {
        string registryPath = null;
        string garcPath = null;
        string outputDir = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": if (++i < args.Length) outputDir = args[i]; break;
                default:
                    if (registryPath == null) registryPath = args[i];
                    else if (garcPath == null) garcPath = args[i];
                    break;
            }
        }

        if (registryPath == null || garcPath == null || outputDir == null)
        {
            Console.Error.WriteLine("Usage: Spica.Registry export <registry.json> <garc-file> -o <dir>");
            return 1;
        }

        var jsonOpts = new JsonSerializerOptions { IncludeFields = true };
        var registry = JsonSerializer.Deserialize<RegistryFile>(File.ReadAllText(registryPath), jsonOpts);
        using var fs = new FileStream(garcPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var garcEntries = GARC.GetEntries(fs);

        Directory.CreateDirectory(outputDir);

        int exported = 0;
        int errors = 0;

        foreach (var group in registry.Pokemon)
        {
            try
            {
                Console.WriteLine($"Exporting {group.Id}...");
                string pokemonDir = Path.Combine(outputDir, group.Id);
                Directory.CreateDirectory(pokemonDir);

                // 1. Load model entry → H3D with skeleton
                H3D scene = LoadEntry(fs, garcEntries[group.ModelEntry]);
                if (scene == null || scene.Models.Count == 0)
                {
                    Console.Error.WriteLine($"  {group.Id}: no model in entry {group.ModelEntry}");
                    errors++;
                    continue;
                }

                var skeleton = scene.Models[0].Skeleton;

                // 2. Merge texture entries
                foreach (int texIdx in group.TextureEntries)
                {
                    H3D texScene = LoadEntry(fs, garcEntries[texIdx]);
                    if (texScene != null)
                        scene.Merge(texScene);
                }

                // 3. Merge animation entries
                foreach (int animIdx in group.AnimationEntries)
                {
                    H3D animScene = LoadEntryWithSkeleton(fs, garcEntries[animIdx], skeleton);
                    if (animScene != null)
                        scene.Merge(animScene);
                }

                // 4. Export textures
                foreach (H3DTexture tex in scene.Textures)
                {
                    try
                    {
                        string texPath = Path.Combine(pokemonDir, $"{tex.Name}.png");
                        Bitmap bmp = tex.ToBitmap();
                        bmp.Save(texPath, ImageFormat.Png);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  Texture \"{tex.Name}\": {ex.Message}");
                    }
                }

                // 5. Export model(s) — one DAE per model, static (no animation baked in)
                for (int m = 0; m < scene.Models.Count; m++)
                {
                    int animIdx = -1; // export static model, animations are in separate files
                    var dae = new DAE(scene, m, animIdx);

                    string suffix = scene.Models.Count > 1 ? $"_{m}" : "";
                    string daePath = Path.Combine(pokemonDir, $"model{suffix}.dae");
                    dae.Save(daePath);

                    var mdl = scene.Models[m];
                    Console.WriteLine($"  model{suffix}.dae ({mdl.Meshes.Count} meshes, {mdl.Skeleton.Count} bones, {scene.SkeletalAnimations.Count} anims available, baked={animIdx})");
                }

                // 6. Export each animation as a separate DAE file
                for (int a = 0; a < scene.SkeletalAnimations.Count; a++)
                {
                    try
                    {
                        var dae = new DAE(scene, 0, a);
                        string animName = scene.SkeletalAnimations[a].Name ?? $"anim_{a}";
                        string safeName = string.Join("_", animName.Split(Path.GetInvalidFileNameChars()));
                        string animPath = Path.Combine(pokemonDir, $"anim_{a:D3}_{safeName}.dae");
                        dae.Save(animPath);
                        Console.WriteLine($"  anim_{a:D3}_{safeName}.dae ({(int)scene.SkeletalAnimations[a].FramesCount} frames)");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  anim_{a}: {ex.Message}");
                    }
                }

                exported++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  {group.Id} error: {ex.Message}");
                errors++;
            }
        }

        Console.WriteLine($"\nDone. Exported {exported} Pokemon, {errors} errors.");
        return errors > 0 ? 1 : 0;
    }

    // ── helpers ───────────────────────────────────────────────────────

    static H3D LoadEntry(FileStream fs, GARC.GARCEntry entry)
    {
        byte[] data = GARC.ReadEntry(fs, entry);
        if (LZSS.IsCompressed(data))
            try { data = LZSS.Decompress(data); } catch { return null; }

        if (data.Length < 4) return null;

        uint magic = BitConverter.ToUInt32(data, 0);

        using var ms = new MemoryStream(data);
        var reader = new BinaryReader(ms);

        // GFPackage check
        if (data[0] >= 'A' && data[0] <= 'Z' && data[1] >= 'A' && data[1] <= 'Z' && data.Length >= 0x80)
        {
            string pkgMagic = Encoding.ASCII.GetString(data, 0, 2);
            if (pkgMagic == "PC")
            {
                // Full PC package load via GFModelPack approach
                return LoadPCPackage(ms, reader);
            }
        }

        switch (magic)
        {
            case 0x15122117: // GFModel
                var h3d = new H3D();
                h3d.Models.Add(new GFModel(reader, "Model").ToH3DModel());
                return h3d;

            case 0x15041213: // GFTexture
                h3d = new H3D();
                h3d.Textures.Add(new GFTexture(reader).ToH3DTexture());
                return h3d;

            case 0x00010000: // GFModelPack
                return new GFModelPack(reader).ToH3D();

            case 0x00484342: // BCH
                return H3D.Open(data);
        }

        return null;
    }

    static H3D LoadEntryWithSkeleton(FileStream fs, GARC.GARCEntry entry, H3DDict<H3DBone> skeleton)
    {
        byte[] data = GARC.ReadEntry(fs, entry);
        if (LZSS.IsCompressed(data))
            try { data = LZSS.Decompress(data); } catch { return null; }

        if (data.Length < 4) return null;

        using var ms = new MemoryStream(data);
        var reader = new BinaryReader(ms);

        // PC package with skeleton support
        if (data[0] >= 'A' && data[0] <= 'Z' && data[1] >= 'A' && data[1] <= 'Z' && data.Length >= 0x80)
        {
            string pkgMagic = Encoding.ASCII.GetString(data, 0, 2);
            if (pkgMagic == "PC")
                return LoadPCPackage(ms, reader, skeleton);
        }

        uint magic = BitConverter.ToUInt32(data, 0);

        if (magic == 0x00060000 && skeleton != null) // raw GFMotion
        {
            var mot = new GFMotion(reader, 0);
            var h3d = new H3D();
            var sklAnim = mot.ToH3DSkeletalAnimation(skeleton);
            var matAnim = mot.ToH3DMaterialAnimation();
            var visAnim = mot.ToH3DVisibilityAnimation();
            if (sklAnim != null) h3d.SkeletalAnimations.Add(sklAnim);
            if (matAnim != null) h3d.MaterialAnimations.Add(matAnim);
            if (visAnim != null) h3d.VisibilityAnimations.Add(visAnim);
            return h3d;
        }

        // Fall back to generic load
        return LoadEntry(fs, entry);
    }

    static H3D LoadPCPackage(MemoryStream ms, BinaryReader reader, H3DDict<H3DBone> skeleton = null)
    {
        ms.Seek(0, SeekOrigin.Begin);

        // Read GFPackage header
        string magic = Encoding.ASCII.GetString(reader.ReadBytes(2));
        ushort entryCount = reader.ReadUInt16();
        long tablePos = ms.Position;

        var offsets = new uint[entryCount + 1];
        for (int i = 0; i <= entryCount; i++)
        {
            ms.Seek(tablePos + i * 4, SeekOrigin.Begin);
            offsets[i] = reader.ReadUInt32();
        }

        long baseAddr = tablePos - 4;
        var h3d = new H3D();
        int modelIdx = 0;

        for (int i = 0; i < entryCount; i++)
        {
            try
            {
                ms.Seek(baseAddr + offsets[i], SeekOrigin.Begin);
                uint m = reader.ReadUInt32();

                switch (m)
                {
                    case 0x15122117: // GFModel
                        ms.Seek(baseAddr + offsets[i], SeekOrigin.Begin);
                        string name = modelIdx == 0 ? "PM_HighPoly" : "PM_LowPoly";
                        h3d.Models.Add(new GFModel(reader, name).ToH3DModel());
                        modelIdx++;
                        break;

                    case 0x15041213: // GFTexture
                        ms.Seek(baseAddr + offsets[i], SeekOrigin.Begin);
                        h3d.Textures.Add(new GFTexture(reader).ToH3DTexture());
                        break;

                    case 0x00060000: // GFMotion
                        if (skeleton != null)
                        {
                            ms.Seek(baseAddr + offsets[i], SeekOrigin.Begin);
                            var mot = new GFMotion(reader, 0);
                            var sklAnim = mot.ToH3DSkeletalAnimation(skeleton);
                            var matAnim = mot.ToH3DMaterialAnimation();
                            var visAnim = mot.ToH3DVisibilityAnimation();
                            if (sklAnim != null) h3d.SkeletalAnimations.Add(sklAnim);
                            if (matAnim != null) h3d.MaterialAnimations.Add(matAnim);
                            if (visAnim != null) h3d.VisibilityAnimations.Add(visAnim);
                        }
                        break;
                }
            }
            catch { }
        }

        return h3d;
    }

    static H3DDict<H3DBone> LoadSkeleton(byte[] data)
    {
        if (data.Length < 4) return null;

        using var ms = new MemoryStream(data);
        var reader = new BinaryReader(ms);

        byte b0 = data[0], b1 = data[1];
        if (b0 >= 'A' && b0 <= 'Z' && b1 >= 'A' && b1 <= 'Z' && data.Length >= 0x80)
        {
            string magic = Encoding.ASCII.GetString(data, 0, 2);
            if (magic == "PC")
            {
                var h3d = LoadPCPackage(ms, reader);
                if (h3d != null && h3d.Models.Count > 0)
                    return h3d.Models[0].Skeleton;
            }
            return null;
        }

        uint magicNum = BitConverter.ToUInt32(data, 0);
        switch (magicNum)
        {
            case 0x15122117:
                var model = new GFModel(reader, "Model").ToH3DModel();
                return model.Skeleton;
            case 0x00010000:
                var pack = new GFModelPack(reader).ToH3D();
                return pack.Models.Count > 0 ? pack.Models[0].Skeleton : null;
        }

        return null;
    }
}

class RegistryFile
{
    public string Source { get; set; }
    public int TotalEntries { get; set; }
    public int ScannedEntries { get; set; }
    public List<EntryInfo> Entries { get; set; }
    public List<PokemonGroup> Pokemon { get; set; }
}
