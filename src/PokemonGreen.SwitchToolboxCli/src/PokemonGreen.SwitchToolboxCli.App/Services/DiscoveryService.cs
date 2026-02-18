using PokemonGreen.SwitchToolboxCli.Core.Extraction;

namespace PokemonGreen.SwitchToolboxCli.App.Services;

public sealed class DiscoveryService
{
    private readonly ArchiveWalker _walker;

    public DiscoveryService(ArchiveWalker walker)
    {
        _walker = walker;
    }

    public IReadOnlyList<DiscoveredItem> Discover(string inputPath)
    {
        var discovered = new List<DiscoveredItem>();

        if (File.Exists(inputPath))
        {
            discovered.AddRange(DiscoverFile(inputPath, string.Empty));
            return discovered;
        }

        if (!Directory.Exists(inputPath))
        {
            return discovered;
        }

        var files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories)
            .Select(file => new
            {
                AbsolutePath = file,
                RelativePath = Path.GetRelativePath(inputPath, file).Replace('\\', '/'),
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var relativeDirectory = Path.GetDirectoryName(file.RelativePath)?.Replace('\\', '/') ?? string.Empty;
            discovered.AddRange(DiscoverFile(file.AbsolutePath, relativeDirectory));
        }

        return discovered;
    }

    private IReadOnlyList<DiscoveredItem> DiscoverFile(string absoluteFilePath, string relativeDirectory)
    {
        var items = new List<DiscoveredItem>();
        foreach (var node in _walker.Walk(absoluteFilePath))
        {
            var path = CombinePaths(relativeDirectory, node.Path);
            items.Add(new DiscoveredItem(path, node.Format.FormatName, node.Value));
        }

        return items;
    }

    private static string CombinePaths(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right.Replace('\\', '/');
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left.Replace('\\', '/');
        }

        return $"{left.TrimEnd('/', '\\')}/{right.TrimStart('/', '\\')}".Replace('\\', '/');
    }
}

public sealed record DiscoveredItem(string Path, string FormatName, object Value);
