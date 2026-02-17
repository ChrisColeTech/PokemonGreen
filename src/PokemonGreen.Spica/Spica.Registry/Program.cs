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
            case "diag":   return RunDiag(args);
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

                // 4. Export textures to textures/ subdirectory
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

                // 5. Export static model(s) + clip-only DAEs
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

                    // 6. Clip-only DAEs → clips/ (attached to first model)
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

                // 7. Write manifest.json — same format as OhanaCli
                var manifest = new
                {
                    version = 1,
                    mode = "split-model-anims",
                    textures = textureFileNames,
                    models = modelEntries
                };

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(pokemonDir, "manifest.json"), manifestJson);

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

    // ── diag ─────────────────────────────────────────────────────────

    static int RunDiag(string[] args)
    {
        string garcPath = args.Length > 1 ? args[1] : null;
        if (garcPath == null)
        {
            Console.Error.WriteLine("Usage: Spica.Registry diag <garc-file>");
            return 1;
        }

        using var fs = new FileStream(garcPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var garcEntries = GARC.GetEntries(fs);

        // Load first Pokemon model (entry 1) + animations (entries 5,6,7)
        H3D scene = LoadEntry(fs, garcEntries[1]);
        if (scene == null || scene.Models.Count == 0)
        {
            Console.Error.WriteLine("No model in entry 1");
            return 1;
        }

        var skeleton = scene.Models[0].Skeleton;

        // Merge texture entries
        foreach (int ti in new[] { 2, 3 })
        {
            H3D ts = LoadEntry(fs, garcEntries[ti]);
            if (ts != null) scene.Merge(ts);
        }

        // Merge animation entries
        foreach (int ai in new[] { 5, 6, 7 })
        {
            H3D ans = LoadEntryWithSkeleton(fs, garcEntries[ai], skeleton);
            if (ans != null) scene.Merge(ans);
        }

        Console.WriteLine($"Skeleton: {skeleton.Count} bones");
        Console.WriteLine($"Animations: {scene.SkeletalAnimations.Count}");
        Console.WriteLine();

        // Dump bone hierarchy
        Console.WriteLine("=== SKELETON BONES ===");
        for (int i = 0; i < skeleton.Count; i++)
        {
            var b = skeleton[i];
            Console.WriteLine($"  [{i,2}] {b.Name,-30} parent={b.ParentIndex,3}  T=({b.Translation.X:F3},{b.Translation.Y:F3},{b.Translation.Z:F3}) R=({b.Rotation.X:F3},{b.Rotation.Y:F3},{b.Rotation.Z:F3}) S=({b.Scale.X:F3},{b.Scale.Y:F3},{b.Scale.Z:F3})");
        }

        Console.WriteLine();

        // Dump first animation element details
        for (int a = 0; a < Math.Min(scene.SkeletalAnimations.Count, 1); a++)
        {
            var anim = scene.SkeletalAnimations[a];
            Console.WriteLine($"=== ANIMATION [{a}] \"{anim.Name}\" frames={anim.FramesCount} elements={anim.Elements.Count} ===");

            var boneNames = new HashSet<string>();
            for (int i = 0; i < skeleton.Count; i++)
                boneNames.Add(skeleton[i].Name);

            var elemNames = new HashSet<string>();

            foreach (var elem in anim.Elements)
            {
                elemNames.Add(elem.Name);
                string typeStr = elem.PrimitiveType.ToString();
                string detail = "";

                if (elem.Content is H3DAnimTransform t)
                {
                    detail = $"TX={t.TranslationX.Exists} TY={t.TranslationY.Exists} TZ={t.TranslationZ.Exists} " +
                             $"RX={t.RotationX.Exists} RY={t.RotationY.Exists} RZ={t.RotationZ.Exists} " +
                             $"SX={t.ScaleX.Exists} SY={t.ScaleY.Exists} SZ={t.ScaleZ.Exists}";
                }
                else if (elem.Content is H3DAnimQuatTransform qt)
                {
                    detail = $"HasT={qt.HasTranslation}({qt.Translations.Count}) HasR={qt.HasRotation}({qt.Rotations.Count}) HasS={qt.HasScale}({qt.Scales.Count})";
                }
                else if (elem.Content is H3DAnimMtxTransform mt)
                {
                    detail = $"Frames={mt.Frames.Count}";
                }

                bool matchesBone = boneNames.Contains(elem.Name);
                string marker = matchesBone ? "  " : "!!";
                Console.WriteLine($"  {marker} {elem.Name,-30} type={typeStr,-15} target={elem.TargetType,-10} {detail}");
            }

            Console.WriteLine();
            Console.WriteLine("=== BONES WITHOUT ANIMATION ELEMENT ===");
            for (int i = 0; i < skeleton.Count; i++)
            {
                if (!elemNames.Contains(skeleton[i].Name))
                    Console.WriteLine($"  [{i,2}] {skeleton[i].Name}");
            }

            Console.WriteLine();
            Console.WriteLine("=== ANIMATION ELEMENTS NOT MATCHING ANY BONE ===");
            foreach (var elem in anim.Elements)
            {
                if (!boneNames.Contains(elem.Name))
                    Console.WriteLine($"  {elem.Name} (type={elem.PrimitiveType})");
            }
        }

        return 0;
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

    // Semantic name resolution — shared slot maps with OhanaCli and SpicaCli.
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

class RegistryFile
{
    public string Source { get; set; }
    public int TotalEntries { get; set; }
    public int ScannedEntries { get; set; }
    public List<EntryInfo> Entries { get; set; }
    public List<PokemonGroup> Pokemon { get; set; }
}
