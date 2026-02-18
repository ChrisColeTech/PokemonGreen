using PokemonGreen.SwitchToolboxCli.Core.Abstractions;

namespace PokemonGreen.SwitchToolboxCli.Core.Loading;

public sealed class FormatRegistry
{
    private readonly List<IFileFormat> _formats = new();

    public IReadOnlyList<IFileFormat> Formats => _formats;

    public void Register(IFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        _formats.Add(format);
    }
}
