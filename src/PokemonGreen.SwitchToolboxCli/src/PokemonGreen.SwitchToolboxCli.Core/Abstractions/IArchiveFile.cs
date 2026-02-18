namespace PokemonGreen.SwitchToolboxCli.Core.Abstractions;

public interface IArchiveFile
{
    IEnumerable<ArchiveEntry> Files { get; }
}

public sealed class ArchiveEntry
{
    public required string FileName { get; init; }
    public required Func<Stream> OpenRead { get; init; }
    public string? DetailMessage { get; init; }
    public ulong? RootHash { get; init; }
    public string? ExtensionHint { get; init; }
}
