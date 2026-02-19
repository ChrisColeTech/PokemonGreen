// SwitchToolboxCli — Headless Trinity model export tool
//
// Usage:
//   switchtool --arc <arcDir> --model <romfsPath> --output <outDir>
//   switchtool --arc <arcDir> --all --output <outDir>         # export ALL models
//   switchtool --arc <arcDir> --list                           # list available .trmdl files
//
// Output (split model + animation clip pattern):
//   <output>/model.dae          — mesh + skeleton + skin + materials (no animations)
//   <output>/textures/*.png     — BNTX textures as PNG
//   <output>/manifest.json      — links model + textures

using SwitchToolboxCli.Core.Archive;
using SwitchToolboxCli.Core.Decoders;
using SwitchToolboxCli.Core.Exporters;
using SwitchToolboxCli.Core.Texture;
using SwitchToolboxCli.Core.Utils;
using SwitchToolboxCli.Core.Flatbuffers.TR.Model;

// ---------- Parse Args ----------

string? arcDir = null;
string? modelPath = null;
string? outputDir = null;
bool listMode = false;
bool allMode = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--arc" when i + 1 < args.Length: arcDir = args[++i]; break;
        case "--model" when i + 1 < args.Length: modelPath = args[++i]; break;
        case "--output" or "-o" when i + 1 < args.Length: outputDir = args[++i]; break;
        case "--list": listMode = true; break;
        case "--all": allMode = true; break;
    }
}

if (string.IsNullOrWhiteSpace(arcDir))
{
    Console.Error.WriteLine("Usage: switchtool --arc <arcDir> [--model <romfsPath>] [--output <outDir>] [--list]");
    return 1;
}

// ---------- Load Hash Cache ----------

Console.WriteLine("Loading hash cache...");
var hashCache = new TrpakHashCache();
string hashFile = Path.Combine(AppContext.BaseDirectory, "hashes_inside_fd.txt");
if (File.Exists(hashFile))
{
    hashCache.LoadHashList(File.ReadAllLines(hashFile));
    Console.WriteLine($"  {hashCache.Count} entries loaded.");
}
else
{
    Console.Error.WriteLine($"  WARNING: {hashFile} not found — will use raw hashes.");
}

// ---------- Open Archive ----------

Console.WriteLine($"Opening archive: {arcDir}");
var loader = new TrpfsLoader(arcDir, hashCache);
Console.WriteLine($"  {loader.FileCount} files in descriptor, {loader.PackNames.Count} packs.");

// ---------- List Mode ----------

if (listMode)
{
    Console.WriteLine("\nAvailable .trmdl files:");
    int count = 0;
    foreach (var (hash, name) in loader.FindFilesByExtension(".trmdl"))
    {
        Console.WriteLine($"  {name}");
        count++;
    }
    Console.WriteLine($"\n{count} model(s) found.");
    return 0;
}

// ---------- Batch All Mode ----------

if (allMode)
{
    outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "export_all");
    Directory.CreateDirectory(outputDir);
    Console.WriteLine($"\nBatch exporting ALL models to: {outputDir}");

    var allModels = loader.FindFilesByExtension(".trmdl").ToList();
    Console.WriteLine($"  Found {allModels.Count} models.");

    int success = 0, failed = 0;
    for (int mi = 0; mi < allModels.Count; mi++)
    {
        var (hash, mpath) = allModels[mi];
        // Use the parent folder name as the output directory name (e.g. pm0025_00_00)
        string modelDirName = Path.GetFileNameWithoutExtension(mpath);
        string modelOutDir = Path.Combine(outputDir, modelDirName);

        Console.WriteLine($"\n[{mi + 1}/{allModels.Count}] {mpath}");
        try
        {
            int result = ExportModel(loader, mpath, modelOutDir);
            if (result == 0) success++; else failed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
            failed++;
        }
    }

    Console.WriteLine($"\n=== Batch complete: {success} succeeded, {failed} failed out of {allModels.Count} ===");
    return 0;
}

// ---------- Export Mode ----------

if (string.IsNullOrWhiteSpace(modelPath))
{
    Console.Error.WriteLine("ERROR: --model <romfsPath> is required for export. Use --list to see available models.");
    return 1;
}

outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(modelPath));
return ExportModel(loader, modelPath, outputDir);

// ---------- Export a single model ----------

static int ExportModel(TrpfsLoader loader, string modelPath, string outputDir)
{
Directory.CreateDirectory(outputDir);

Console.WriteLine($"Extracting model: {modelPath}");
Console.WriteLine($"Output: {outputDir}");

// Normalize the model path
string normalizedModel = modelPath.Replace('\\', '/').TrimStart('/');

// ---------- Extract Files to Temp ----------

string tempRoot = Path.Combine(Path.GetTempPath(), "SwitchToolboxCli", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);

try
{
    // Extract the TRMDL itself
    var trmdlBytes = loader.ExtractFile(normalizedModel);
    if (trmdlBytes == null)
    {
        Console.Error.WriteLine($"ERROR: Could not find '{normalizedModel}' in archive.");
        return 1;
    }

    WriteExtractedFile(tempRoot, normalizedModel, trmdlBytes);
    Console.WriteLine($"  Extracted: {normalizedModel} ({trmdlBytes.Length} bytes)");

    // Parse TRMDL to find dependencies
    var mdl = FlatBufferConverter.DeserializeFrom<TRMDL>(trmdlBytes);
    string modelDir = GetDirectoryOrEmpty(normalizedModel);

    var pending = new Queue<string>();
    var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedModel };

    // Enqueue mesh files
    if (mdl.Meshes != null)
    {
        foreach (var mesh in mdl.Meshes)
        {
            if (!string.IsNullOrWhiteSpace(mesh?.PathName))
                EnqueuePath(modelDir, mesh.PathName, pending);
        }
    }

    // Enqueue material files
    if (mdl.Materials != null)
    {
        foreach (var mat in mdl.Materials)
        {
            if (!string.IsNullOrWhiteSpace(mat))
                EnqueuePath(modelDir, mat, pending);
        }
    }

    // Enqueue skeleton
    if (mdl.Skeleton != null && !string.IsNullOrWhiteSpace(mdl.Skeleton.PathName))
    {
        EnqueuePath(modelDir, mdl.Skeleton.PathName, pending);
    }

    // BFS: extract dependencies
    while (pending.Count > 0)
    {
        string relPath = NormalizePath(pending.Dequeue());
        if (!extracted.Add(relPath)) continue;

        var bytes = loader.ExtractFile(relPath);
        if (bytes == null)
        {
            Console.WriteLine($"  SKIP (not found): {relPath}");
            continue;
        }

        WriteExtractedFile(tempRoot, relPath, bytes);
        Console.WriteLine($"  Extracted: {relPath} ({bytes.Length} bytes)");

        string ext = Path.GetExtension(relPath).ToLowerInvariant();
        string dir = GetDirectoryOrEmpty(relPath);

        if (ext == ".trmsh")
        {
            // TRMSH → buffer file
            try
            {
                var msh = FlatBufferConverter.DeserializeFrom<TRMSH>(bytes);
                if (!string.IsNullOrWhiteSpace(msh?.bufferFilePath))
                    EnqueuePath(dir, msh.bufferFilePath, pending);
            }
            catch { /* skip parse errors */ }
        }
        else if (ext == ".trmtr")
        {
            // TRMTR → texture files (BNTX)
            try
            {
                var mtr = FlatBufferConverter.DeserializeFrom<TRMTR>(bytes);
                if (mtr?.Materials != null)
                {
                    foreach (var mat in mtr.Materials)
                    {
                        if (mat?.Textures == null) continue;
                        foreach (var tex in mat.Textures)
                        {
                            if (!string.IsNullOrWhiteSpace(tex?.File))
                                EnqueuePath(dir, tex.File, pending);
                        }
                    }
                }
            }
            catch { /* skip parse errors */ }
        }
    }

    // ---------- Decode Model ----------

    string trmdlOnDisk = Path.Combine(tempRoot, normalizedModel.Replace('/', Path.DirectorySeparatorChar));
    Console.WriteLine("\nDecoding model...");

    var decoder = new TrinityModelDecoder(trmdlOnDisk);
    var exportData = decoder.CreateExportData();
    Console.WriteLine($"  Name: {exportData.Name}");
    Console.WriteLine($"  Submeshes: {exportData.Submeshes.Count}");
    Console.WriteLine($"  Materials: {exportData.Materials.Count}");
    Console.WriteLine($"  Armature: {(exportData.Armature != null ? $"{exportData.Armature.Bones.Count} bones" : "none")}");

    // ---------- Export DAE ----------

    string daeOut = Path.Combine(outputDir, "model.dae");
    Console.WriteLine($"\nExporting DAE: {daeOut}");
    TrinityColladaExporter.Export(daeOut, exportData);
    Console.WriteLine("  Done.");

    // ---------- Dump Eye Material Params (diagnostic) ----------
    foreach (var mat in exportData.Materials)
    {
        if (!mat.Name.Contains("eye", StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine($"\n=== EYE MATERIAL: {mat.Name} (shader: {mat.ShaderName}) ===");
        Console.WriteLine("  Textures:");
        foreach (var tex in mat.Textures)
            Console.WriteLine($"    [{tex.Slot}] {tex.Name} -> {Path.GetFileName(tex.FilePath)}");
        if (mat.FloatParams.Length > 0)
        {
            Console.WriteLine("  FloatParams:");
            foreach (var p in mat.FloatParams)
                Console.WriteLine($"    {p.Name} = {p.Value}");
        }
        if (mat.Vec3Params.Length > 0)
        {
            Console.WriteLine("  Vec3Params:");
            foreach (var p in mat.Vec3Params)
                Console.WriteLine($"    {p.Name} = ({p.Value.X:F3}, {p.Value.Y:F3}, {p.Value.Z:F3})");
        }
        if (mat.Vec4Params.Length > 0)
        {
            Console.WriteLine("  Vec4Params:");
            foreach (var p in mat.Vec4Params)
                Console.WriteLine($"    {p.Name} = ({p.Value.W:F3}, {p.Value.X:F3}, {p.Value.Y:F3}, {p.Value.Z:F3})");
        }
        if (mat.ShaderParams.Count > 0)
        {
            Console.WriteLine("  ShaderParams:");
            foreach (var (name, val) in mat.ShaderParams)
                Console.WriteLine($"    {name} = {val}");
        }
    }

    // ---------- Decode Textures ----------

    Console.WriteLine("\nDecoding textures...");
    string texOutDir = Path.Combine(outputDir, "textures");
    Directory.CreateDirectory(texOutDir);

    int texCount = 0;
    foreach (string bntxFile in Directory.EnumerateFiles(tempRoot, "*.bntx", SearchOption.AllDirectories))
    {
        try
        {
            var bntxBytes = File.ReadAllBytes(bntxFile);
            var textures = BntxDecoder.Decode(bntxBytes);
            foreach (var tex in textures)
            {
                string pngPath = Path.Combine(texOutDir, tex.Name + ".png");
                tex.SavePng(pngPath);
                Console.WriteLine($"  {tex.Name}: {tex.Width}x{tex.Height} → {pngPath}");
                texCount++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARNING: Failed to decode {Path.GetFileName(bntxFile)}: {ex.Message}");
        }
    }
    Console.WriteLine($"  {texCount} texture(s) exported.");

    // ---------- Bake Eye Textures ----------

    foreach (var mat in exportData.Materials)
    {
        if (EyeTextureBaker.IsEyeMaterial(mat))
        {
            EyeTextureBaker.BakeEyeTexture(mat, tempRoot, texOutDir);
        }
    }

    // ---------- Extract Animations ----------

    Console.WriteLine("\nExtracting animations...");
    string animOutDir = Path.Combine(outputDir, "animations");
    Directory.CreateDirectory(animOutDir);

    // Find the model's data directory (e.g. "pokemon/data/pm0025/pm0025_00_00/")
    string modelDataDir = GetDirectoryOrEmpty(normalizedModel);
    int animCount = 0;

    if (exportData.Armature != null)
    {
        foreach (var (hash, animName) in loader.FindFiles(name =>
            name.StartsWith(modelDataDir, StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".tranm", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var animBytes = loader.ExtractFile(hash);
                if (animBytes == null) continue;

                var animFb = FlatBufferConverter.DeserializeFrom<SwitchToolboxCli.Core.Flatbuffers.GF.Animation.Animation>(animBytes);
                string clipName = Path.GetFileNameWithoutExtension(animName);
                var animDecoder = new TrinityAnimationDecoder(animFb, clipName);

                string clipDae = Path.Combine(animOutDir, clipName + ".dae");
                TrinityColladaExporter.ExportWithAnimation(clipDae, exportData, animDecoder);
                Console.WriteLine($"  {clipName}: {animDecoder.FrameCount} frames @ {animDecoder.FrameRate}fps → {clipDae}");
                animCount++;
            }
            catch (Exception ex)
            {
                string shortName = Path.GetFileNameWithoutExtension(animName);
                Console.WriteLine($"  WARNING: Failed to decode {shortName}: {ex.Message}");
            }
        }
    }
    else
    {
        Console.WriteLine("  No armature — skipping animation export.");
    }
    Console.WriteLine($"  {animCount} animation(s) exported.");

    // Copy textures into animations folder so combined DAEs resolve their paths
    if (animCount > 0 && Directory.Exists(texOutDir))
    {
        string animTexDir = Path.Combine(animOutDir, "textures");
        Directory.CreateDirectory(animTexDir);
        foreach (var texFile in Directory.GetFiles(texOutDir, "*.png"))
        {
            File.Copy(texFile, Path.Combine(animTexDir, Path.GetFileName(texFile)), overwrite: true);
        }
        Console.WriteLine($"  Copied {Directory.GetFiles(animTexDir).Length} textures to {animTexDir}");
    }

    // ---------- Summary ----------

    Console.WriteLine($"\n=== Export complete ===");
    Console.WriteLine($"  Model: {daeOut}");
    Console.WriteLine($"  Textures: {texCount} PNGs in {texOutDir}");
    Console.WriteLine($"  Animations: {animCount} clip DAEs in {animOutDir}");
    return 0;
}
finally
{
    // Cleanup temp
    try { Directory.Delete(tempRoot, true); }
    catch { /* ignore cleanup errors */ }
}
}

// ---------- Helpers ----------

static void WriteExtractedFile(string root, string relPath, byte[] data)
{
    string outPath = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
    string? dir = Path.GetDirectoryName(outPath);
    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
    File.WriteAllBytes(outPath, data);
}

static string NormalizePath(string path)
{
    path = (path ?? "").Replace('\\', '/').Trim();
    if (path.StartsWith("romfs://", StringComparison.OrdinalIgnoreCase))
        path = path["romfs://".Length..];
    if (path.StartsWith("trpfs://", StringComparison.OrdinalIgnoreCase))
        path = path["trpfs://".Length..];
    return path.TrimStart('/');
}

static string GetDirectoryOrEmpty(string romfsRelativePath)
{
    string normalized = NormalizePath(romfsRelativePath);
    int lastSlash = normalized.LastIndexOf('/');
    return lastSlash < 0 ? "" : normalized[..(lastSlash + 1)];
}

static void EnqueuePath(string currentDir, string relativePath, Queue<string> pending)
{
    string combined = CombineAndNormalize(currentDir, relativePath);
    pending.Enqueue(combined);
}

static string CombineAndNormalize(string dir, string rel)
{
    string combined = dir + rel.Replace('\\', '/');
    // Resolve .. segments
    var parts = combined.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
    var resolved = new List<string>();
    foreach (var part in parts)
    {
        if (part == "..")
        {
            if (resolved.Count > 0)
                resolved.RemoveAt(resolved.Count - 1);
        }
        else if (part != ".")
        {
            resolved.Add(part);
        }
    }
    return string.Join('/', resolved);
}
