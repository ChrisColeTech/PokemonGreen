using PokemonGreen.SwitchToolboxCli.Core.Extraction;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

internal static class InfoCommand
{
    public static int Run(ArchiveWalker walker, string inputPath, TextWriter stdout, TextWriter stderr)
    {
        if (!File.Exists(inputPath))
        {
            stderr.WriteLine($"Input file not found: {inputPath}");
            return 2;
        }

        var items = walker.Walk(inputPath).ToList();
        if (items.Count == 0)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return 2;
        }

        stdout.WriteLine($"Input: {Path.GetFullPath(inputPath)}");
        stdout.WriteLine($"Detected items: {items.Count}");
        foreach (var entry in items.Take(50))
        {
            stdout.WriteLine($"- {entry.Path} [{entry.Format.FormatName}]");
        }

        if (items.Count > 50)
        {
            stdout.WriteLine($"... {items.Count - 50} more");
        }

        return 0;
    }
}
