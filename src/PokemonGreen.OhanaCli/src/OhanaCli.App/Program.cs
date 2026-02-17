using System.CommandLine;
using System.CommandLine.Invocation;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.Json;

using OhanaCli.Formats;
using OhanaCli.Formats.Containers;
using OhanaCli.Formats.Models.GenericFormats;
using OhanaCli.Formats.Models.PocketMonsters;

var rootCommand = new RootCommand("OhanaCli rewrite command pipeline.");

var infoFileArgument = new Argument<FileInfo>("file", "Input file path.");
var convertFileArgument = new Argument<FileInfo>("file", "Input file path.");
var batchInputArgument = new Argument<DirectoryInfo>("inputDir", "Input directory.");
var diagnoseFileArgument = new Argument<FileInfo>("file", "Input GARC/container file path.");

var outputOption = new Option<DirectoryInfo>(new[] { "-o", "--output" }, "Output directory.")
{
    IsRequired = true
};

var formatOption = new Option<string>(new[] { "-f", "--format" }, () => "dae", "Export format: dae or obj.");
var animationIndexOption = new Option<int?>(new[] { "-a", "--animation-index" }, "Skeletal animation index for DAE export.");
var splitModelAnimationsOption = new Option<bool>("--split-model-anims", "Export one model DAE plus separate skeletal clip DAEs and manifest metadata.");
var limitOption = new Option<int?>(new[] { "-n", "--limit" }, "Maximum container entries to process.");
var diagnosticAnimationOption = new Option<bool>("--diag-anim", "Emit per-bone animation segment diagnostics during export.");
var startOption = new Option<int>("--start", () => 0, "Diagnose start entry index (inclusive).");
var endOption = new Option<int>("--end", () => 10, "Diagnose end entry index (inclusive).");

var infoCommand = new Command("info", "Print file metadata and quick inspection details.");
infoCommand.AddArgument(infoFileArgument);
infoCommand.SetHandler((InvocationContext ctx) =>
{
    FileInfo file = ctx.ParseResult.GetValueForArgument(infoFileArgument);
    ctx.ExitCode = RunFatalSafe(() => InfoHandler(file));
});

var convertCommand = new Command("convert", "Convert one file/container to DAE or OBJ.");
convertCommand.AddArgument(convertFileArgument);
convertCommand.AddOption(outputOption);
convertCommand.AddOption(formatOption);
convertCommand.AddOption(animationIndexOption);
convertCommand.AddOption(splitModelAnimationsOption);
convertCommand.AddOption(limitOption);
convertCommand.AddOption(diagnosticAnimationOption);
convertCommand.SetHandler((InvocationContext ctx) =>
{
    FileInfo file = ctx.ParseResult.GetValueForArgument(convertFileArgument);
    DirectoryInfo? output = ctx.ParseResult.GetValueForOption(outputOption);
    if (output is null)
    {
        Console.Error.WriteLine("missing required option: --output");
        ctx.ExitCode = CliConventions.ExitFatal;
        return;
    }

    string format = ctx.ParseResult.GetValueForOption(formatOption) ?? "dae";
    int? animationIndex = ctx.ParseResult.GetValueForOption(animationIndexOption);
    bool splitModelAnimations = ctx.ParseResult.GetValueForOption(splitModelAnimationsOption);
    int? limit = ctx.ParseResult.GetValueForOption(limitOption);
    bool diagAnim = ctx.ParseResult.GetValueForOption(diagnosticAnimationOption);
    ctx.ExitCode = RunFatalSafe(() => RunWithAnimationDiagnostics(diagAnim, () => ConvertHandler(file, output, format, animationIndex, splitModelAnimations, limit)));
});

var batchCommand = new Command("batch", "Batch-convert files from a directory.");
batchCommand.AddArgument(batchInputArgument);
batchCommand.AddOption(outputOption);
batchCommand.AddOption(formatOption);
batchCommand.AddOption(animationIndexOption);
batchCommand.AddOption(splitModelAnimationsOption);
batchCommand.AddOption(limitOption);
batchCommand.AddOption(diagnosticAnimationOption);
batchCommand.SetHandler((InvocationContext ctx) =>
{
    DirectoryInfo inputDir = ctx.ParseResult.GetValueForArgument(batchInputArgument);
    DirectoryInfo? output = ctx.ParseResult.GetValueForOption(outputOption);
    if (output is null)
    {
        Console.Error.WriteLine("missing required option: --output");
        ctx.ExitCode = CliConventions.ExitFatal;
        return;
    }

    string format = ctx.ParseResult.GetValueForOption(formatOption) ?? "dae";
    int? animationIndex = ctx.ParseResult.GetValueForOption(animationIndexOption);
    bool splitModelAnimations = ctx.ParseResult.GetValueForOption(splitModelAnimationsOption);
    int? limit = ctx.ParseResult.GetValueForOption(limitOption);
    bool diagAnim = ctx.ParseResult.GetValueForOption(diagnosticAnimationOption);
    ctx.ExitCode = RunFatalSafe(() => RunWithAnimationDiagnostics(diagAnim, () => BatchHandler(inputDir, output, format, animationIndex, splitModelAnimations, limit)));
});

var diagnoseCommand = new Command("diagnose", "Inspect container entries and detected content types.");
diagnoseCommand.AddArgument(diagnoseFileArgument);
diagnoseCommand.AddOption(startOption);
diagnoseCommand.AddOption(endOption);
diagnoseCommand.SetHandler((InvocationContext ctx) =>
{
    FileInfo file = ctx.ParseResult.GetValueForArgument(diagnoseFileArgument);
    int start = ctx.ParseResult.GetValueForOption(startOption);
    int end = ctx.ParseResult.GetValueForOption(endOption);
    ctx.ExitCode = RunFatalSafe(() => DiagnoseHandler(file, start, end));
});

rootCommand.AddCommand(infoCommand);
rootCommand.AddCommand(convertCommand);
rootCommand.AddCommand(batchCommand);
rootCommand.AddCommand(diagnoseCommand);

return await rootCommand.InvokeAsync(args);

static int RunFatalSafe(Func<int> action)
{
    try
    {
        return action();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"fatal: {ex.Message}");
        return CliConventions.ExitFatal;
    }
}

static int RunWithAnimationDiagnostics(bool enabled, Func<int> action)
{
    bool previousDae = DAE.DiagnosticLogging;
    bool previousGfModel = GfModel.DiagnosticLogging;

    DAE.DiagnosticLogging = enabled;
    GfModel.DiagnosticLogging = enabled;

    try
    {
        return action();
    }
    finally
    {
        DAE.DiagnosticLogging = previousDae;
        GfModel.DiagnosticLogging = previousGfModel;
    }
}

static int InfoHandler(FileInfo file)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"missing file: {file.FullName}");
        return CliConventions.ExitFatal;
    }

    FileIO.LoadedFile loaded = FileIO.load(file.FullName);
    Console.WriteLine($"file={file.FullName}");
    Console.WriteLine($"detectedType={loaded.type}");

    if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup modelGroup)
    {
        WriteModelGroupSummary(modelGroup);
        return 0;
    }

    if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
    {
        try
        {
            Console.WriteLine($"entries={container.content.Count}");
            int inspected = 0;
            int modelEntries = 0;
            int imageEntries = 0;
            int textureEntries = 0;
            int animEntries = 0;

            for (int i = 0; i < container.content.Count; i++)
            {
                byte[] entryData = ReadEntryData(container, container.content[i]);
                if (entryData.Length == 0)
                {
                    continue;
                }

                FileIO.LoadedFile entryLoaded = FileIO.load(new MemoryStream(entryData));
                inspected++;
                if (entryLoaded.type == FileIO.formatType.model) modelEntries++;
                if (entryLoaded.type == FileIO.formatType.image) imageEntries++;
                if (entryLoaded.type == FileIO.formatType.texture) textureEntries++;
                if (entryLoaded.type == FileIO.formatType.anims) animEntries++;
            }

            Console.WriteLine($"inspected={inspected} modelEntries={modelEntries} imageEntries={imageEntries} textureEntries={textureEntries} animEntries={animEntries}");
        }
        finally
        {
            container.data?.Dispose();
        }

        return 0;
    }

    if (loaded.type == FileIO.formatType.image && loaded.data is RenderBase.OTexture texture)
    {
        Console.WriteLine($"texture=1 name={texture.name}");
        return 0;
    }

    if (loaded.type == FileIO.formatType.texture && loaded.data is List<RenderBase.OTexture> textures)
    {
        Console.WriteLine($"textureListCount={textures.Count}");
        return 0;
    }

    if (loaded.type == FileIO.formatType.anims)
    {
        Console.WriteLine("animationData=1");
        return 0;
    }

    Console.Error.WriteLine("unsupported input file for info");
    return CliConventions.ExitFatal;
}

static int ConvertHandler(FileInfo file, DirectoryInfo outputDir, string format, int? animationIndex, bool splitModelAnimations, int? limit)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"missing file: {file.FullName}");
        return CliConventions.ExitFatal;
    }

    if (!TryNormalizeFormat(format, out string normalizedFormat))
    {
        Console.Error.WriteLine($"unsupported format: {format}. expected: dae or obj");
        return CliConventions.ExitFatal;
    }

    if (limit.HasValue && limit.Value <= 0)
    {
        Console.Error.WriteLine("limit must be > 0 when provided");
        return CliConventions.ExitFatal;
    }

    if (splitModelAnimations)
    {
        if (normalizedFormat != "dae")
        {
            Console.Error.WriteLine("--split-model-anims supports DAE format only");
            return CliConventions.ExitFatal;
        }

        if (animationIndex.HasValue)
        {
            Console.Error.WriteLine("--split-model-anims cannot be combined with --animation-index");
            return CliConventions.ExitFatal;
        }

    }

    Directory.CreateDirectory(outputDir.FullName);

    FileIO.LoadedFile loaded = FileIO.load(file.FullName);
    string baseName = Path.GetFileNameWithoutExtension(file.Name);

    if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup modelGroup)
    {
        string folderPath = Path.Combine(outputDir.FullName, SanitizeName(baseName));
        ExportStats stats = ExportModelGroup(modelGroup, folderPath, normalizedFormat, animationIndex, splitModelAnimations);
        Console.WriteLine($"convert summary: groupsTotal=1 groupsSucceeded=1 groupsFailed=0 models={stats.Models} textures={stats.Textures} clipsFound={stats.ClipsFound} clipsExported={stats.ClipsExported} clipsSkipped={stats.ClipsSkipped} out={folderPath}");
        if (stats.Models > 0)
        {
            return CliConventions.ExitSuccess;
        }

        return stats.Textures > 0 ? CliConventions.ExitPartial : CliConventions.ExitFatal;
    }

    if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
    {
        try
        {
            GroupingOutcome grouping = GroupContainerEntries(container, limit);
            List<GroupedEntry> groups = grouping.Groups;
            if (groups.Count == 0)
            {
                Console.Error.WriteLine("no exportable model groups found in container");
                return CliConventions.ExitFatal;
            }

            int groupsSucceeded = 0;
            int groupsFailed = 0;
            int modelTotal = 0;
            int textureTotal = 0;
            int clipsFoundTotal = 0;
            int clipsExportedTotal = 0;
            int clipsSkippedTotal = 0;
            string containerRoot = Path.Combine(outputDir.FullName, SanitizeName(baseName));
            Directory.CreateDirectory(containerRoot);

            for (int i = 0; i < groups.Count; i++)
            {
                GroupedEntry group = groups[i];
                string folderName = DeriveGroupFolderName(group.ModelGroup, i, group.StartEntry);
                string folderPath = Path.Combine(containerRoot, folderName);
                try
                {
                    ExportStats stats = ExportModelGroup(group.ModelGroup, folderPath, normalizedFormat, animationIndex, splitModelAnimations);
                    modelTotal += stats.Models;
                    textureTotal += stats.Textures;
                    clipsFoundTotal += stats.ClipsFound;
                    clipsExportedTotal += stats.ClipsExported;
                    clipsSkippedTotal += stats.ClipsSkipped;
                    groupsSucceeded++;
                }
                catch (Exception ex)
                {
                    groupsFailed++;
                    Console.Error.WriteLine($"convert group-failure: startEntry={group.StartEntry} endEntry={group.EndEntry} reason={SanitizeNote(ex.Message)}");
                }
            }

            Console.WriteLine($"convert summary: groupsTotal={groups.Count} groupsSucceeded={groupsSucceeded} groupsFailed={groupsFailed} models={modelTotal} textures={textureTotal} clipsFound={clipsFoundTotal} clipsExported={clipsExportedTotal} clipsSkipped={clipsSkippedTotal} out={containerRoot}");

            if (grouping.LimitExcludedAnimations)
            {
                Console.Error.WriteLine($"warning: no skeletal animations were included because --limit={limit} ended before animation entries. Increase or remove --limit and rerun to export all clips.");
            }

            if (groupsSucceeded == 0)
            {
                return CliConventions.ExitFatal;
            }

            if (groupsFailed > 0 || modelTotal == 0)
            {
                return CliConventions.ExitPartial;
            }

            return CliConventions.ExitSuccess;
        }
        finally
        {
            container.data?.Dispose();
        }
    }

    Console.Error.WriteLine($"unsupported input type for convert: {loaded.type}");
    return CliConventions.ExitFatal;
}

static int BatchHandler(DirectoryInfo inputDir, DirectoryInfo outputDir, string format, int? animationIndex, bool splitModelAnimations, int? limit)
{
    if (!inputDir.Exists)
    {
        Console.Error.WriteLine($"missing directory: {inputDir.FullName}");
        return CliConventions.ExitFatal;
    }

    if (!TryNormalizeFormat(format, out _))
    {
        Console.Error.WriteLine($"unsupported format: {format}. expected: dae or obj");
        return CliConventions.ExitFatal;
    }

    Directory.CreateDirectory(outputDir.FullName);

    string[] files = Directory.GetFiles(inputDir.FullName, "*", SearchOption.AllDirectories);
    if (files.Length == 0)
    {
        Console.Error.WriteLine("no files found in input directory");
        return CliConventions.ExitFatal;
    }

    int succeeded = 0;
    int partial = 0;
    int fatal = 0;

    for (int i = 0; i < files.Length; i++)
    {
        string filePath = files[i];
        int result;

        try
        {
            result = ConvertHandler(new FileInfo(filePath), outputDir, format, animationIndex, splitModelAnimations, limit);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"batch error: file={filePath} reason={ex.Message}");
            result = CliConventions.ExitFatal;
        }

        if (result == CliConventions.ExitSuccess)
        {
            succeeded++;
        }
        else if (result == CliConventions.ExitPartial)
        {
            partial++;
        }
        else
        {
            fatal++;
        }
    }

    int failed = partial + fatal;
    Console.WriteLine($"batch summary: totalFiles={files.Length} succeeded={succeeded} partial={partial} fatal={fatal} failedTotal={failed}");
    return CliConventions.AggregateExitCode(succeeded, partial, fatal);
}

static int DiagnoseHandler(FileInfo file, int start, int end)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"missing file: {file.FullName}");
        return CliConventions.ExitFatal;
    }

    if (start < 0)
    {
        Console.Error.WriteLine("start must be >= 0");
        return CliConventions.ExitFatal;
    }

    if (end < start)
    {
        Console.Error.WriteLine("end must be >= start");
        return CliConventions.ExitFatal;
    }

    FileIO.LoadedFile loaded = FileIO.load(file.FullName);
    if (loaded.type != FileIO.formatType.container || loaded.data is not OContainer container)
    {
        Console.Error.WriteLine("diagnose currently supports container inputs only");
        return CliConventions.ExitFatal;
    }

    try
    {
        if (container.content.Count == 0)
        {
            Console.Error.WriteLine("container has no entries");
            return CliConventions.ExitFatal;
        }

        int maxIndex = container.content.Count - 1;
        int startIndex = Math.Min(start, maxIndex);
        int endIndex = Math.Min(end, maxIndex);

        Console.WriteLine($"diagnose file={file.FullName}");
        Console.WriteLine($"entries={container.content.Count} range={startIndex}-{endIndex}");

        int modelEntries = 0;
        int imageEntries = 0;
        int textureEntries = 0;
        int containerEntries = 0;
        int animationEntries = 0;
        int errorEntries = 0;
        int totalModels = 0;
        int totalMeshes = 0;
        int totalTextures = 0;

        for (int i = startIndex; i <= endIndex; i++)
        {
            OContainer.fileEntry entry = container.content[i];
            byte[] entryData = ReadEntryData(container, entry);

            if (entryData.Length == 0)
            {
                Console.WriteLine($"[{i}] name={entry.name} type=empty models=0 meshes=0 textures=0 segmentSummary=- notes=empty");
                continue;
            }

            FileIO.LoadedFile entryLoaded;
            try
            {
                entryLoaded = FileIO.load(new MemoryStream(entryData));
            }
            catch (Exception ex)
            {
                errorEntries++;
                Console.WriteLine($"[{i}] name={entry.name} type=error models=0 meshes=0 textures=0 segmentSummary=- notes={SanitizeNote(ex.Message)}");
                continue;
            }

            EntryDiagnostics diagnostics = BuildEntryDiagnostics(entryLoaded);
            if (entryLoaded.type == FileIO.formatType.model) modelEntries++;
            if (entryLoaded.type == FileIO.formatType.image) imageEntries++;
            if (entryLoaded.type == FileIO.formatType.texture) textureEntries++;
            if (entryLoaded.type == FileIO.formatType.container) containerEntries++;
            if (entryLoaded.type == FileIO.formatType.anims) animationEntries++;

            totalModels += diagnostics.ModelCount;
            totalMeshes += diagnostics.MeshCount;
            totalTextures += diagnostics.TextureCount;

            Console.WriteLine(
                $"[{i}] name={entry.name} type={entryLoaded.type} models={diagnostics.ModelCount} meshes={diagnostics.MeshCount} textures={diagnostics.TextureCount} segmentSummary={diagnostics.SegmentSummary} notes={diagnostics.Note}");
        }

        Console.WriteLine($"diagnose summary: inspected={endIndex - startIndex + 1} modelEntries={modelEntries} imageEntries={imageEntries} textureEntries={textureEntries} containerEntries={containerEntries} animationEntries={animationEntries} errorEntries={errorEntries} totalModels={totalModels} totalMeshes={totalMeshes} totalTextures={totalTextures}");
    }
    finally
    {
        container.data?.Dispose();
    }

    return CliConventions.ExitSuccess;
}

static GroupingOutcome GroupContainerEntries(OContainer container, int? limit)
{
    int entryCount = container.content.Count;
    int max = limit.HasValue ? Math.Min(entryCount, limit.Value) : entryCount;
    List<GroupedEntry> groups = new List<GroupedEntry>();

    GroupedEntry? current = null;
    RenderBase.OModelGroup pendingPrefix = new RenderBase.OModelGroup();

    for (int i = 0; i < max; i++)
    {
        OContainer.fileEntry entry = container.content[i];
        byte[] entryData = ReadEntryData(container, entry);
        if (entryData.Length == 0)
        {
            continue;
        }

        FileIO.LoadedFile loaded;
        try
        {
            loaded = FileIO.load(new MemoryStream(entryData));
        }
        catch
        {
            continue;
        }

        RenderBase.OModelGroup part = BuildModelGroupFromLoaded(loaded, 0);
        if (part.model.Count == 0 && part.texture.Count == 0 && part.skeletalAnimation.list.Count == 0)
        {
            continue;
        }

        int meshCount = CountMeshes(part);
        bool hasModels = part.model.Count > 0;
        bool hasMeshes = meshCount > 0;

        if (hasModels && hasMeshes)
        {
            if (current is not null)
            {
                DeduplicateTextures(current.ModelGroup);
                groups.Add(current);
            }

            if (pendingPrefix.model.Count > 0 || pendingPrefix.texture.Count > 0 || pendingPrefix.skeletalAnimation.list.Count > 0)
            {
                part.merge(pendingPrefix);
                pendingPrefix = new RenderBase.OModelGroup();
            }

            current = new GroupedEntry
            {
                StartEntry = i,
                EndEntry = i,
                ModelGroup = part
            };
            continue;
        }

        if (current is not null)
        {
            current.ModelGroup.merge(part);
            current.EndEntry = i;
        }
        else
        {
            pendingPrefix.merge(part);
        }
    }

    if (current is not null && limit.HasValue && max < entryCount)
    {
        ExtendTrailingGroupForTextures(container, current, max, entryCount);
    }

    if (current is not null)
    {
        DeduplicateTextures(current.ModelGroup);
        groups.Add(current);
    }

    bool limitExcludedAnimations = false;
    if (limit.HasValue && max < entryCount)
    {
        int groupedClipCount = groups.Sum(static g => g.ModelGroup.skeletalAnimation.list.OfType<RenderBase.OSkeletalAnimation>().Count());
        if (groupedClipCount == 0)
        {
            limitExcludedAnimations = DetectAnimationBeyondLimit(container, max);
        }
    }

    return new GroupingOutcome
    {
        Groups = groups,
        LimitExcludedAnimations = limitExcludedAnimations
    };
}

static void ExtendTrailingGroupForTextures(OContainer container, GroupedEntry current, int startIndex, int entryCount)
{
    if (HasAllReferencedTextures(current.ModelGroup))
    {
        return;
    }

    for (int i = startIndex; i < entryCount; i++)
    {
        OContainer.fileEntry entry = container.content[i];
        byte[] entryData = ReadEntryData(container, entry);
        if (entryData.Length == 0)
        {
            continue;
        }

        FileIO.LoadedFile loaded;
        try
        {
            loaded = FileIO.load(new MemoryStream(entryData));
        }
        catch
        {
            continue;
        }

        RenderBase.OModelGroup part = BuildModelGroupFromLoaded(loaded, 0);
        if (part.model.Count == 0 && part.texture.Count == 0 && part.skeletalAnimation.list.Count == 0)
        {
            continue;
        }

        int meshCount = CountMeshes(part);
        bool hasModels = part.model.Count > 0;
        bool hasMeshes = meshCount > 0;
        if (hasModels && hasMeshes)
        {
            break;
        }

        current.ModelGroup.merge(part);
        current.EndEntry = i;

        if (HasAllReferencedTextures(current.ModelGroup))
        {
            break;
        }
    }
}

static bool HasAllReferencedTextures(RenderBase.OModelGroup modelGroup)
{
    HashSet<string> required = GetRequiredTextureNames(modelGroup);
    if (required.Count == 0)
    {
        return true;
    }

    HashSet<string> available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < modelGroup.texture.Count; i++)
    {
        RenderBase.OTexture tex = modelGroup.texture[i];
        if (tex == null || string.IsNullOrWhiteSpace(tex.name))
        {
            continue;
        }

        available.Add(tex.name);
    }

    return required.IsSubsetOf(available);
}

static HashSet<string> GetRequiredTextureNames(RenderBase.OModelGroup modelGroup)
{
    HashSet<string> required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (int modelIndex = 0; modelIndex < modelGroup.model.Count; modelIndex++)
    {
        RenderBase.OModel mdl = modelGroup.model[modelIndex];
        for (int matIndex = 0; matIndex < mdl.material.Count; matIndex++)
        {
            RenderBase.OMaterial mat = mdl.material[matIndex];

            if (!string.IsNullOrWhiteSpace(mat.name0)) required.Add(mat.name0);
            if (!string.IsNullOrWhiteSpace(mat.name1)) required.Add(mat.name1);
            if (!string.IsNullOrWhiteSpace(mat.name2)) required.Add(mat.name2);
        }
    }

    return required;
}

static bool DetectAnimationBeyondLimit(OContainer container, int startIndex)
{
    for (int i = startIndex; i < container.content.Count; i++)
    {
        OContainer.fileEntry entry = container.content[i];
        byte[] entryData = ReadEntryData(container, entry);
        if (entryData.Length == 0)
        {
            continue;
        }

        FileIO.LoadedFile loaded;
        try
        {
            loaded = FileIO.load(new MemoryStream(entryData));
        }
        catch
        {
            continue;
        }

        if (loaded.type == FileIO.formatType.anims)
        {
            return true;
        }

        RenderBase.OModelGroup group = BuildModelGroupFromLoaded(loaded, 0);
        if (group.skeletalAnimation.list.OfType<RenderBase.OSkeletalAnimation>().Any())
        {
            return true;
        }
    }

    return false;
}

static RenderBase.OModelGroup BuildModelGroupFromLoaded(FileIO.LoadedFile loaded, int depth)
{
    const int maxDepth = 3;
    RenderBase.OModelGroup output = new RenderBase.OModelGroup();

    if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup modelGroup)
    {
        output.merge(modelGroup);
        return output;
    }

    if (loaded.type == FileIO.formatType.image && loaded.data is RenderBase.OTexture texture)
    {
        output.texture.Add(texture);
        return output;
    }

    if (loaded.type == FileIO.formatType.texture && loaded.data is List<RenderBase.OTexture> textures)
    {
        output.texture.AddRange(textures);
        return output;
    }

    if (loaded.type == FileIO.formatType.anims && loaded.data is RenderBase.OSkeletalAnimation animation)
    {
        output.skeletalAnimation.list.Add(animation);
        return output;
    }

    if (loaded.type == FileIO.formatType.anims && loaded.data is List<RenderBase.OSkeletalAnimation> animations)
    {
        for (int i = 0; i < animations.Count; i++)
        {
            output.skeletalAnimation.list.Add(animations[i]);
        }

        return output;
    }

    if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container && depth < maxDepth)
    {
        try
        {
            for (int i = 0; i < container.content.Count; i++)
            {
                byte[] nestedData = ReadEntryData(container, container.content[i]);
                if (nestedData.Length == 0)
                {
                    continue;
                }

                FileIO.LoadedFile nestedLoaded;
                try
                {
                    nestedLoaded = FileIO.load(new MemoryStream(nestedData));
                }
                catch
                {
                    continue;
                }

                RenderBase.OModelGroup nestedGroup = BuildModelGroupFromLoaded(nestedLoaded, depth + 1);
                output.merge(nestedGroup);
            }
        }
        finally
        {
            container.data?.Dispose();
        }
    }

    return output;
}

static ExportStats ExportModelGroup(RenderBase.OModelGroup modelGroup, string outputDir, string format, int? animationIndex, bool splitModelAnimations)
{
    Directory.CreateDirectory(outputDir);
    DeduplicateTextures(modelGroup);

    WriteTexturesResult textureWrite = WriteTextures(modelGroup, outputDir);
    int writtenTextures = textureWrite.Count;
    int writtenModels = 0;
    List<RenderBase.OSkeletalAnimation> skeletalClips = modelGroup.skeletalAnimation.list.OfType<RenderBase.OSkeletalAnimation>().ToList();
    int clipsFound = skeletalClips.Count;
    int clipsExported = 0;
    int clipsSkipped = 0;

    if (splitModelAnimations && format == "dae")
    {
        return ExportSplitModelAnimations(modelGroup, outputDir, textureWrite.FileNames, skeletalClips);
    }

    List<int> clipIndicesToExport = new List<int>();
    if (format == "dae")
    {
        if (animationIndex.HasValue)
        {
            if (animationIndex.Value >= 0 && animationIndex.Value < clipsFound)
            {
                clipIndicesToExport.Add(animationIndex.Value);
                clipsExported = 1;
                clipsSkipped = clipsFound - 1;
            }
            else
            {
                clipsSkipped = clipsFound;
                Console.Error.WriteLine($"warning: animation index {animationIndex.Value} is out of range (clipsFound={clipsFound}); exporting static DAE without animation.");
            }
        }
        else
        {
            for (int i = 0; i < clipsFound; i++)
            {
                clipIndicesToExport.Add(i);
            }

            clipsExported = clipsFound;
        }
    }
    else
    {
        clipsSkipped = clipsFound;
    }

    HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < modelGroup.model.Count; i++)
    {
        RenderBase.OModel model = modelGroup.model[i];
        if (model.mesh.Count == 0)
        {
            continue;
        }

        string baseName = string.IsNullOrWhiteSpace(model.name) ? $"model_{i}" : SanitizeName(model.name);
        string uniqueName = EnsureUniqueName(baseName, usedNames);

        if (format == "dae")
        {
            if (animationIndex.HasValue)
            {
                string modelPath = Path.Combine(outputDir, $"{uniqueName}.{format}");
                int selectedAnimation = clipIndicesToExport.Count > 0 ? clipIndicesToExport[0] : -1;
                DAE.export(modelGroup, modelPath, i, selectedAnimation);
                writtenModels++;
            }
            else if (clipIndicesToExport.Count > 0)
            {
                for (int clipIdx = 0; clipIdx < clipIndicesToExport.Count; clipIdx++)
                {
                    int selectedAnimation = clipIndicesToExport[clipIdx];
                    string modelPath = Path.Combine(outputDir, $"{uniqueName}.anim_{selectedAnimation:D3}.{format}");
                    DAE.export(modelGroup, modelPath, i, selectedAnimation);
                    writtenModels++;
                }
            }
            else
            {
                string modelPath = Path.Combine(outputDir, $"{uniqueName}.{format}");
                DAE.export(modelGroup, modelPath, i, -1);
                writtenModels++;
            }
        }
        else
        {
            string modelPath = Path.Combine(outputDir, $"{uniqueName}.{format}");
            OBJ.export(modelGroup, modelPath, i);
            writtenModels++;
        }
    }

    return new ExportStats(writtenModels, writtenTextures, clipsFound, clipsExported, clipsSkipped);
}

static WriteTexturesResult WriteTextures(RenderBase.OModelGroup modelGroup, string outputDir)
{
    int count = 0;
    List<string> fileNames = new List<string>();
    HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < modelGroup.texture.Count; i++)
    {
        RenderBase.OTexture texture = modelGroup.texture[i];
        if (texture == null || texture.texture == null)
        {
            continue;
        }

        string baseName = string.IsNullOrWhiteSpace(texture.name) ? $"texture_{i}" : SanitizeName(texture.name);
        string uniqueName = EnsureUniqueName(baseName, usedNames);
        string path = Path.Combine(outputDir, uniqueName + ".png");

        texture.texture.Save(path, ImageFormat.Png);
        fileNames.Add(Path.GetFileName(path));
        count++;
    }

    return new WriteTexturesResult(count, fileNames);
}

static ExportStats ExportSplitModelAnimations(
    RenderBase.OModelGroup modelGroup,
    string outputDir,
    List<string> textureFileNames,
    List<RenderBase.OSkeletalAnimation> skeletalClips)
{
    int writtenModels = 0;
    int clipsExported = 0;
    AnimAssetType assetType = DetectAssetType(modelGroup);

    HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    List<SplitModelManifestEntry> modelEntries = new List<SplitModelManifestEntry>();

    for (int i = 0; i < modelGroup.model.Count; i++)
    {
        RenderBase.OModel model = modelGroup.model[i];
        if (model.mesh.Count == 0)
        {
            continue;
        }

        string baseName = string.IsNullOrWhiteSpace(model.name) ? $"model_{i}" : SanitizeName(model.name);
        string uniqueName = EnsureUniqueName(baseName, usedNames);

        string modelPath = Path.Combine(outputDir, $"{uniqueName}.dae");
        DAE.export(modelGroup, modelPath, i, -1);
        writtenModels++;

        string modelClipDir = Path.Combine(outputDir, "clips", uniqueName);
        Directory.CreateDirectory(modelClipDir);

        List<SplitClipManifestEntry> clipEntries = new List<SplitClipManifestEntry>();
        for (int clipIndex = 0; clipIndex < skeletalClips.Count; clipIndex++)
        {
            string clipFileName = $"clip_{clipIndex:D3}.dae";
            string clipPath = Path.Combine(modelClipDir, clipFileName);
            DAE.exportSkeletalClip(modelGroup, clipPath, i, clipIndex);

            RenderBase.OSkeletalAnimation clip = skeletalClips[clipIndex];
            string clipId = $"clip_{clipIndex:D3}";
            string sourceName = string.IsNullOrWhiteSpace(clip.name) ? clipId : clip.name;
            (string? semanticName, string? semanticSource) = ResolveSemanticMetadata(sourceName, clipIndex, assetType);
            clipEntries.Add(new SplitClipManifestEntry
            {
                Index = clipIndex,
                Id = clipId,
                Name = clipId,
                SourceName = sourceName,
                SemanticName = semanticName,
                SemanticSource = semanticSource,
                File = ToRelativePath(outputDir, clipPath),
                FrameCount = clip.frameSize,
                Fps = 30f
            });
            clipsExported++;
        }

        modelEntries.Add(new SplitModelManifestEntry
        {
            Name = uniqueName,
            ModelFile = Path.GetFileName(modelPath),
            Clips = clipEntries
        });
    }

    WriteSplitManifest(outputDir, textureFileNames, modelEntries);
    return new ExportStats(writtenModels, textureFileNames.Count, skeletalClips.Count, clipsExported, 0);
}

static void WriteSplitManifest(string outputDir, List<string> textureFileNames, List<SplitModelManifestEntry> modelEntries)
{
    SplitExportManifest manifest = new SplitExportManifest
    {
        Version = 1,
        Mode = "split-model-anims",
        Textures = textureFileNames,
        Models = modelEntries
    };

    JsonSerializerOptions options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    string manifestPath = Path.Combine(outputDir, "manifest.json");
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));
}

static string ToRelativePath(string rootDir, string fullPath)
{
    string relative = Path.GetRelativePath(rootDir, fullPath);
    return relative.Replace('\\', '/');
}

static AnimAssetType DetectAssetType(RenderBase.OModelGroup modelGroup)
{
    // Check texture names for "pm" (Pokemon) or "tr"/"_fi" (overworld character)
    foreach (RenderBase.OTexture tex in modelGroup.texture)
    {
        if (tex?.name == null) continue;
        if (tex.name.StartsWith("pm", StringComparison.OrdinalIgnoreCase)) return AnimAssetType.Pokemon;
        if (tex.name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return AnimAssetType.Overworld;
        if (tex.name.Contains("_fi", StringComparison.OrdinalIgnoreCase)) return AnimAssetType.Overworld;
    }
    // Check model names
    foreach (RenderBase.OModel mdl in modelGroup.model)
    {
        if (mdl?.name == null) continue;
        if (mdl.name.StartsWith("pm", StringComparison.OrdinalIgnoreCase)) return AnimAssetType.Pokemon;
        if (mdl.name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return AnimAssetType.Overworld;
    }
    return AnimAssetType.Unknown;
}

static (string? Name, string? Source) ResolveSemanticMetadata(string sourceName, int clipIndex, AnimAssetType assetType)
{
    // Try descriptive name match first (rare — Sun/Moon uses numeric names)
    if (!string.IsNullOrWhiteSpace(sourceName))
    {
        if (sourceName.Contains("idle", StringComparison.OrdinalIgnoreCase)) return ("Idle", "source-name");
        if (sourceName.Contains("walk", StringComparison.OrdinalIgnoreCase)) return ("Walk", "source-name");
        if (sourceName.Contains("run", StringComparison.OrdinalIgnoreCase)) return ("Run", "source-name");
        if (sourceName.Contains("jump", StringComparison.OrdinalIgnoreCase)) return ("Jump", "source-name");
    }

    // Parse original animation number from source name (anim_4 → 4, Motion_17 → 17).
    // OhanaCli Pokemon exports name ALL clips "anim_0", so fall back to clipIndex.
    int sourceIndex = ParseSourceAnimIndex(sourceName, clipIndex);

    string? mapped = assetType switch
    {
        AnimAssetType.Overworld => MapOverworldSlot(sourceIndex),
        AnimAssetType.Pokemon => MapPokemonSlot(sourceIndex),
        _ => sourceIndex switch { 0 => "Idle", _ => null }
    };

    return (mapped, mapped is null ? null : "slot-map-v1");
}

// Sun/Moon overworld character animation slots (GARC a/2/0/0).
// Slot numbers are sparse — not all characters have all slots.
// See SPICA-README.md "Overworld Animation Slots" for full documentation.
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

// Sun/Moon Pokemon battle animation slots (GARC a/0/9/4).
// Sequential index — OhanaCli names all clips "anim_0", so clipIndex is used.
// Slot purposes are tentative — update as identified.
static string? MapPokemonSlot(int slot) => slot switch
{
    0 => "Idle",
    _ => null
};

static int ParseSourceAnimIndex(string sourceName, int fallback)
{
    // Parses "anim_4" → 4, "Motion_17" → 17, "clip_003" → 3
    if (string.IsNullOrWhiteSpace(sourceName)) return fallback;
    int lastUnderscore = sourceName.LastIndexOf('_');
    if (lastUnderscore >= 0 && lastUnderscore < sourceName.Length - 1)
    {
        if (int.TryParse(sourceName.AsSpan(lastUnderscore + 1), out int parsed))
            return parsed;
    }
    return fallback;
}

static void DeduplicateTextures(RenderBase.OModelGroup modelGroup)
{
    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    List<RenderBase.OTexture> deduped = new List<RenderBase.OTexture>();

    for (int i = 0; i < modelGroup.texture.Count; i++)
    {
        RenderBase.OTexture texture = modelGroup.texture[i];
        if (texture == null)
        {
            continue;
        }

        string key = string.IsNullOrWhiteSpace(texture.name) ? $"__null_{i}" : texture.name;
        if (seen.Add(key))
        {
            deduped.Add(texture);
        }
    }

    modelGroup.texture = deduped;
}

static byte[] ReadEntryData(OContainer container, OContainer.fileEntry entry)
{
    if (!entry.loadFromDisk)
    {
        return entry.data ?? Array.Empty<byte>();
    }

    if (container.data == null)
    {
        return Array.Empty<byte>();
    }

    if (!container.data.CanSeek || !container.data.CanRead)
    {
        return Array.Empty<byte>();
    }

    if (entry.fileLength == 0)
    {
        return Array.Empty<byte>();
    }

    if (entry.fileLength > int.MaxValue)
    {
        throw new InvalidDataException("entry too large to read into memory");
    }

    byte[] buffer = new byte[(int)entry.fileLength];
    long originalPosition = container.data.Position;

    try
    {
        container.data.Seek(entry.fileOffset, SeekOrigin.Begin);

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = container.data.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead == buffer.Length)
        {
            return buffer;
        }

        byte[] resized = new byte[totalRead];
        Buffer.BlockCopy(buffer, 0, resized, 0, totalRead);
        return resized;
    }
    finally
    {
        container.data.Seek(originalPosition, SeekOrigin.Begin);
    }
}

static EntryDiagnostics BuildEntryDiagnostics(FileIO.LoadedFile loaded)
{
    EntryDiagnostics diagnostics = new EntryDiagnostics
    {
        SegmentSummary = "-",
        Note = "-"
    };

    if (loaded.type == FileIO.formatType.model && loaded.data is RenderBase.OModelGroup modelGroup)
    {
        diagnostics.ModelCount = modelGroup.model.Count;
        diagnostics.MeshCount = CountMeshes(modelGroup);
        diagnostics.TextureCount = modelGroup.texture.Count;

        List<RenderBase.OSkeletalAnimation> skeletalAnimations = modelGroup.skeletalAnimation.list
            .OfType<RenderBase.OSkeletalAnimation>()
            .ToList();

        if (skeletalAnimations.Count > 0)
        {
            int eulerBones = 0;
            int axisAngleBones = 0;
            int frameFormatBones = 0;
            int fullBakedBones = 0;

            foreach (RenderBase.OSkeletalAnimation animation in skeletalAnimations)
            {
                foreach (RenderBase.OSkeletalAnimationBone bone in animation.bone)
                {
                    if (!bone.isFrameFormat && !bone.isFullBakedFormat) eulerBones++;
                    if (bone.isAxisAngle) axisAngleBones++;
                    if (bone.isFrameFormat) frameFormatBones++;
                    if (bone.isFullBakedFormat) fullBakedBones++;
                }
            }

            diagnostics.SegmentSummary = $"euler={eulerBones},quaternion={frameFormatBones},matrix={fullBakedBones},axisAngle={axisAngleBones}";
            diagnostics.Note = $"skelAnims={skeletalAnimations.Count};axisAngleBones={axisAngleBones};frameBones={frameFormatBones};bakedBones={fullBakedBones}";
        }

        return diagnostics;
    }

    if (loaded.type == FileIO.formatType.image && loaded.data is RenderBase.OTexture)
    {
        diagnostics.TextureCount = 1;
        return diagnostics;
    }

    if (loaded.type == FileIO.formatType.texture && loaded.data is List<RenderBase.OTexture> textures)
    {
        diagnostics.TextureCount = textures.Count;
        return diagnostics;
    }

    if (loaded.type == FileIO.formatType.container && loaded.data is OContainer container)
    {
        diagnostics.Note = $"nestedEntries={container.content.Count}";
        return diagnostics;
    }

    if (loaded.type == FileIO.formatType.anims && loaded.data is RenderBase.OSkeletalAnimation skeletalAnimation)
    {
        int eulerBones = 0;
        int axisAngleBones = 0;
        int frameFormatBones = 0;
        int fullBakedBones = 0;
        foreach (RenderBase.OSkeletalAnimationBone bone in skeletalAnimation.bone)
        {
            if (!bone.isFrameFormat && !bone.isFullBakedFormat) eulerBones++;
            if (bone.isAxisAngle) axisAngleBones++;
            if (bone.isFrameFormat) frameFormatBones++;
            if (bone.isFullBakedFormat) fullBakedBones++;
        }

        diagnostics.SegmentSummary = $"euler={eulerBones},quaternion={frameFormatBones},matrix={fullBakedBones},axisAngle={axisAngleBones}";
        diagnostics.Note = "animationPayload";
    }

    return diagnostics;
}

static int CountMeshes(RenderBase.OModelGroup modelGroup)
{
    int meshCount = 0;
    for (int i = 0; i < modelGroup.model.Count; i++)
    {
        meshCount += modelGroup.model[i].mesh.Count;
    }

    return meshCount;
}

static void WriteModelGroupSummary(RenderBase.OModelGroup modelGroup)
{
    int modelCount = modelGroup.model.Count;
    int meshCount = CountMeshes(modelGroup);
    int textureCount = modelGroup.texture.Count;
    int boneCount = 0;

    for (int i = 0; i < modelGroup.model.Count; i++)
    {
        boneCount += modelGroup.model[i].skeleton.Count;
    }

    int skeletalAnimationCount = modelGroup.skeletalAnimation.list.OfType<RenderBase.OSkeletalAnimation>().Count();
    Console.WriteLine($"models={modelCount} meshes={meshCount} textures={textureCount} bones={boneCount} skeletalAnimations={skeletalAnimationCount}");
}

static string DeriveGroupFolderName(RenderBase.OModelGroup modelGroup, int groupIndex, int startEntry)
{
    string modelName = modelGroup.model.FirstOrDefault(m => m.mesh.Count > 0)?.name ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(modelName))
    {
        return $"{groupIndex:D4}_{SanitizeName(modelName)}";
    }

    string textureName = modelGroup.texture.FirstOrDefault()?.name ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(textureName))
    {
        return $"{groupIndex:D4}_{SanitizeName(textureName)}";
    }

    return $"{groupIndex:D4}_entry_{startEntry:D5}";
}

static string EnsureUniqueName(string baseName, HashSet<string> usedNames)
{
    string candidate = baseName;
    int suffix = 1;

    while (!usedNames.Add(candidate))
    {
        candidate = $"{baseName}_{suffix}";
        suffix++;
    }

    return candidate;
}

static string SanitizeName(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "unnamed";
    }

    char[] invalid = Path.GetInvalidFileNameChars();
    string sanitized = value;

    for (int i = 0; i < invalid.Length; i++)
    {
        sanitized = sanitized.Replace(invalid[i], '_');
    }

    sanitized = sanitized.Trim();
    return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
}

static bool TryNormalizeFormat(string format, out string normalized)
{
    return CliConventions.TryNormalizeFormat(format, out normalized);
}

static string SanitizeNote(string message)
{
    return message.Replace('\r', ' ').Replace('\n', ' ').Trim();
}

readonly record struct ExportStats(int Models, int Textures, int ClipsFound, int ClipsExported, int ClipsSkipped);
readonly record struct WriteTexturesResult(int Count, List<string> FileNames);

// Asset types for selecting the correct animation slot map.
// Overworld characters (GARC a/2/0/0) and Pokemon battle models (GARC a/0/9/4)
// use completely different animation slot conventions.
enum AnimAssetType { Overworld, Pokemon, Unknown }

sealed class SplitExportManifest
{
    public int Version { get; set; }
    public required string Mode { get; set; }
    public required List<string> Textures { get; set; }
    public required List<SplitModelManifestEntry> Models { get; set; }
}

sealed class SplitModelManifestEntry
{
    public required string Name { get; set; }
    public required string ModelFile { get; set; }
    public required List<SplitClipManifestEntry> Clips { get; set; }
}

sealed class SplitClipManifestEntry
{
    public int Index { get; set; }
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string SourceName { get; set; }
    public string? SemanticName { get; set; }
    public string? SemanticSource { get; set; }
    public required string File { get; set; }
    public float FrameCount { get; set; }
    public float Fps { get; set; }
}

sealed class GroupingOutcome
{
    public required List<GroupedEntry> Groups { get; set; }
    public bool LimitExcludedAnimations { get; set; }
}

sealed class GroupedEntry
{
    public int StartEntry { get; set; }
    public int EndEntry { get; set; }
    public required RenderBase.OModelGroup ModelGroup { get; set; }
}

sealed class EntryDiagnostics
{
    public int ModelCount { get; set; }
    public int MeshCount { get; set; }
    public int TextureCount { get; set; }
    public required string SegmentSummary { get; set; }
    public required string Note { get; set; }
}
