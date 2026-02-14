using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Commands;

public static partial class ExportCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Exports .g.cs files back to schema v2 .map.json format.
    /// </summary>
    public static void Run(string inputFolder, string? outputFolder = null)
    {
        inputFolder = Path.GetFullPath(inputFolder);

        if (!Directory.Exists(inputFolder))
        {
            Console.Error.WriteLine($"Error: Input folder does not exist: {inputFolder}");
            Environment.Exit(1);
        }

        outputFolder ??= inputFolder;
        outputFolder = Path.GetFullPath(outputFolder);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        Console.WriteLine($"Input folder: {inputFolder}");
        Console.WriteLine($"Output folder: {outputFolder}");
        Console.WriteLine();

        var csFiles = Directory.GetFiles(inputFolder, "*.g.cs", SearchOption.AllDirectories);

        if (csFiles.Length == 0)
        {
            Console.WriteLine("No *.g.cs files found.");
            return;
        }

        Console.WriteLine($"Found {csFiles.Length} generated file(s).");
        Console.WriteLine();

        int successCount = 0;
        int errorCount = 0;

        foreach (var csFile in csFiles)
        {
            try
            {
                var code = File.ReadAllText(csFile);
                var map = ParseGeneratedCode(code);

                if (map == null)
                {
                    Console.WriteLine($"Skipped (not a valid map definition): {csFile}");
                    continue;
                }

                var outputFile = Path.Combine(outputFolder, $"{map.MapId}.map.json");
                var json = JsonSerializer.Serialize(map, JsonOptions);
                File.WriteAllText(outputFile, json);

                Console.WriteLine($"Exported: {outputFile}");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {csFile}: {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Exported {successCount} file(s), {errorCount} error(s).");
    }

    /// <summary>
    /// Parses the new flat-array .g.cs format and extracts a schema v2 model.
    /// </summary>
    private static MapJsonModel? ParseGeneratedCode(string code)
    {
        // Extract constructor args: base("id", "name", width, height, tileSize, ...)
        var ctorMatch = ConstructorRegex().Match(code);
        if (!ctorMatch.Success)
            return null;

        var mapId = ctorMatch.Groups[1].Value;
        var displayName = ctorMatch.Groups[2].Value;
        int width = int.Parse(ctorMatch.Groups[3].Value);
        int height = int.Parse(ctorMatch.Groups[4].Value);
        int tileSize = int.Parse(ctorMatch.Groups[5].Value);

        // Extract BaseTileData array
        var baseData = ExtractIntArray(code, "BaseTileData");
        if (baseData == null || baseData.Length != width * height)
            return null;

        // Extract OverlayTileData array
        var overlayData = ExtractNullableIntArray(code, "OverlayTileData");

        // Convert flat arrays to 2D jagged arrays [y][x]
        var baseTiles = new int[height][];
        int?[][]? overlayTiles = overlayData != null ? new int?[height][] : null;

        for (int y = 0; y < height; y++)
        {
            baseTiles[y] = new int[width];
            if (overlayTiles != null)
                overlayTiles[y] = new int?[width];

            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                baseTiles[y][x] = baseData[i];
                if (overlayTiles != null && overlayData != null && i < overlayData.Length)
                    overlayTiles[y][x] = overlayData[i];
            }
        }

        // Extract warps and connections
        var warps = ExtractWarps(code);
        var connections = ExtractConnections(code);

        // Extract worldX/worldY from constructor call
        var (worldX, worldY) = ExtractWorldPosition(code);

        return new MapJsonModel
        {
            SchemaVersion = 2,
            MapId = mapId,
            DisplayName = displayName,
            TileSize = tileSize,
            Width = width,
            Height = height,
            BaseTiles = baseTiles,
            OverlayTiles = overlayTiles,
            Warps = warps?.Count > 0 ? warps : null,
            Connections = connections?.Count > 0 ? connections : null,
            WorldX = worldX,
            WorldY = worldY
        };
    }

    /// <summary>
    /// Extracts an int[] from a static readonly field declaration.
    /// </summary>
    private static int[]? ExtractIntArray(string code, string fieldName)
    {
        // Match: private static readonly int[] FieldName = [ ... ];
        var pattern = $@"int\[\]\s+{Regex.Escape(fieldName)}\s*=\s*\[([\s\S]*?)\];";
        var match = Regex.Match(code, pattern);
        if (!match.Success)
            return null;

        var body = match.Groups[1].Value;
        var numbers = Regex.Matches(body, @"-?\d+");
        return numbers.Select(m => int.Parse(m.Value)).ToArray();
    }

    /// <summary>
    /// Extracts an int?[] from a static readonly field declaration (handles "null" literals).
    /// </summary>
    private static int?[]? ExtractNullableIntArray(string code, string fieldName)
    {
        var pattern = $@"int\?\[\]\s+{Regex.Escape(fieldName)}\s*=\s*\[([\s\S]*?)\];";
        var match = Regex.Match(code, pattern);
        if (!match.Success)
            return null;

        var body = match.Groups[1].Value;
        var tokens = Regex.Matches(body, @"null|-?\d+");
        return tokens.Select(m =>
            m.Value == "null" ? (int?)null : int.Parse(m.Value)
        ).ToArray();
    }

    /// <summary>
    /// Extracts WarpConnection entries from the generated Warps array.
    /// </summary>
    private static List<WarpJsonModel>? ExtractWarps(string code)
    {
        // Match the Warps array block
        var arrayMatch = Regex.Match(code, @"WarpConnection\[\]\s+WarpData\s*=\s*\[([\s\S]*?)\];");
        if (!arrayMatch.Success)
            return null;

        var body = arrayMatch.Groups[1].Value;

        // Match each new(...) entry
        var entries = WarpEntryRegex().Matches(body);
        if (entries.Count == 0)
            return null;

        var warps = new List<WarpJsonModel>();
        foreach (Match entry in entries)
        {
            var trigger = entry.Groups[6].Value.Contains("Interact", StringComparison.OrdinalIgnoreCase)
                ? "interact"
                : "step";

            warps.Add(new WarpJsonModel
            {
                X = int.Parse(entry.Groups[1].Value),
                Y = int.Parse(entry.Groups[2].Value),
                TargetMapId = entry.Groups[3].Value,
                TargetX = int.Parse(entry.Groups[4].Value),
                TargetY = int.Parse(entry.Groups[5].Value),
                Trigger = trigger
            });
        }

        return warps;
    }

    /// <summary>
    /// Extracts MapConnection entries from the generated ConnectionData array.
    /// </summary>
    private static List<ConnectionJsonModel>? ExtractConnections(string code)
    {
        var arrayMatch = Regex.Match(code, @"MapConnection\[\]\s+ConnectionData\s*=\s*\[([\s\S]*?)\];");
        if (!arrayMatch.Success)
            return null;

        var body = arrayMatch.Groups[1].Value;
        var entries = ConnectionEntryRegex().Matches(body);
        if (entries.Count == 0)
            return null;

        var connections = new List<ConnectionJsonModel>();
        foreach (Match entry in entries)
        {
            var edge = entry.Groups[1].Value switch
            {
                "MapEdge.North" => "north",
                "MapEdge.South" => "south",
                "MapEdge.East" => "east",
                "MapEdge.West" => "west",
                _ => "north"
            };
            connections.Add(new ConnectionJsonModel
            {
                Edge = edge,
                TargetMapId = entry.Groups[2].Value,
                Offset = int.Parse(entry.Groups[3].Value)
            });
        }
        return connections;
    }

    /// <summary>
    /// Extracts worldX and worldY from the constructor call.
    /// Looks for the pattern: ..., WalkableTileIds, warps?, connections?, worldX, worldY)
    /// </summary>
    private static (int worldX, int worldY) ExtractWorldPosition(string code)
    {
        var match = WorldPositionRegex().Match(code);
        if (!match.Success)
            return (0, 0);
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    // Matches: base("mapId", "displayName", width, height, tileSize, ...)
    [GeneratedRegex(@"base\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,")]
    private static partial Regex ConstructorRegex();

    // Matches worldX, worldY as the last two integer args before the closing paren
    // Pattern: ..., <int>, <int>)  where neither int is preceded by a quote (to avoid matching targetX/Y in warps)
    [GeneratedRegex(@",\s*(-?\d+)\s*,\s*(-?\d+)\s*\)\s*$", RegexOptions.Multiline)]
    private static partial Regex WorldPositionRegex();

    // Matches: new(x, y, "targetMapId", targetX, targetY, WarpTrigger.Step)
    [GeneratedRegex(@"new\(\s*(\d+)\s*,\s*(\d+)\s*,\s*""([^""]*)""\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(WarpTrigger\.\w+)\s*\)")]
    private static partial Regex WarpEntryRegex();

    // Matches: new(MapEdge.North, "targetMapId", offset)
    [GeneratedRegex(@"new\(\s*(MapEdge\.\w+)\s*,\s*""([^""]*)""\s*,\s*(-?\d+)\s*\)")]
    private static partial Regex ConnectionEntryRegex();
}
