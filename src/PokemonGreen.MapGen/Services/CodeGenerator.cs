using System.Reflection;
using System.Text;
using PokemonGreen.Core.Maps;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Services;

public static class CodeGenerator
{
    private static readonly string MapClassTemplate = LoadTemplate("MapClass.tpl");
    private static readonly string MapRegistryTemplate = LoadTemplate("MapRegistry.tpl");

    public static string GenerateMapClass(MapJsonModel map, string className)
    {
        int w = map.Width;
        int h = map.Height;

        var usedTileIds = CollectUsedTileIds(map);
        var walkableIds = usedTileIds
            .Where(id => TileRegistry.GetTile(id) is { Walkable: true })
            .OrderBy(id => id)
            .ToList();

        bool hasWarps = map.Warps is { Count: > 0 };
        bool hasConnections = map.Connections is { Count: > 0 };
        bool hasWorldPos = map.WorldX != 0 || map.WorldY != 0;

        // Build extra constructor args: warps?, connections?, worldX?, worldY?
        var extraArgParts = new List<string>();

        if (hasWarps || hasConnections || hasWorldPos)
            extraArgParts.Add(hasWarps ? "WarpData" : "null");

        if (hasConnections || hasWorldPos)
            extraArgParts.Add(hasConnections ? "ConnectionData" : "null");

        if (hasWorldPos)
        {
            extraArgParts.Add(map.WorldX.ToString());
            extraArgParts.Add(map.WorldY.ToString());
        }

        var extraArgs = extraArgParts.Count > 0
            ? ", " + string.Join(", ", extraArgParts)
            : "";

        return MapClassTemplate
            .Replace("{{CLASS_NAME}}", className)
            .Replace("{{MAP_ID}}", EscapeString(map.MapId))
            .Replace("{{DISPLAY_NAME}}", EscapeString(map.DisplayName))
            .Replace("{{WIDTH}}", w.ToString())
            .Replace("{{HEIGHT}}", h.ToString())
            .Replace("{{TILE_SIZE}}", map.TileSize.ToString())
            .Replace("{{BASE_TILE_DATA}}", FormatBaseTiles(map, w, h))
            .Replace("{{OVERLAY_TILE_DATA}}", FormatOverlayTiles(map, w, h))
            .Replace("{{WALKABLE_TILE_IDS}}", FormatWalkableIds(walkableIds))
            .Replace("{{WARP_DATA}}", FormatWarps(map.Warps))
            .Replace("{{CONNECTION_DATA}}", FormatConnections(map.Connections))
            .Replace("{{EXTRA_CTOR_ARGS}}", extraArgs);
    }

    public static string GenerateMapRegistry(List<string> classNames)
    {
        var sb = new StringBuilder();
        foreach (var name in classNames)
            sb.AppendLine($"        _ = {name}.Instance;");

        return MapRegistryTemplate
            .Replace("{{INSTANCES}}", sb.ToString().TrimEnd());
    }

    #region Formatters

    private static string FormatBaseTiles(MapJsonModel map, int w, int h)
    {
        var sb = new StringBuilder();
        for (int y = 0; y < h; y++)
        {
            sb.Append("        ");
            for (int x = 0; x < w; x++)
            {
                int tileId = (map.BaseTiles.Length > y && map.BaseTiles[y].Length > x)
                    ? map.BaseTiles[y][x] : 0;
                sb.Append(tileId);
                if (x < w - 1 || y < h - 1) sb.Append(", ");
            }
            if (y < h - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatOverlayTiles(MapJsonModel map, int w, int h)
    {
        var sb = new StringBuilder();
        for (int y = 0; y < h; y++)
        {
            sb.Append("        ");
            for (int x = 0; x < w; x++)
            {
                int? val = GetOverlayValue(map.OverlayTiles, x, y);
                sb.Append(val.HasValue ? $"{val.Value,4}" : "null");
                if (x < w - 1 || y < h - 1) sb.Append(", ");
            }
            if (y < h - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatWalkableIds(List<int> ids)
    {
        if (ids.Count == 0) return "";
        return "        " + string.Join(", ", ids);
    }

    private static string FormatWarps(List<WarpJsonModel>? warps)
    {
        if (warps is not { Count: > 0 }) return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("    private static readonly WarpConnection[] WarpData =");
        sb.AppendLine("    [");
        foreach (var warp in warps)
        {
            var trigger = string.Equals(warp.Trigger, "interact", StringComparison.OrdinalIgnoreCase)
                ? "WarpTrigger.Interact" : "WarpTrigger.Step";
            sb.AppendLine($"        new({warp.X}, {warp.Y}, \"{EscapeString(warp.TargetMapId)}\", {warp.TargetX}, {warp.TargetY}, {trigger}),");
        }
        sb.AppendLine("    ];");
        return sb.ToString();
    }

    private static string FormatConnections(List<ConnectionJsonModel>? connections)
    {
        if (connections is not { Count: > 0 }) return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("    private static readonly MapConnection[] ConnectionData =");
        sb.AppendLine("    [");
        foreach (var conn in connections)
        {
            var edge = conn.Edge.ToLowerInvariant() switch
            {
                "north" => "MapEdge.North",
                "south" => "MapEdge.South",
                "east" => "MapEdge.East",
                "west" => "MapEdge.West",
                _ => "MapEdge.North"
            };
            sb.AppendLine($"        new({edge}, \"{EscapeString(conn.TargetMapId)}\", {conn.Offset}),");
        }
        sb.AppendLine("    ];");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static HashSet<int> CollectUsedTileIds(MapJsonModel map)
    {
        var ids = new HashSet<int>();
        foreach (var row in map.BaseTiles)
            foreach (var id in row)
                ids.Add(id);
        if (map.OverlayTiles != null)
            foreach (var row in map.OverlayTiles)
            {
                if (row == null) continue;
                foreach (var id in row)
                    if (id.HasValue) ids.Add(id.Value);
            }
        return ids;
    }

    private static int? GetOverlayValue(int?[][]? overlay, int x, int y)
    {
        if (overlay == null || y >= overlay.Length) return null;
        if (overlay[y] == null || x >= overlay[y].Length) return null;
        return overlay[y][x];
    }

    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string LoadTemplate(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name))
            ?? throw new InvalidOperationException($"Template not found: {name}");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion
}
