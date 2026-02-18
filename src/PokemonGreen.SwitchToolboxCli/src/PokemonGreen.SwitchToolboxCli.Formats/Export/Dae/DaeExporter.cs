using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Dae;

public sealed class DaeExporter
{
    public void Export(string filePath, ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(model);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(filePath);
        using var writer = new ColladaWriter(stream);
        writer.Write(model);
    }
}
