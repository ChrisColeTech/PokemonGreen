using System.Text.RegularExpressions;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Services;

public static class MapClassParser
{
    public static Result<MapData> ParseFile(string filePath)
    {
        try
        {
            var source = File.ReadAllText(filePath);
            return ParseSource(source);
        }
        catch (Exception ex)
        {
            return Result<MapData>.Fail($"{filePath}: Failed to read file: {ex.Message}");
        }
    }

    public static Result<MapData> ParseSource(string source)
    {
        var mapId = ExtractStringLiteral(source, "base(\"");
        if (mapId is null)
            return Result<MapData>.Fail("Could not extract mapId");

        var displayName = ExtractStringLiteralAfter(source, mapId);
        if (displayName is null)
            return Result<MapData>.Fail("Could not extract displayName");

        var dimensions = ExtractDimensions(source, mapId, displayName);
        if (dimensions is null)
            return Result<MapData>.Fail("Could not extract dimensions (width, height, tileSize)");

        var (width, height, tileSize) = dimensions.Value;

        var baseTiles = ExtractIntArray(source, "BaseTileData");
        if (baseTiles is null)
            return Result<MapData>.Fail("Could not extract BaseTileData");

        var overlayTiles = ExtractNullableIntArray(source, "OverlayTileData");
        if (overlayTiles is null)
            return Result<MapData>.Fail("Could not extract OverlayTileData");

        var walkableTileIds = ExtractIntArray(source, "WalkableTileIds");
        if (walkableTileIds is null)
            return Result<MapData>.Fail("Could not extract WalkableTileIds");

        return Result<MapData>.Ok(new MapData(
            mapId,
            displayName,
            width,
            height,
            tileSize,
            baseTiles,
            overlayTiles,
            walkableTileIds
        ));
    }

    private static string? ExtractStringLiteral(string source, string prefix)
    {
        var idx = source.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + prefix.Length;
        var end = source.IndexOf('"', start);
        return end < 0 ? null : source[start..end];
    }

    private static string? ExtractStringLiteralAfter(string source, string afterValue)
    {
        var pattern = $"\"{Regex.Escape(afterValue)}\", \"";
        var idx = source.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + pattern.Length;
        var end = source.IndexOf('"', start);
        return end < 0 ? null : source[start..end];
    }

    private static (int width, int height, int tileSize)? ExtractDimensions(string source, string mapId, string displayName)
    {
        var pattern = $": base\\(\"{Regex.Escape(mapId)}\", \"{Regex.Escape(displayName)}\", (\\d+), (\\d+), (\\d+)";
        var match = Regex.Match(source, pattern);
        if (!match.Success) return null;

        if (int.TryParse(match.Groups[1].Value, out var width) &&
            int.TryParse(match.Groups[2].Value, out var height) &&
            int.TryParse(match.Groups[3].Value, out var tileSize))
        {
            return (width, height, tileSize);
        }

        return null;
    }

    private static int[]? ExtractIntArray(string source, string arrayName)
    {
        var pattern = $"{arrayName}\\s*=\\s*\\[([^\\]]+)\\]";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return null;

        var content = match.Groups[1].Value;
        var tokens = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<int>();
        foreach (var token in tokens)
        {
            if (int.TryParse(token, out var value))
                result.Add(value);
        }

        return result.ToArray();
    }

    private static int?[]? ExtractNullableIntArray(string source, string arrayName)
    {
        var pattern = $"{arrayName}\\s*=\\s*\\[([^\\]]+)\\]";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return null;

        var content = match.Groups[1].Value;
        var tokens = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<int?>();
        foreach (var token in tokens)
        {
            if (token.Equals("null", StringComparison.OrdinalIgnoreCase))
                result.Add(null);
            else if (int.TryParse(token, out var value))
                result.Add(value);
        }

        return result.ToArray();
    }
}
