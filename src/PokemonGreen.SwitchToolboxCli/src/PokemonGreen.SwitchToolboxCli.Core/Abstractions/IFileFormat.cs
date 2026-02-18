namespace PokemonGreen.SwitchToolboxCli.Core.Abstractions;

public interface IFileFormat
{
    string FormatName { get; }
    IReadOnlyList<string> Extensions { get; }

    bool Identify(Stream stream, string? fileName);
    object Load(Stream stream, string? fileName);
}
