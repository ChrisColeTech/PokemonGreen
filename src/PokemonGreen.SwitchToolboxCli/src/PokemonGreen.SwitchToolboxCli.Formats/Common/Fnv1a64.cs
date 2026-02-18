using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Formats.Common;

public static class Fnv1a64
{
    private const ulong OffsetBasis = 14695981039346656837;
    private const ulong Prime = 1099511628211;

    public static ulong Compute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var hash = OffsetBasis;
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }
}
