using System.Text;
using PokemonGreen.Core.Maps;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Services;

public static class CodeGenerator
{
    /// <summary>
    /// Generates a sealed C# class extending MapDefinition from a schema v2 map model.
    /// Output matches the flat-array constructor pattern.
    /// </summary>
    public static string GenerateMapClass(MapJsonModel map, string className)
    {
        var sb = new StringBuilder();
        int w = map.Width;
        int h = map.Height;
        bool hasOverlay = HasAnyOverlay(map.OverlayTiles, w, h);

        // Collect walkable tile IDs from TileRegistry
        var usedTileIds = CollectUsedTileIds(map);
        var walkableIds = usedTileIds
            .Where(id => TileRegistry.GetTile(id) is { Walkable: true })
            .OrderBy(id => id)
            .ToList();

        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace PokemonGreen.Core.Maps;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {className} : MapDefinition");
        sb.AppendLine("{");

        // BaseTileData
        sb.AppendLine("    private static readonly int[] BaseTileData =");
        sb.AppendLine("    [");
        for (int y = 0; y < h; y++)
        {
            sb.Append("        ");
            for (int x = 0; x < w; x++)
            {
                int tileId = (map.BaseTiles.Length > y && map.BaseTiles[y].Length > x)
                    ? map.BaseTiles[y][x]
                    : 0;
                sb.Append(tileId);
                if (x < w - 1 || y < h - 1)
                    sb.Append(", ");
            }
            sb.AppendLine();
        }
        sb.AppendLine("    ];");
        sb.AppendLine();

        // OverlayTileData
        sb.AppendLine("    private static readonly int?[] OverlayTileData =");
        sb.AppendLine("    [");
        for (int y = 0; y < h; y++)
        {
            sb.Append("        ");
            for (int x = 0; x < w; x++)
            {
                int? val = GetOverlayValue(map.OverlayTiles, x, y);
                if (val.HasValue)
                {
                    sb.Append($"{val.Value,4}");
                }
                else
                {
                    sb.Append("null");
                }
                if (x < w - 1 || y < h - 1)
                    sb.Append(", ");
            }
            sb.AppendLine();
        }
        sb.AppendLine("    ];");
        sb.AppendLine();

        // WalkableTileIds
        sb.AppendLine("    private static readonly int[] WalkableTileIds =");
        sb.AppendLine("    [");
        if (walkableIds.Count > 0)
        {
            sb.Append("        ");
            sb.AppendJoin(", ", walkableIds);
            sb.AppendLine();
        }
        sb.AppendLine("    ];");
        sb.AppendLine();

        // Singleton + constructor
        sb.AppendLine($"    public static {className} Instance {{ get; }} = new();");
        sb.AppendLine();
        sb.AppendLine($"    private {className}()");
        sb.AppendLine($"        : base(\"{EscapeString(map.MapId)}\", \"{EscapeString(map.DisplayName)}\", {w}, {h}, {map.TileSize}, BaseTileData, OverlayTileData, WalkableTileIds)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static HashSet<int> CollectUsedTileIds(MapJsonModel map)
    {
        var ids = new HashSet<int>();
        foreach (var row in map.BaseTiles)
        {
            foreach (var id in row)
                ids.Add(id);
        }
        if (map.OverlayTiles != null)
        {
            foreach (var row in map.OverlayTiles)
            {
                if (row == null) continue;
                foreach (var id in row)
                {
                    if (id.HasValue)
                        ids.Add(id.Value);
                }
            }
        }
        return ids;
    }

    private static bool HasAnyOverlay(int?[][]? overlay, int w, int h)
    {
        if (overlay == null) return false;
        for (int y = 0; y < h && y < overlay.Length; y++)
        {
            if (overlay[y] == null) continue;
            for (int x = 0; x < w && x < overlay[y].Length; x++)
            {
                if (overlay[y][x].HasValue) return true;
            }
        }
        return false;
    }

    private static int? GetOverlayValue(int?[][]? overlay, int x, int y)
    {
        if (overlay == null) return null;
        if (y >= overlay.Length) return null;
        if (overlay[y] == null) return null;
        if (x >= overlay[y].Length) return null;
        return overlay[y][x];
    }

    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
