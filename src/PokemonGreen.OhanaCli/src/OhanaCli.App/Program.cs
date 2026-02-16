using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Drawing.Imaging;
using System.IO;

using OhanaCli.Formats;
using OhanaCli.Formats.Compressions;
using OhanaCli.Formats.Containers;
using OhanaCli.Formats.Models;
using OhanaCli.Formats.Models.GenericFormats;
using OhanaCli.Formats.Models.PocketMonsters;
using OhanaCli.Formats.Textures.PocketMonsters;

namespace OhanaCli.App
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("OhanaCli - Pokemon 3DS model extraction tool");

            // --- info command ---
            var infoFileArg = new Argument<string>("file", "Path to the file to inspect");
            var infoCommand = new Command("info", "Display format information about a file") { infoFileArg };
            infoCommand.SetHandler((string file) => RunInfo(file), infoFileArg);
            rootCommand.AddCommand(infoCommand);

            // --- convert command ---
            var convertFileArg = new Argument<string>("file", "Path to the file to convert");
            var convertOutOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory") { IsRequired = true };
            var convertFmtOpt = new Option<string>(new[] { "-f", "--format" }, () => "dae", "Output format (dae or obj)");
            var convertAnimOpt = new Option<int>(new[] { "-a", "--anim" }, () => -1, "Skeletal animation index (-1 = none)");
            var convertDiagOpt = new Option<bool>("--diag", "Enable GfModel diagnostic logging");
            var convertLimitOpt = new Option<int>(new[] { "-n", "--limit" }, () => -1, "Max container entries to process (-1 = all)");
            var convertCommand = new Command("convert", "Convert a file to DAE or OBJ")
            {
                convertFileArg, convertOutOpt, convertFmtOpt, convertAnimOpt, convertDiagOpt, convertLimitOpt
            };
            convertCommand.SetHandler(
                (string file, string output, string format, int anim, bool diag, int limit) =>
                    RunConvert(file, output, format, anim, diag, limit),
                convertFileArg, convertOutOpt, convertFmtOpt, convertAnimOpt, convertDiagOpt, convertLimitOpt);
            rootCommand.AddCommand(convertCommand);

            // --- batch command ---
            var batchDirArg = new Argument<string>("inputDir", "Directory containing files to convert");
            var batchOutOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory") { IsRequired = true };
            var batchFmtOpt = new Option<string>(new[] { "-f", "--format" }, () => "dae", "Output format (dae or obj)");
            var batchCommand = new Command("batch", "Batch convert all files in a directory")
            {
                batchDirArg, batchOutOpt, batchFmtOpt
            };
            batchCommand.SetHandler(
                (string inputDir, string output, string format) => RunBatch(inputDir, output, format),
                batchDirArg, batchOutOpt, batchFmtOpt);
            rootCommand.AddCommand(batchCommand);

            // --- convert-all command ---
            var convertAllDirArg = new Argument<string>("garcDir", "Directory containing .garc files");
            var convertAllOutOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory") { IsRequired = true };
            var convertAllFmtOpt = new Option<string>(new[] { "-f", "--format" }, () => "dae", "Output format (dae or obj)");
            var convertAllCommand = new Command("convert-all", "Mass-extract all GARC files in a directory")
            {
                convertAllDirArg, convertAllOutOpt, convertAllFmtOpt
            };
            convertAllCommand.SetHandler(
                (string garcDir, string output, string format) => RunConvertAll(garcDir, output, format),
                convertAllDirArg, convertAllOutOpt, convertAllFmtOpt);
            rootCommand.AddCommand(convertAllCommand);

            // --- diagnose command ---
            var diagFileArg = new Argument<string>("file", "Path to the GARC file to diagnose");
            var diagStartOpt = new Option<int>("--start", () => 0, "Start entry index");
            var diagEndOpt = new Option<int>("--end", () => -1, "End entry index (-1 = last)");
            var diagnoseCommand = new Command("diagnose", "Hex dump and analyze GARC entries")
            {
                diagFileArg, diagStartOpt, diagEndOpt
            };
            diagnoseCommand.SetHandler(
                (string file, int start, int end) => RunDiagnose(file, start, end),
                diagFileArg, diagStartOpt, diagEndOpt);
            rootCommand.AddCommand(diagnoseCommand);

            return rootCommand.Invoke(args);
        }

        // =========================================================
        // info
        // =========================================================
        static void RunInfo(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return;
            }

            Console.WriteLine($"File: {filePath}");
            Console.WriteLine($"Size: {new FileInfo(filePath).Length:N0} bytes");

            FileIO.LoadedFile loaded = FileIO.load(filePath);
            Console.WriteLine($"Format: {loaded.type}");

            if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup models)
            {
                for (int m = 0; m < models.model.Count; m++)
                {
                    var mdl = models.model[m];
                    Console.WriteLine($"  Model[{m}]: \"{mdl.name}\"");
                    Console.WriteLine($"    Meshes:    {mdl.mesh.Count}");
                    Console.WriteLine($"    Materials: {mdl.material.Count}");
                    Console.WriteLine($"    Bones:     {mdl.skeleton.Count}");
                }
                Console.WriteLine($"  Textures: {models.texture.Count}");
                Console.WriteLine($"  Skeletal animations: {models.skeletalAnimation.list.Count}");
                Console.WriteLine($"  Material animations: {models.materialAnimation.list.Count}");
            }
            else if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
            {
                Console.WriteLine($"  Entries: {container.content.Count}");
                for (int i = 0; i < container.content.Count; i++)
                {
                    try
                    {
                        byte[] entryData = GetEntryData(container, i);
                        string ext = FileIO.getExtension(entryData);
                        Console.WriteLine($"    [{i}] {entryData.Length:N0} bytes  {ext}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    [{i}] error: {ex.Message}");
                    }
                }
            }
            else if (loaded.type == FileIO.formatType.image && loaded.data is RenderBase.OTexture tex)
            {
                Console.WriteLine($"  Texture: \"{tex.name}\" {tex.texture.Width}x{tex.texture.Height}");
            }
            else if (loaded.type == FileIO.formatType.texture && loaded.data is RenderBase.OModelGroup texGroup)
            {
                Console.WriteLine($"  Textures: {texGroup.texture.Count}");
                foreach (var t in texGroup.texture)
                    Console.WriteLine($"    \"{t.name}\" {t.texture.Width}x{t.texture.Height}");
            }
            else if (loaded.type == FileIO.formatType.unsupported)
            {
                Console.Error.WriteLine("  Unsupported format.");
            }
        }

        // =========================================================
        // convert
        // =========================================================
        static void RunConvert(string filePath, string outDir, string format, int animIndex, bool diag, int entryLimit = -1)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return;
            }

            GfModel.DiagnosticLogging = diag;
            Directory.CreateDirectory(outDir);

            FileIO.LoadedFile loaded = FileIO.load(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);

            if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup models)
            {
                ExportModelGroup(models, outDir, baseName, format, animIndex);
            }
            else if (loaded.type == FileIO.formatType.texture && loaded.data is RenderBase.OModelGroup texGroup)
            {
                ExportTextures(texGroup.texture, outDir);
            }
            else if (loaded.type == FileIO.formatType.image && loaded.data is RenderBase.OTexture tex)
            {
                string texPath = Path.Combine(outDir, tex.name + ".png");
                tex.texture.Save(texPath, ImageFormat.Png);
                Console.WriteLine($"  Texture: {texPath}");
            }
            else if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
            {
                int maxEntries = entryLimit > 0 ? Math.Min(entryLimit, container.content.Count) : container.content.Count;
                Console.WriteLine($"  Container with {container.content.Count} entries. Processing {maxEntries}...");

                // Streaming grouping: merge trailing texture/animation entries into preceding model
                RenderBase.OModelGroup? currentModel = null;
                int modelEntryIndex = -1;

                for (int i = 0; i < maxEntries; i++)
                {
                    try
                    {
                        byte[] entryBytes = GetEntryData(container, i);
                        FileIO.LoadedFile entry = FileIO.load(new MemoryStream(entryBytes));

                        if (entry.type == FileIO.formatType.model && entry.data is RenderBase.OModelGroup entryModels)
                        {
                            bool hasMeshes = false;
                            foreach (var m in entryModels.model)
                                if (m.mesh.Count > 0) { hasMeshes = true; break; }

                            if (hasMeshes)
                            {
                                // New model with geometry — flush previous
                                if (currentModel != null)
                                {
                                    string prevDir = Path.Combine(outDir, $"entry_{modelEntryIndex}");
                                    ExportModelGroup(currentModel, prevDir, $"entry_{modelEntryIndex}", format, animIndex);
                                }
                                currentModel = entryModels;
                                modelEntryIndex = i;
                            }
                            else if (currentModel != null)
                            {
                                // Animation-only model entry — merge animations + textures into preceding model
                                foreach (var a in entryModels.skeletalAnimation.list)
                                    currentModel.skeletalAnimation.list.Add(a);
                                foreach (var t in entryModels.texture)
                                    currentModel.texture.Add(t);
                            }
                        }
                        else if (entry.type == FileIO.formatType.texture && entry.data is RenderBase.OModelGroup entryTex)
                        {
                            if (currentModel != null)
                            {
                                foreach (var t in entryTex.texture)
                                    currentModel.texture.Add(t);
                            }
                            else
                            {
                                string entryDir = Path.Combine(outDir, $"entry_{i}");
                                ExportTextures(entryTex.texture, entryDir);
                            }
                        }
                        else if (entry.type == FileIO.formatType.image && entry.data is RenderBase.OTexture entryImg)
                        {
                            if (currentModel != null)
                            {
                                currentModel.texture.Add(entryImg);
                            }
                            else
                            {
                                string entryDir = Path.Combine(outDir, $"entry_{i}");
                                Directory.CreateDirectory(entryDir);
                                entryImg.texture.Save(Path.Combine(entryDir, entryImg.name + ".png"), ImageFormat.Png);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  Entry[{i}] error: {ex.Message}");
                    }
                }

                // Flush final model group
                if (currentModel != null)
                {
                    string finalDir = Path.Combine(outDir, $"entry_{modelEntryIndex}");
                    ExportModelGroup(currentModel, finalDir, $"entry_{modelEntryIndex}", format, animIndex);
                }
            }
            else
            {
                Console.Error.WriteLine($"Unsupported format: {loaded.type}");
            }
        }

        // =========================================================
        // batch
        // =========================================================
        static void RunBatch(string inputDir, string outDir, string format)
        {
            if (!Directory.Exists(inputDir))
            {
                Console.Error.WriteLine($"Directory not found: {inputDir}");
                return;
            }

            string[] files = Directory.GetFiles(inputDir);
            Console.WriteLine($"Batch converting {files.Length} files...");

            int converted = 0, errors = 0;
            foreach (string file in files)
            {
                string baseName = Path.GetFileNameWithoutExtension(file);
                string fileOutDir = Path.Combine(outDir, baseName);
                try
                {
                    RunConvert(file, fileOutDir, format, -1, false);
                    converted++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error converting {file}: {ex.Message}");
                    errors++;
                }
            }

            Console.WriteLine($"Batch complete: {converted} converted, {errors} errors.");
        }

        // =========================================================
        // convert-all (mass GARC extraction)
        // =========================================================
        static void RunConvertAll(string garcDir, string outDir, string format)
        {
            if (!Directory.Exists(garcDir))
            {
                Console.Error.WriteLine($"Directory not found: {garcDir}");
                return;
            }

            string[] garcFiles = Directory.GetFiles(garcDir, "*.garc");
            if (garcFiles.Length == 0)
                garcFiles = Directory.GetFiles(garcDir); // try all files

            Console.WriteLine($"Processing {garcFiles.Length} files from {garcDir}...");

            int totalModels = 0, totalTextures = 0, totalErrors = 0;

            foreach (string garcPath in garcFiles)
            {
                string garcName = Path.GetFileNameWithoutExtension(garcPath);
                Console.WriteLine($"\n=== {garcName} ===");

                try
                {
                    FileIO.LoadedFile loaded = FileIO.load(garcPath);

                    if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
                    {
                        // Streaming single-pass grouping:
                        // Track current model group + trailing textures
                        RenderBase.OModelGroup? currentModel = null;
                        string currentModelName = "";
                        int modelEntryIndex = -1;

                        for (int i = 0; i < container.content.Count; i++)
                        {
                            try
                            {
                                byte[] entryBytes = GetEntryData(container, i);
                                FileIO.LoadedFile entry = FileIO.load(new MemoryStream(entryBytes));

                                if (entry.type == FileIO.formatType.model && entry.data is RenderBase.OModelGroup mdl)
                                {
                                    // Flush previous group
                                    if (currentModel != null)
                                    {
                                        FlushModelGroup(currentModel, outDir, garcName, currentModelName, modelEntryIndex, format, ref totalModels, ref totalTextures, ref totalErrors);
                                    }

                                    currentModel = mdl;
                                    currentModelName = mdl.model.Count > 0 ? mdl.model[0].name : $"model_{i}";
                                    modelEntryIndex = i;
                                }
                                else if (entry.type == FileIO.formatType.texture && entry.data is RenderBase.OModelGroup texMdl)
                                {
                                    // Merge textures into current model group
                                    if (currentModel != null)
                                    {
                                        foreach (var tex in texMdl.texture)
                                            currentModel.texture.Add(tex);
                                    }
                                }
                                else if (entry.type == FileIO.formatType.image && entry.data is RenderBase.OTexture singleTex)
                                {
                                    if (currentModel != null)
                                        currentModel.texture.Add(singleTex);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"  [{i}] Error: {ex.Message}");
                                totalErrors++;
                            }
                        }

                        // Flush final group
                        if (currentModel != null)
                        {
                            FlushModelGroup(currentModel, outDir, garcName, currentModelName, modelEntryIndex, format, ref totalModels, ref totalTextures, ref totalErrors);
                        }
                    }
                    else if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup directModel)
                    {
                        string modelDir = Path.Combine(outDir, garcName);
                        ExportModelGroup(directModel, modelDir, garcName, format, -1);
                        totalModels++;
                        totalTextures += directModel.texture.Count;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Error loading {garcPath}: {ex.Message}");
                    totalErrors++;
                }
            }

            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Models exported:   {totalModels}");
            Console.WriteLine($"Textures exported: {totalTextures}");
            Console.WriteLine($"Errors:            {totalErrors}");
        }

        // =========================================================
        // diagnose
        // =========================================================
        static void RunDiagnose(string filePath, int start, int end)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return;
            }

            FileIO.LoadedFile loaded = FileIO.load(filePath);

            if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
            {
                if (end < 0 || end >= container.content.Count) end = container.content.Count - 1;

                Console.WriteLine($"GARC: {container.content.Count} entries");
                Console.WriteLine();

                for (int i = start; i <= end; i++)
                {
                    byte[] data = GetEntryData(container, i);
                    string ext = FileIO.getExtension(data);
                    string magic = data.Length >= 4
                        ? $"0x{data[0]:X2}{data[1]:X2}{data[2]:X2}{data[3]:X2}"
                        : "N/A";

                    Console.WriteLine($"--- Entry [{i}] ---");
                    Console.WriteLine($"  Size:      {data.Length:N0} bytes");
                    Console.WriteLine($"  Magic:     {magic}");
                    Console.WriteLine($"  Extension: {ext}");

                    // Try format detection
                    try
                    {
                        FileIO.LoadedFile entry = FileIO.load(new MemoryStream(data));
                        Console.WriteLine($"  Detected:  {entry.type}");

                        if (entry.type == FileIO.formatType.model && entry.data is RenderBase.OModelGroup mdl)
                        {
                            for (int m = 0; m < mdl.model.Count; m++)
                            {
                                Console.WriteLine($"    Model[{m}] \"{mdl.model[m].name}\" meshes={mdl.model[m].mesh.Count} bones={mdl.model[m].skeleton.Count}");
                            }
                            Console.WriteLine($"    Textures: {mdl.texture.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Parse error: {ex.Message}");
                    }

                    // Hex dump first 64 bytes
                    int dumpLen = Math.Min(data.Length, 64);
                    Console.Write("  Hex: ");
                    for (int b = 0; b < dumpLen; b++)
                    {
                        if (b > 0 && b % 16 == 0) Console.Write("\n       ");
                        Console.Write($"{data[b]:X2} ");
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
            else
            {
                Console.Error.WriteLine($"File is not a container (detected: {loaded.type})");
            }
        }

        // =========================================================
        // Helpers
        // =========================================================
        static void ExportModelGroup(RenderBase.OModelGroup models, string outDir, string baseName, string format, int animIndex)
        {
            Directory.CreateDirectory(outDir);

            // Auto-export first animation when available and no explicit index given
            int effectiveAnimIndex = animIndex;
            if (effectiveAnimIndex == -1 && models.skeletalAnimation.list.Count > 0)
                effectiveAnimIndex = 0;

            // Export textures
            ExportTextures(models.texture, outDir);

            // Export each model
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int m = 0; m < models.model.Count; m++)
            {
                string modelName = models.model[m].name ?? baseName;
                if (string.IsNullOrEmpty(modelName)) modelName = baseName;

                // Deduplicate names: append _1, _2, etc. on collision
                string uniqueName = modelName;
                int suffix = 1;
                while (!usedNames.Add(uniqueName))
                    uniqueName = $"{modelName}_{suffix++}";

                string ext = format.ToLowerInvariant() == "obj" ? ".obj" : ".dae";
                string outPath = Path.Combine(outDir, uniqueName + ext);

                try
                {
                    if (format.ToLowerInvariant() == "obj")
                    {
                        OBJ.export(models, outPath, m);
                    }
                    else
                    {
                        DAE.export(models, outPath, m, effectiveAnimIndex);
                    }
                    string animNote = effectiveAnimIndex >= 0 ? $", anim={effectiveAnimIndex}" : "";
                    Console.WriteLine($"  Model: {outPath} ({models.model[m].mesh.Count} meshes, {models.model[m].skeleton.Count} bones{animNote})");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Export error [{modelName}]: {ex.Message}");
                }
            }
        }

        static void ExportTextures(List<RenderBase.OTexture> textures, string outDir)
        {
            Directory.CreateDirectory(outDir);
            foreach (var tex in textures)
            {
                try
                {
                    string texPath = Path.Combine(outDir, tex.name + ".png");
                    tex.texture.Save(texPath, ImageFormat.Png);
                    Console.WriteLine($"  Texture: {texPath} ({tex.texture.Width}x{tex.texture.Height})");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Texture error [{tex.name}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Materializes a container entry's data, handling lazy-loaded GARC entries.
        /// </summary>
        static byte[] GetEntryData(OContainer container, int index)
        {
            var entry = container.content[index];
            if (entry.data != null) return entry.data;

            // Lazy-loaded from stream (GARC)
            if (entry.loadFromDisk && container.data != null)
            {
                container.data.Seek(entry.fileOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[entry.fileLength];
                container.data.Read(buffer, 0, buffer.Length);
                return buffer;
            }

            return Array.Empty<byte>();
        }

        static void FlushModelGroup(
            RenderBase.OModelGroup model,
            string outDir,
            string garcName,
            string modelName,
            int entryIndex,
            string format,
            ref int totalModels,
            ref int totalTextures,
            ref int totalErrors)
        {
            // Sanitize folder name
            string safeName = modelName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(safeName)) safeName = $"entry_{entryIndex}";

            string modelDir = Path.Combine(outDir, garcName, safeName);
            try
            {
                ExportModelGroup(model, modelDir, safeName, format, -1);
                totalModels++;
                totalTextures += model.texture.Count;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Flush error [{safeName}]: {ex.Message}");
                totalErrors++;
            }
        }
    }
}
