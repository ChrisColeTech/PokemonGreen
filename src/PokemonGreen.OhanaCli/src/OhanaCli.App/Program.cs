using System.CommandLine;
using Ohana3DS_Rebirth.Ohana;
using Ohana3DS_Rebirth.Ohana.Compressions;
using Ohana3DS_Rebirth.Ohana.Containers;
using Ohana3DS_Rebirth.Ohana.Models.GenericFormats;

var rootCommand = new RootCommand("OhanaCli - Pokemon 3DS model converter");

// info command: dump model metadata
var infoArg = new Argument<FileInfo>("file", "Path to a model file (GfModel, BCH, GARC, etc.)");
var infoCommand = new Command("info", "Show model metadata (meshes, materials, textures)") { infoArg };
infoCommand.SetHandler(InfoHandler, infoArg);
rootCommand.AddCommand(infoCommand);

// convert command: convert model to DAE + PNG textures
var convertInputArg = new Argument<FileInfo>("input", "Path to a model file");
var convertOutputArg = new Argument<DirectoryInfo>("output", "Output directory for DAE + textures");
var convertFormatOpt = new Option<string>("--format", () => "dae", "Export format (dae or obj)");
var convertCommand = new Command("convert", "Convert a model file to DAE/OBJ with textures") { convertInputArg, convertOutputArg, convertFormatOpt };
convertCommand.SetHandler(ConvertHandler, convertInputArg, convertOutputArg, convertFormatOpt);
rootCommand.AddCommand(convertCommand);

// batch command: bulk convert a folder
var batchInputArg = new Argument<DirectoryInfo>("input-dir", "Directory containing model files");
var batchOutputArg = new Argument<DirectoryInfo>("output-dir", "Output directory");
var batchFormatOpt = new Option<string>("--format", () => "dae", "Export format (dae or obj)");
var batchCommand = new Command("batch", "Bulk convert all model files in a directory") { batchInputArg, batchOutputArg, batchFormatOpt };
batchCommand.SetHandler(BatchHandler, batchInputArg, batchOutputArg, batchFormatOpt);
rootCommand.AddCommand(batchCommand);

// convert-all command: recursively find and process all GARC archives
var convertAllInputArg = new Argument<DirectoryInfo>("input-dir", "Root directory to scan for GARC archives");
var convertAllOutputArg = new Argument<DirectoryInfo>("output-dir", "Output directory for all exports");
var convertAllFormatOpt = new Option<string>("--format", () => "dae", "Export format (dae or obj)");
var convertAllCommand = new Command("convert-all", "Recursively find all GARC archives and convert models") { convertAllInputArg, convertAllOutputArg, convertAllFormatOpt };
convertAllCommand.SetHandler(ConvertAllHandler, convertAllInputArg, convertAllOutputArg, convertAllFormatOpt);
rootCommand.AddCommand(convertAllCommand);

return await rootCommand.InvokeAsync(args);

// ---- Handlers ----

static void InfoHandler(FileInfo file)
{
    if (!file.Exists) { Console.Error.WriteLine($"File not found: {file.FullName}"); return; }

    try
    {
        var loaded = FileIO.load(file.FullName);
        if (loaded.type == FileIO.formatType.model)
        {
            var models = (RenderBase.OModelGroup)loaded.data;
            Console.WriteLine($"File: {file.Name}");
            Console.WriteLine($"Models: {models.model.Count}");
            Console.WriteLine($"Textures: {models.texture.Count}");
            foreach (var tex in models.texture)
                Console.WriteLine($"  Texture: {tex.name} ({tex.texture.Width}x{tex.texture.Height})");

            for (int i = 0; i < models.model.Count; i++)
            {
                var mdl = models.model[i];
                Console.WriteLine($"\nModel[{i}]: {mdl.name}");
                Console.WriteLine($"  Meshes: {mdl.mesh.Count}");
                Console.WriteLine($"  Materials: {mdl.material.Count}");
                for (int m = 0; m < mdl.mesh.Count; m++)
                {
                    var mesh = mdl.mesh[m];
                    var mat = mesh.materialId < mdl.material.Count ? mdl.material[mesh.materialId] : null;
                    Console.WriteLine($"  Mesh[{m}] \"{mesh.name}\" -> Material[{mesh.materialId}] \"{mat?.name}\" -> Texture \"{mat?.name0}\"");
                }
            }
        }
        else if (loaded.type == FileIO.formatType.container)
        {
            var container = (OContainer)loaded.data;
            Console.WriteLine($"Container: {file.Name}");
            Console.WriteLine($"Files: {container.content.Count}");
            for (int i = 0; i < container.content.Count; i++)
            {
                var entry = container.content[i];
                var size = entry.loadFromDisk ? entry.fileLength : (uint)(entry.data?.Length ?? 0);
                Console.WriteLine($"  [{i}] {entry.name ?? "(unnamed)"} ({size} bytes)");
            }
        }
        else
        {
            Console.WriteLine($"File type: {loaded.type}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}

static void ConvertHandler(FileInfo input, DirectoryInfo output, string format)
{
    if (!input.Exists) { Console.Error.WriteLine($"File not found: {input.FullName}"); return; }

    try
    {
        var loaded = FileIO.load(input.FullName);
        if (loaded.type == FileIO.formatType.model)
        {
            var models = (RenderBase.OModelGroup)loaded.data;
            ExportModel(models, input.Name, output.FullName, format);
        }
        else if (loaded.type == FileIO.formatType.container)
        {
            var container = (OContainer)loaded.data;
            Console.WriteLine($"Container with {container.content.Count} files, processing each...");
            int converted = 0, skipped = 0, errors = 0;
            for (int i = 0; i < container.content.Count; i++)
            {
                var entry = container.content[i];
                try
                {
                    byte[] entryData = ReadEntryData(container, entry);
                    if (entryData == null || entryData.Length == 0) { skipped++; continue; }

                    var sub = FileIO.load(new MemoryStream(entryData));
                    if (sub.type == FileIO.formatType.model)
                    {
                        var models = (RenderBase.OModelGroup)sub.data;
                        var name = entry.name ?? $"sub_{i}";
                        ExportModel(models, name, output.FullName, format);
                        converted++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    if (errors <= 3)
                        Console.Error.WriteLine($"  Skipping [{i}] {entry.name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            Console.WriteLine($"\nDone. Converted: {converted}, Skipped: {skipped}, Errors: {errors}");
        }
        else
        {
            Console.Error.WriteLine($"Unsupported file type: {loaded.type}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}

static void BatchHandler(DirectoryInfo inputDir, DirectoryInfo outputDir, string format)
{
    if (!inputDir.Exists) { Console.Error.WriteLine($"Directory not found: {inputDir.FullName}"); return; }

    var files = inputDir.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    Console.WriteLine($"Found {files.Count} files in {inputDir.FullName}");

    int success = 0, failed = 0, skipped = 0;
    foreach (var file in files)
    {
        if (file.Length == 0) { skipped++; continue; }
        try
        {
            ConvertHandler(file, outputDir, format);
            success++;
        }
        catch
        {
            failed++;
        }
    }
    Console.WriteLine($"\nDone. Success: {success}, Failed: {failed}, Skipped (empty): {skipped}");
}

static void ConvertAllHandler(DirectoryInfo inputDir, DirectoryInfo outputDir, string format)
{
    if (!inputDir.Exists) { Console.Error.WriteLine($"Directory not found: {inputDir.FullName}"); return; }

    Directory.CreateDirectory(outputDir.FullName);

    const uint GARC_MAGIC = 0x47415243; // "CRAG" as read by BitConverter on little-endian

    // Find all files recursively
    var allFiles = inputDir.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    Console.WriteLine($"Scanning {allFiles.Count} files in {inputDir.FullName} for GARC archives...");

    // Filter to GARCs by checking magic bytes
    var garcFiles = new List<FileInfo>();
    foreach (var file in allFiles)
    {
        if (file.Length < 4) continue;
        try
        {
            using var fs = file.OpenRead();
            var magic = new byte[4];
            if (fs.Read(magic, 0, 4) == 4 && BitConverter.ToUInt32(magic, 0) == GARC_MAGIC)
                garcFiles.Add(file);
        }
        catch { /* skip unreadable files */ }
    }

    Console.WriteLine($"Found {garcFiles.Count} GARC archives.\n");

    // Per-GARC stats tracking
    var garcStats = new List<GarcStats>();
    int totalModels = 0, totalTextures = 0, totalErrors = 0;

    foreach (var garcFile in garcFiles)
    {
        // Build subfolder name from relative path, replacing separators with _
        var relativePath = Path.GetRelativePath(inputDir.FullName, garcFile.FullName);
        var subfolderName = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_') ?? "";
        var fileName = Path.GetFileNameWithoutExtension(garcFile.Name);
        if (!string.IsNullOrEmpty(subfolderName))
            subfolderName = subfolderName + "_" + fileName;
        else
            subfolderName = fileName;

        var garcOutputDir = Path.Combine(outputDir.FullName, subfolderName);

        Console.WriteLine($"--- Processing GARC: {relativePath} -> {subfolderName}/ ---");

        var stats = new GarcStats { Name = subfolderName };

        try
        {
            var loaded = FileIO.load(garcFile.FullName);
            if (loaded.type != FileIO.formatType.container)
            {
                Console.WriteLine($"  Not a container (type={loaded.type}), skipping.");
                stats.AddSkip($"GARC not a container (type={loaded.type})");
                garcStats.Add(stats);
                continue;
            }

            var container = (OContainer)loaded.data;
            Console.WriteLine($"  Entries: {container.content.Count}");

            // Single-pass streaming: process entries, hold onto current model
            // until we've collected its trailing texture entries, then export the group.
            RenderBase.OModelGroup pendingModel = null;
            List<RenderBase.OModelGroup> pendingTextures = new();
            int pendingModelIndex = -1;

            void FlushPendingModel()
            {
                if (pendingModel == null) return;
                string folderName = DeriveFolderName(pendingModel, pendingTextures, pendingModelIndex);
                try
                {
                    int texCount = ExportModelGrouped(pendingModel, pendingTextures, folderName, garcOutputDir, format);
                    stats.Converted++;
                    stats.Textures += texCount;
                }
                catch (Exception ex)
                {
                    stats.Errors++;
                    stats.AddError($"Export failed: {ex.Message} [entry={pendingModelIndex}, folder={folderName}]");
                    if (stats.Errors <= 5)
                        Console.Error.WriteLine($"  Export error [{pendingModelIndex}] {folderName}: {ex.Message}");
                }
                pendingModel = null;
                pendingTextures = new();
                pendingModelIndex = -1;
            }

            for (int i = 0; i < container.content.Count; i++)
            {
                var entry = container.content[i];
                try
                {
                    byte[] data = ReadEntryData(container, entry);
                    if (data == null || data.Length == 0)
                    {
                        // Empty entry breaks a model+texture group
                        FlushPendingModel();
                        stats.AddSkip("empty entry");
                        continue;
                    }

                    var sub = FileIO.load(new MemoryStream(data));
                    if (sub.type == FileIO.formatType.model)
                    {
                        var models = (RenderBase.OModelGroup)sub.data;
                        bool hasMeshes = models.model.Count > 0 && models.model.Any(m => m.mesh.Count > 0);
                        bool hasTextures = models.texture.Count > 0;

                        if (hasMeshes)
                        {
                            // New model — flush any pending model first
                            FlushPendingModel();
                            pendingModel = models;
                            pendingModelIndex = i;
                        }
                        else if (hasTextures && pendingModel != null)
                        {
                            // Texture-only entry following a model — group it
                            pendingTextures.Add(models);
                        }
                        else if (hasTextures)
                        {
                            stats.AddSkip("texture entry not following a model");
                        }
                        else
                        {
                            FlushPendingModel();
                            stats.AddSkip("model with no meshes or textures");
                        }
                    }
                    else
                    {
                        // Non-model entry breaks the group
                        FlushPendingModel();
                        if (sub.type == FileIO.formatType.image)
                            stats.AddSkip("image/texture (standalone)");
                        else if (sub.type == FileIO.formatType.container)
                            stats.AddSkip("nested container");
                        else
                        {
                            string ext = entry.name != null ? Path.GetExtension(entry.name) : "";
                            stats.AddSkip($"unsupported format (ext={ext})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    FlushPendingModel();
                    stats.Errors++;
                    string ext = entry.name != null ? Path.GetExtension(entry.name) : "";
                    stats.AddError($"{ex.Message} [ext={ext}]");
                    if (stats.Errors <= 5)
                        Console.Error.WriteLine($"  Error [{i}] {entry.name}: {ex.Message}");
                }
            }
            // Flush last pending model
            FlushPendingModel();
        }
        catch (Exception ex)
        {
            stats.Errors++;
            stats.AddError(ex.Message);
            Console.Error.WriteLine($"  Failed to load GARC: {ex.Message}");
        }

        totalModels += stats.Converted;
        totalTextures += stats.Textures;
        totalErrors += stats.Errors;
        garcStats.Add(stats);

        Console.WriteLine($"  Result: {stats.Converted} models, {stats.Textures} textures, {stats.SkipReasons.Values.Sum()} skipped, {stats.Errors} errors\n");
    }

    // Build summary report
    var report = new System.Text.StringBuilder();
    report.AppendLine("===== CONVERSION SUMMARY =====");
    report.AppendLine($"Total GARCs found: {garcFiles.Count}");
    report.AppendLine($"Total GARCs processed: {garcStats.Count}");
    report.AppendLine($"Total models converted: {totalModels}");
    report.AppendLine($"Total textures exported: {totalTextures}");
    report.AppendLine($"Total errors: {totalErrors}");
    int totalSkipped = garcStats.Sum(s => s.SkipReasons.Values.Sum());
    report.AppendLine($"Total skipped: {totalSkipped}");

    // GARCs that produced output
    var productive = garcStats.Where(s => s.Converted > 0).OrderByDescending(s => s.Converted).ToList();
    report.AppendLine();
    report.AppendLine($"===== GARCs WITH MODELS ({productive.Count}) =====");
    foreach (var s in productive)
    {
        report.AppendLine($"  {s.Name}: {s.Converted} models, {s.Textures} textures, {s.Errors} errors");
    }

    // GARCs with only errors (no models, no skips — tried to parse but failed)
    var errorOnly = garcStats.Where(s => s.Converted == 0 && s.Errors > 0).OrderByDescending(s => s.Errors).ToList();
    report.AppendLine();
    report.AppendLine($"===== GARCs WITH ERRORS ONLY ({errorOnly.Count}) =====");
    foreach (var s in errorOnly)
    {
        var topError = s.ErrorMessages.OrderByDescending(e => e.Value).FirstOrDefault();
        report.AppendLine($"  {s.Name}: {s.Errors} errors, {s.SkipReasons.Values.Sum()} skipped — {topError.Key ?? "unknown"}");
    }

    // GARCs with no models and no errors (all skipped — data archives)
    var dataOnly = garcStats.Where(s => s.Converted == 0 && s.Errors == 0).ToList();
    report.AppendLine();
    report.AppendLine($"===== GARCs WITH NO MODELS ({dataOnly.Count}) =====");
    foreach (var s in dataOnly)
    {
        var topReason = s.SkipReasons.OrderByDescending(r => r.Value).FirstOrDefault();
        report.AppendLine($"  {s.Name}: {s.SkipReasons.Values.Sum()} entries — {topReason.Key ?? "unknown"} (x{topReason.Value})");
    }

    // Aggregate skip reasons across all GARCs
    var allSkipReasons = new Dictionary<string, int>();
    foreach (var s in garcStats)
    {
        foreach (var kvp in s.SkipReasons)
        {
            if (allSkipReasons.ContainsKey(kvp.Key))
                allSkipReasons[kvp.Key] += kvp.Value;
            else
                allSkipReasons[kvp.Key] = kvp.Value;
        }
    }

    report.AppendLine();
    report.AppendLine("===== SKIP REASONS (WHY ENTRIES WERE NOT CONVERTED) =====");
    foreach (var kvp in allSkipReasons.OrderByDescending(e => e.Value))
    {
        report.AppendLine($"  {kvp.Key}: {kvp.Value}");
    }

    // Aggregate error messages across all GARCs
    var allErrors = new Dictionary<string, int>();
    foreach (var s in garcStats)
    {
        foreach (var kvp in s.ErrorMessages)
        {
            if (allErrors.ContainsKey(kvp.Key))
                allErrors[kvp.Key] += kvp.Value;
            else
                allErrors[kvp.Key] = kvp.Value;
        }
    }

    if (allErrors.Count > 0)
    {
        report.AppendLine();
        report.AppendLine("===== ERROR DETAILS =====");
        foreach (var kvp in allErrors.OrderByDescending(e => e.Value).Take(30))
        {
            report.AppendLine($"  (x{kvp.Value}) {kvp.Key}");
        }
    }

    var reportText = report.ToString();
    Console.WriteLine();
    Console.WriteLine(reportText);

    // Write report to file
    var reportPath = Path.Combine(outputDir.FullName, "report.txt");
    File.WriteAllText(reportPath, reportText);
    Console.WriteLine($"Report written to: {reportPath}");
}

static byte[] ReadEntryData(OContainer container, OContainer.fileEntry entry)
{
    if (!entry.loadFromDisk)
        return entry.data;

    if (container.data == null || entry.fileLength == 0)
        return Array.Empty<byte>();

    container.data.Seek(entry.fileOffset, SeekOrigin.Begin);
    byte[] buffer = new byte[entry.fileLength];
    container.data.Read(buffer, 0, buffer.Length);

    if (entry.doDecompression && buffer.Length > 0 && buffer[0] == 0x11)
    {
        return LZSS_Ninty.decompress(buffer);
    }

    return buffer;
}

static int ExportModel(RenderBase.OModelGroup models, string sourceName, string outputDir, string format)
{
    if (models.model.Count == 0 && models.texture.Count == 0)
        return 0;

    int textureCount = 0;
    var baseName = Path.GetFileNameWithoutExtension(sourceName);
    var outDir = Path.Combine(outputDir, baseName);
    Directory.CreateDirectory(outputDir);
    Directory.CreateDirectory(outDir);

    // Export textures as PNG
    foreach (var tex in models.texture)
    {
        var texPath = Path.Combine(outDir, tex.name + ".png");
        tex.texture.Save(texPath);
        textureCount++;
        Console.WriteLine($"  Texture: {tex.name}.png ({tex.texture.Width}x{tex.texture.Height})");
    }

    // Export each model
    for (int i = 0; i < models.model.Count; i++)
    {
        var mdl = models.model[i];
        var modelName = mdl.name ?? $"model_{i}";
        var outFile = Path.Combine(outDir, modelName);

        switch (format.ToLower())
        {
            case "dae":
                DAE.export(models, outFile + ".dae", i);
                break;
            case "obj":
                OBJ.export(models, outFile + ".obj", i);
                break;
            default:
                Console.Error.WriteLine($"Unknown format: {format}");
                return textureCount;
        }

        Console.WriteLine($"  Model: {modelName}.{format}");
        Console.WriteLine($"    Meshes: {mdl.mesh.Count}, Materials: {mdl.material.Count}");
        for (int m = 0; m < mdl.mesh.Count; m++)
        {
            var mesh = mdl.mesh[m];
            var mat = mesh.materialId < mdl.material.Count ? mdl.material[mesh.materialId] : null;
            Console.WriteLine($"    Mesh \"{mesh.name}\" -> Material \"{mat?.name}\" -> Texture \"{mat?.name0}\"");
        }
    }

    return textureCount;
}

/// <summary>
/// Derive a meaningful folder name from model/texture data.
/// Tries to extract a Pokemon ID like "pm0001_00" from texture names.
/// Falls back to the model name, then to "entry_NNNNN".
/// </summary>
static string DeriveFolderName(RenderBase.OModelGroup models, List<RenderBase.OModelGroup> extraTextures, int entryIndex)
{
    // Try texture names first (most reliable source of Pokemon IDs)
    var allTexNames = models.texture.Select(t => t.name).ToList();
    foreach (var extra in extraTextures)
        allTexNames.AddRange(extra.texture.Select(t => t.name));

    // Look for pmNNNN_NN pattern in texture names
    foreach (var texName in allTexNames)
    {
        if (texName == null || texName.StartsWith("DummyTex")) continue;
        var match = System.Text.RegularExpressions.Regex.Match(texName, @"(pm\d{4}_\d{2})");
        if (match.Success)
            return match.Groups[1].Value;
    }

    // Try model name
    if (models.model.Count > 0)
    {
        var name = models.model[0].name;
        if (!string.IsNullOrEmpty(name) && name != "model")
            return name;
    }

    // Try any non-dummy texture name as prefix
    foreach (var texName in allTexNames)
    {
        if (texName == null || texName.StartsWith("DummyTex")) continue;
        // Strip the suffix like _Body1.tga to get a base name
        var baseName = System.Text.RegularExpressions.Regex.Replace(texName, @"_[^_]+\.\w+$", "");
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
    }

    return $"entry_{entryIndex:D5}";
}

/// <summary>
/// Export a model with its grouped texture entries into a single folder.
/// </summary>
static int ExportModelGrouped(RenderBase.OModelGroup models, List<RenderBase.OModelGroup> extraTextures, string folderName, string garcOutputDir, string format)
{
    var outDir = Path.Combine(garcOutputDir, folderName);
    Directory.CreateDirectory(garcOutputDir);
    Directory.CreateDirectory(outDir);

    int textureCount = 0;
    var exportedTextures = new HashSet<string>();

    // Export textures from the model entry itself
    foreach (var tex in models.texture)
    {
        var texFileName = tex.name + ".png";
        if (exportedTextures.Add(texFileName))
        {
            tex.texture.Save(Path.Combine(outDir, texFileName));
            textureCount++;
        }
    }

    // Export textures from grouped texture entries (take first set as primary, rest as variants)
    for (int t = 0; t < extraTextures.Count; t++)
    {
        var texGroup = extraTextures[t];
        // Subfolder for variants: textures/ for primary, textures_shiny/ for second, etc.
        string texSubDir = outDir;
        if (t == 1) { texSubDir = Path.Combine(outDir, "shiny"); Directory.CreateDirectory(texSubDir); }
        else if (t > 1) { texSubDir = Path.Combine(outDir, $"variant_{t}"); Directory.CreateDirectory(texSubDir); }

        foreach (var tex in texGroup.texture)
        {
            var texFileName = tex.name + ".png";
            var texPath = Path.Combine(texSubDir, texFileName);
            tex.texture.Save(texPath);
            textureCount++;
        }
    }

    // Export models
    for (int i = 0; i < models.model.Count; i++)
    {
        var mdl = models.model[i];
        var modelName = mdl.name ?? $"model_{i}";
        var outFile = Path.Combine(outDir, modelName);

        switch (format.ToLower())
        {
            case "dae":
                DAE.export(models, outFile + ".dae", i);
                break;
            case "obj":
                OBJ.export(models, outFile + ".obj", i);
                break;
            default:
                Console.Error.WriteLine($"Unknown format: {format}");
                return textureCount;
        }

        Console.WriteLine($"  {folderName}/{modelName}.{format} ({mdl.mesh.Count} meshes, {mdl.material.Count} materials, {textureCount} textures)");
    }

    return textureCount;
}

// Stats tracker for a single GARC archive
class GarcStats
{
    public string Name { get; set; } = "";
    public int Converted { get; set; }
    public int Textures { get; set; }
    public int Errors { get; set; }
    public Dictionary<string, int> ErrorMessages { get; } = new();
    public Dictionary<string, int> SkipReasons { get; } = new();

    public void AddError(string message)
    {
        if (ErrorMessages.ContainsKey(message))
            ErrorMessages[message]++;
        else
            ErrorMessages[message] = 1;
    }

    public void AddSkip(string reason)
    {
        if (SkipReasons.ContainsKey(reason))
            SkipReasons[reason]++;
        else
            SkipReasons[reason] = 1;
    }
}
