namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

public sealed class TrinityAnimationLocator
{
    private static readonly string[] DecodableExtensions =
    [
        ".tranm",
        ".gfbanm",
    ];

    private static readonly string[] EmbeddedSignatures =
    [
        "TRANM",
        "CHR0",
        "BFSKA",
        "FSKA",
        "ANIM",
    ];

    public bool TryLocate(string clipPath, ReadOnlySpan<byte> payload, out TrinityLocatedAnimation located)
    {
        located = null!;
        if (payload.Length == 0)
        {
            return false;
        }

        var extension = Path.GetExtension(clipPath);
        if (!DecodableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var signature in EmbeddedSignatures)
        {
            if (TryFindSignature(payload, signature, out var offset))
            {
                located = new TrinityLocatedAnimation(clipPath, payload[offset..].ToArray(), signature, offset);
                return true;
            }
        }

        located = new TrinityLocatedAnimation(clipPath, payload.ToArray(), "raw", 0);
        return true;
    }

    private static bool TryFindSignature(ReadOnlySpan<byte> payload, string signature, out int offset)
    {
        offset = -1;
        if (payload.Length < signature.Length)
        {
            return false;
        }

        for (var i = 0; i <= payload.Length - signature.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < signature.Length; j++)
            {
                if (payload[i + j] != (byte)signature[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                offset = i;
                return true;
            }
        }

        return false;
    }
}

public sealed record TrinityLocatedAnimation(
    string SourcePath,
    byte[] Payload,
    string Signature,
    int PayloadOffset);
