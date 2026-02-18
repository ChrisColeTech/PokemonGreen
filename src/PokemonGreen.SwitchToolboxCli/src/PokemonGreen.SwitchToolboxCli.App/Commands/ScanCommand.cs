using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Services;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class ScanCommand
{
    public static int Run(DiscoveryService discovery, string inputPath, string outputPath, TextWriter stdout, TextWriter stderr)
    {
        var items = discovery.Discover(inputPath)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.FormatName, StringComparer.Ordinal)
            .Select(item => new ScanReportItem(item.Path, item.FormatName))
            .ToList();

        if (items.Count == 0)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return 2;
        }

        var report = new ScanReport(Path.GetFullPath(inputPath), items);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDir = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        File.WriteAllText(fullOutputPath, json);
        stdout.WriteLine($"Scan complete. Wrote {items.Count} record(s): {fullOutputPath}");
        return 0;
    }
}

public sealed record ScanReport(string InputPath, IReadOnlyList<ScanReportItem> Items);
public sealed record ScanReportItem(string Path, string FormatName);
