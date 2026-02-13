using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokemonGreen.Core.Maps;

const int ExpectedSchemaVersionMin = 1;
const int ExpectedSchemaVersionMax = 2;
const int MinDimension = 1;
const int MaxDimension = 512;

var optionsResult = GeneratorOptions.Parse(args);
if (!optionsResult.IsSuccess)
{
    Console.Error.WriteLine(optionsResult.ErrorMessage);
    return 1;
}

var options = optionsResult.Value;
if (!Directory.Exists(options.InputDirectory))
{
    Console.Error.WriteLine($"Input directory does not exist: {options.InputDirectory}");
    return 1;
}

Directory.CreateDirectory(options.OutputDirectory);

var mapFiles = Directory
    .EnumerateFiles(options.InputDirectory, "*.map.json", SearchOption.TopDirectoryOnly)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (mapFiles.Length == 0)
{
    Console.Error.WriteLine($"No .map.json files found in: {options.InputDirectory}");
    return 1;
}

var parsedMaps = new List<ValidatedMapData>();
var parseErrors = new List<string>();

var runtimeRenderableTileIdsResult = BuildRuntimeRenderableTileIds();
if (!runtimeRenderableTileIdsResult.IsSuccess)
{
    Console.Error.WriteLine(runtimeRenderableTileIdsResult.ErrorMessage);
    return 1;
}

var runtimeRenderableTileIds = runtimeRenderableTileIdsResult.Value;
var decorationTileIds = BuildDecorationTileIds();

foreach (var mapFile in mapFiles)
{
    var parseResult = TryParseAndValidateMap(mapFile, runtimeRenderableTileIds, decorationTileIds);
    if (!parseResult.IsSuccess)
    {
        parseErrors.Add(parseResult.ErrorMessage);
        continue;
    }

    parsedMaps.Add(parseResult.Value);
}

if (parseErrors.Count > 0)
{
    foreach (var error in parseErrors)
    {
        Console.Error.WriteLine(error);
    }

    return 1;
}

var duplicateMapIds = parsedMaps
    .GroupBy(map => map.MapId, StringComparer.Ordinal)
    .Where(group => group.Count() > 1)
    .Select(group => group.Key)
    .ToArray();

if (duplicateMapIds.Length > 0)
{
    foreach (var duplicateMapId in duplicateMapIds)
    {
        Console.Error.WriteLine($"Duplicate mapId found: {duplicateMapId}");
    }

    return 1;
}

var generatedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var map in parsedMaps)
{
    var generatedCode = GenerateMapClass(map, options.GeneratedNamespace);
    var outputFileName = $"{map.ClassName}.g.cs";
    if (!generatedFileNames.Add(outputFileName))
    {
        Console.Error.WriteLine($"Generated class name collision: {map.ClassName}");
        return 1;
    }

    var outputPath = Path.Combine(options.OutputDirectory, outputFileName);
    File.WriteAllText(outputPath, generatedCode, Encoding.UTF8);
    Console.WriteLine($"Generated {outputPath}");
}

var catalogCode = GenerateCatalogClass(parsedMaps, options.GeneratedNamespace);
var catalogOutputPath = Path.Combine(options.OutputDirectory, "GeneratedMapCatalog.g.cs");
File.WriteAllText(catalogOutputPath, catalogCode, Encoding.UTF8);
Console.WriteLine($"Generated {catalogOutputPath}");

Console.WriteLine($"Map generation complete. Files generated: {parsedMaps.Count + 1}");
return 0;

static HashSet<int> BuildDecorationTileIds() =>
[
    3,   // Tree
    8,   // Rock
    19,  // Flower
    49,  // Statue
    26,  // Strength Rock
    27,  // Cut Tree
];

static Result<ValidatedMapData> TryParseAndValidateMap(
    string mapFilePath, 
    HashSet<int> runtimeRenderableTileIds,
    HashSet<int> decorationTileIds)
{
    JsonElement jsonRoot;

    try
    {
        var json = File.ReadAllText(mapFilePath);
        jsonRoot = JsonSerializer.Deserialize<JsonElement>(json, CreateSerializerOptions());
    }
    catch (Exception ex)
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: failed to parse JSON. {ex.Message}");
    }

    if (jsonRoot.ValueKind != JsonValueKind.Object)
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: JSON payload must be an object.");
    }

    if (!jsonRoot.TryGetProperty("schemaVersion", out var schemaVersionElement))
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: schemaVersion is required.");
    }

    var schemaVersion = schemaVersionElement.GetInt32();
    if (schemaVersion < ExpectedSchemaVersionMin || schemaVersion > ExpectedSchemaVersionMax)
    {
        return Result<ValidatedMapData>.Fail(
            $"{mapFilePath}: schemaVersion must be {ExpectedSchemaVersionMin}-{ExpectedSchemaVersionMax}, got {schemaVersion}.");
    }

    if (!jsonRoot.TryGetProperty("mapId", out var mapIdElement))
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: mapId is required.");
    }

    var mapId = mapIdElement.GetString() ?? "";
    if (string.IsNullOrWhiteSpace(mapId))
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: mapId is required.");
    }

    if (!IsValidMapId(mapId))
    {
        return Result<ValidatedMapData>.Fail(
            $"{mapFilePath}: mapId '{mapId}' is invalid. Use letters, numbers, underscores, or hyphens.");
    }

    if (!jsonRoot.TryGetProperty("width", out var widthElement) || 
        !jsonRoot.TryGetProperty("height", out var heightElement))
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: width and height are required.");
    }

    var width = widthElement.GetInt32();
    var height = heightElement.GetInt32();

    if (width < MinDimension || width > MaxDimension || height < MinDimension || height > MaxDimension)
    {
        return Result<ValidatedMapData>.Fail(
            $"{mapFilePath}: width and height must be between {MinDimension} and {MaxDimension}.");
    }

    if (!jsonRoot.TryGetProperty("tileSize", out var tileSizeElement))
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: tileSize is required.");
    }

    var tileSize = tileSizeElement.GetInt32();
    if (tileSize <= 0)
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: tileSize must be positive.");
    }

    if (!jsonRoot.TryGetProperty("tileTypes", out var tileTypesElement) ||
        tileTypesElement.ValueKind != JsonValueKind.Object)
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: tileTypes must be an object.");
    }

    var tileDefinitions = new Dictionary<int, MapTileTypeContract>();
    foreach (var property in tileTypesElement.EnumerateObject())
    {
        if (!int.TryParse(property.Name, out var tileId))
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: invalid tileTypes key '{property.Name}'.");
        }

        try
        {
            var tileType = JsonSerializer.Deserialize<MapTileTypeContract>(property.Value.GetRawText(), CreateSerializerOptions());
            if (tileType is null)
            {
                return Result<ValidatedMapData>.Fail($"{mapFilePath}: tileType for {tileId} is invalid.");
            }
            tileDefinitions[tileId] = tileType;
        }
        catch (Exception ex)
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: failed to parse tileType {tileId}: {ex.Message}");
        }
    }

    if (tileDefinitions.Count == 0)
    {
        return Result<ValidatedMapData>.Fail($"{mapFilePath}: tileTypes must define at least one tile id.");
    }

    var knownTileIds = tileDefinitions.Keys.ToHashSet();
    var nonRenderableTileTypeIds = knownTileIds
        .Where(tileId => !runtimeRenderableTileIds.Contains(tileId))
        .OrderBy(tileId => tileId)
        .ToArray();

    if (nonRenderableTileTypeIds.Length > 0)
    {
        return Result<ValidatedMapData>.Fail(
            $"{mapFilePath}: tileTypes defines ids not renderable by runtime catalog: {string.Join(", ", nonRenderableTileTypeIds)}.");
    }

    var walkableTileIds = tileDefinitions
        .Where(entry => entry.Value.Walkable)
        .Select(entry => entry.Key)
        .OrderBy(tileId => tileId)
        .ToArray();

    string displayName = mapId;
    if (jsonRoot.TryGetProperty("displayName", out var displayNameElement))
    {
        displayName = displayNameElement.GetString() ?? mapId;
    }

    int[] flatBaseTileData;
    int?[] flatOverlayTileData;

    if (schemaVersion == 1)
    {
        if (!jsonRoot.TryGetProperty("tiles", out var tilesElement))
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: tiles is required for schema v1.");
        }

        var (baseTiles, overlayTiles) = ParseLegacyTiles(tilesElement, width, height, decorationTileIds, mapFilePath);
        if (baseTiles is null)
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: failed to parse legacy tiles.");
        }

        flatBaseTileData = baseTiles;
        flatOverlayTileData = overlayTiles!;
    }
    else
    {
        if (!jsonRoot.TryGetProperty("baseTiles", out var baseTilesElement))
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: baseTiles is required for schema v2.");
        }

        if (!jsonRoot.TryGetProperty("overlayTiles", out var overlayTilesElement))
        {
            return Result<ValidatedMapData>.Fail($"{mapFilePath}: overlayTiles is required for schema v2.");
        }

        var baseResult = ParseTileLayer(baseTilesElement, width, height, knownTileIds, runtimeRenderableTileIds, mapFilePath, "baseTiles");
        if (!baseResult.IsSuccess)
        {
            return Result<ValidatedMapData>.Fail(baseResult.ErrorMessage);
        }
        flatBaseTileData = baseResult.Value!;

        var overlayResult = ParseOverlayTileLayer(overlayTilesElement, width, height, decorationTileIds, runtimeRenderableTileIds, mapFilePath);
        if (!overlayResult.IsSuccess)
        {
            return Result<ValidatedMapData>.Fail(overlayResult.ErrorMessage);
        }
        flatOverlayTileData = overlayResult.Value!;
    }

    var className = BuildClassName(mapId);
    if (string.IsNullOrWhiteSpace(className))
    {
        return Result<ValidatedMapData>.Fail(
            $"{mapFilePath}: could not derive a C# class name from mapId '{mapId}'.");
    }

    return Result<ValidatedMapData>.Ok(new ValidatedMapData(
        MapId: mapId,
        DisplayName: string.IsNullOrWhiteSpace(displayName) ? mapId : displayName.Trim(),
        ClassName: className,
        Width: width,
        Height: height,
        TileSize: tileSize,
        FlatBaseTileData: flatBaseTileData,
        FlatOverlayTileData: flatOverlayTileData,
        WalkableTileIds: walkableTileIds));
}

static (int[]? baseTiles, int?[]? overlayTiles) ParseLegacyTiles(
    JsonElement tilesElement,
    int width,
    int height,
    HashSet<int> decorationTileIds,
    string mapFilePath)
{
    if (tilesElement.ValueKind != JsonValueKind.Array)
    {
        return (null, null);
    }

    var rows = tilesElement.EnumerateArray().ToArray();
    if (rows.Length != height)
    {
        return (null, null);
    }

    var baseTiles = new int[width * height];
    var overlayTiles = new int?[width * height];

    for (var y = 0; y < height; y++)
    {
        var row = rows[y];
        if (row.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        var cells = row.EnumerateArray().ToArray();
        if (cells.Length != width)
        {
            return (null, null);
        }

        for (var x = 0; x < width; x++)
        {
            var tileId = cells[x].GetInt32();
            var index = (y * width) + x;

            if (decorationTileIds.Contains(tileId))
            {
                baseTiles[index] = 1; // Default to grass for decorations
                overlayTiles[index] = tileId;
            }
            else
            {
                baseTiles[index] = tileId;
                overlayTiles[index] = null;
            }
        }
    }

    return (baseTiles, overlayTiles);
}

static Result<int[]> ParseTileLayer(
    JsonElement tilesElement,
    int width,
    int height,
    HashSet<int> knownTileIds,
    HashSet<int> runtimeRenderableTileIds,
    string mapFilePath,
    string layerName)
{
    if (tilesElement.ValueKind != JsonValueKind.Array)
    {
        return Result<int[]>.Fail($"{mapFilePath}: {layerName} must be an array.");
    }

    var rows = tilesElement.EnumerateArray().ToArray();
    if (rows.Length != height)
    {
        return Result<int[]>.Fail($"{mapFilePath}: {layerName} row count must match height ({height}).");
    }

    var flatTileData = new int[width * height];

    for (var y = 0; y < height; y++)
    {
        var row = rows[y];
        if (row.ValueKind != JsonValueKind.Array)
        {
            return Result<int[]>.Fail($"{mapFilePath}: {layerName} row {y} must be an array.");
        }

        var cells = row.EnumerateArray().ToArray();
        if (cells.Length != width)
        {
            return Result<int[]>.Fail($"{mapFilePath}: {layerName} row {y} must contain exactly {width} entries.");
        }

        for (var x = 0; x < width; x++)
        {
            var tileId = cells[y].GetInt32();
            var index = (y * width) + x;

            if (!knownTileIds.Contains(tileId))
            {
                return Result<int[]>.Fail($"{mapFilePath}: {layerName} at ({x}, {y}) references undefined tile id {tileId}.");
            }

            if (!runtimeRenderableTileIds.Contains(tileId))
            {
                return Result<int[]>.Fail($"{mapFilePath}: {layerName} at ({x}, {y}) references tile id {tileId} which is not renderable.");
            }

            flatTileData[index] = tileId;
        }
    }

    return Result<int[]>.Ok(flatTileData);
}

static Result<int?[]> ParseOverlayTileLayer(
    JsonElement tilesElement,
    int width,
    int height,
    HashSet<int> decorationTileIds,
    HashSet<int> runtimeRenderableTileIds,
    string mapFilePath)
{
    if (tilesElement.ValueKind != JsonValueKind.Array)
    {
        return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles must be an array.");
    }

    var rows = tilesElement.EnumerateArray().ToArray();
    if (rows.Length != height)
    {
        return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles row count must match height ({height}).");
    }

    var flatTileData = new int?[width * height];

    for (var y = 0; y < height; y++)
    {
        var row = rows[y];
        if (row.ValueKind != JsonValueKind.Array)
        {
            return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles row {y} must be an array.");
        }

        var cells = row.EnumerateArray().ToArray();
        if (cells.Length != width)
        {
            return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles row {y} must contain exactly {width} entries.");
        }

        for (var x = 0; x < width; x++)
        {
            var cell = cells[x];
            var index = (y * width) + x;

            if (cell.ValueKind == JsonValueKind.Null)
            {
                flatTileData[index] = null;
                continue;
            }

            var tileId = cell.GetInt32();

            if (!decorationTileIds.Contains(tileId))
            {
                return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles at ({x}, {y}) has tile id {tileId} which is not a decoration tile.");
            }

            if (!runtimeRenderableTileIds.Contains(tileId))
            {
                return Result<int?[]>.Fail($"{mapFilePath}: overlayTiles at ({x}, {y}) references tile id {tileId} which is not renderable.");
            }

            flatTileData[index] = tileId;
        }
    }

    return Result<int?[]>.Ok(flatTileData);
}

static string GenerateMapClass(ValidatedMapData map, string generatedNamespace)
{
    var baseTileData = string.Join(", ", map.FlatBaseTileData);
    var overlayTileData = string.Join(", ", map.FlatOverlayTileData.Select(t => t?.ToString() ?? "null"));
    var walkableTileIds = string.Join(", ", map.WalkableTileIds);

    return $$"""
    #nullable enable

    namespace {{generatedNamespace}};

    public sealed class {{map.ClassName}} : MapDefinition
    {
        private static readonly int[] BaseTileData =
        [
            {{baseTileData}}
        ];

        private static readonly int?[] OverlayTileData =
        [
            {{overlayTileData}}
        ];

        private static readonly int[] WalkableTileIds =
        [
            {{walkableTileIds}}
        ];

        public static {{map.ClassName}} Instance { get; } = new();

        private {{map.ClassName}}()
            : base("{{EscapeForStringLiteral(map.MapId)}}", "{{EscapeForStringLiteral(map.DisplayName)}}", {{map.Width}}, {{map.Height}}, {{map.TileSize}}, BaseTileData, OverlayTileData, WalkableTileIds)
        {
        }
    }
    """;
}

static string GenerateCatalogClass(IReadOnlyList<ValidatedMapData> maps, string generatedNamespace)
{
    var orderedMaps = maps
        .OrderBy(map => map.MapId, StringComparer.Ordinal)
        .Select(map => $"        {map.ClassName}.Instance")
        .ToArray();

    var lookupEntries = maps
        .OrderBy(map => map.MapId, StringComparer.Ordinal)
        .Select(map => $"        [\"{EscapeForStringLiteral(map.MapId)}\"] = {map.ClassName}.Instance")
        .ToArray();

    var orderedMapBlock = string.Join(",\n", orderedMaps);
    var lookupBlock = string.Join(",\n", lookupEntries);

    return $$"""
    #nullable enable

    using System.Collections.Generic;

    namespace {{generatedNamespace}};

    public static partial class MapCatalog
    {
        private static readonly IReadOnlyList<MapDefinition> AllMaps =
        [
    {{orderedMapBlock}}
        ];

        private static readonly IReadOnlyDictionary<string, MapDefinition> MapsById = new Dictionary<string, MapDefinition>
        {
    {{lookupBlock}}
        };

        public static IReadOnlyList<MapDefinition> All => AllMaps;

        public static bool TryGetById(string mapId, out MapDefinition? map)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                map = null;
                return false;
            }

            return MapsById.TryGetValue(mapId, out map);
        }
    }
    """;
}

static bool IsValidMapId(string mapId)
{
    for (var index = 0; index < mapId.Length; index += 1)
    {
        var character = mapId[index];
        var isValid = char.IsLetterOrDigit(character) || character == '_' || character == '-';
        if (!isValid)
        {
            return false;
        }
    }

    return true;
}

static string BuildClassName(string mapId)
{
    var tokens = mapId
        .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(token =>
        {
            if (token.Length == 0)
            {
                return string.Empty;
            }

            if (token.Length == 1)
            {
                return char.ToUpperInvariant(token[0]).ToString();
            }

            return char.ToUpperInvariant(token[0]) + token[1..];
        })
        .Where(token => token.Length > 0)
        .ToArray();

    if (tokens.Length == 0)
    {
        return string.Empty;
    }

    var baseName = string.Concat(tokens);
    var prefixedName = char.IsDigit(baseName[0]) ? $"Map{baseName}" : baseName;
    return prefixedName.EndsWith("Map", StringComparison.Ordinal) ? prefixedName : $"{prefixedName}Map";
}

static string EscapeForStringLiteral(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

static Result<HashSet<int>> BuildRuntimeRenderableTileIds()
{
    TileRenderCatalog tileRenderCatalog;

    try
    {
        tileRenderCatalog = new TileRenderCatalog();
    }
    catch (Exception ex)
    {
        return Result<HashSet<int>>.Fail(
            $"Runtime tile render catalog is invalid: {ex.Message}");
    }

    var renderableTileIds = new HashSet<int>();
    var missingRuntimeTileIds = new List<int>();
    for (var tileId = TileRenderCatalog.MinKnownTileId; tileId <= TileRenderCatalog.MaxKnownTileId; tileId += 1)
    {
        if (!tileRenderCatalog.TryGetRule(tileId, out _))
        {
            missingRuntimeTileIds.Add(tileId);
            continue;
        }

        renderableTileIds.Add(tileId);
    }

    if (missingRuntimeTileIds.Count > 0)
    {
        return Result<HashSet<int>>.Fail(
            $"Runtime tile render catalog is missing expected ids: {string.Join(", ", missingRuntimeTileIds)}.");
    }

    return Result<HashSet<int>>.Ok(renderableTileIds);
}

static JsonSerializerOptions CreateSerializerOptions() => new()
{
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
};

internal sealed record GeneratorOptions(string InputDirectory, string OutputDirectory, string GeneratedNamespace)
{
    private const string InputOption = "--input";
    private const string OutputOption = "--output";
    private const string NamespaceOption = "--namespace";
    private const string DefaultNamespace = "PokemonGreen.Core.Maps";

    public static Result<GeneratorOptions> Parse(string[] args)
    {
        string? inputDirectory = null;
        string? outputDirectory = null;
        string generatedNamespace = DefaultNamespace;

        for (var index = 0; index < args.Length; index += 1)
        {
            var argument = args[index];
            switch (argument)
            {
                case InputOption:
                    if (!TryReadOptionValue(args, ref index, out var inputValue))
                    {
                        return Result<GeneratorOptions>.Fail($"Missing value for {InputOption}.");
                    }

                    inputDirectory = Path.GetFullPath(inputValue);
                    break;
                case OutputOption:
                    if (!TryReadOptionValue(args, ref index, out var outputValue))
                    {
                        return Result<GeneratorOptions>.Fail($"Missing value for {OutputOption}.");
                    }

                    outputDirectory = Path.GetFullPath(outputValue);
                    break;
                case NamespaceOption:
                    if (!TryReadOptionValue(args, ref index, out var namespaceValue))
                    {
                        return Result<GeneratorOptions>.Fail($"Missing value for {NamespaceOption}.");
                    }

                    generatedNamespace = namespaceValue.Trim();
                    break;
                default:
                    return Result<GeneratorOptions>.Fail($"Unknown argument: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(inputDirectory))
        {
            return Result<GeneratorOptions>.Fail(
                "Missing required input folder. Usage: dotnet run --project src/PokemonGreen.MapGen -- --input <folder> [--output <folder>] [--namespace <value>]");
        }

        if (string.IsNullOrWhiteSpace(generatedNamespace))
        {
            return Result<GeneratorOptions>.Fail("Namespace cannot be empty.");
        }

        var resolvedOutputDirectory = outputDirectory;
        if (string.IsNullOrWhiteSpace(resolvedOutputDirectory))
        {
            resolvedOutputDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../PokemonGreen.Core/Maps"));
        }

        return Result<GeneratorOptions>.Ok(new GeneratorOptions(
            InputDirectory: inputDirectory,
            OutputDirectory: resolvedOutputDirectory,
            GeneratedNamespace: generatedNamespace));
    }

    private static bool TryReadOptionValue(string[] args, ref int index, out string value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
    }
}

internal sealed record MapTileTypeContract(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("walkable")] bool Walkable,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("encounter")] string? Encounter,
    [property: JsonPropertyName("direction")] string? Direction);

internal sealed record ValidatedMapData(
    string MapId,
    string DisplayName,
    string ClassName,
    int Width,
    int Height,
    int TileSize,
    int[] FlatBaseTileData,
    int?[] FlatOverlayTileData,
    int[] WalkableTileIds);

internal readonly record struct Result<T>(bool IsSuccess, T Value, string ErrorMessage)
{
    public static Result<T> Ok(T value) => new(true, value, string.Empty);

    public static Result<T> Fail(string errorMessage) => new(false, default!, errorMessage);
}
