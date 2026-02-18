using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Core.Serialization;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

public sealed class ManifestFileWriter
{
    private readonly DeterministicJsonFileWriter _jsonWriter;

    public ManifestFileWriter()
        : this(new DeterministicJsonFileWriter())
    {
    }

    public ManifestFileWriter(DeterministicJsonFileWriter jsonWriter)
    {
        _jsonWriter = jsonWriter;
    }

    public void WriteConvertManifest(string outputPath, ConvertManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);
        _jsonWriter.Write(outputPath, manifest);
    }

    public void WriteBatchManifest(string outputPath, BatchManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);
        _jsonWriter.Write(outputPath, manifest);
    }
}
