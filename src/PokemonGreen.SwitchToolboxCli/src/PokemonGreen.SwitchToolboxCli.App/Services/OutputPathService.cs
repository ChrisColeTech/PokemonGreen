namespace PokemonGreen.SwitchToolboxCli.App.Services;

internal static class OutputPathService
{
    public static string SanitizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "item";
        }

        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sanitized = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                continue;
            }

            var cleaned = SanitizeSegment(segment);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                sanitized.Add(cleaned);
            }
        }

        return sanitized.Count == 0 ? "item" : Path.Combine(sanitized.ToArray());
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}
