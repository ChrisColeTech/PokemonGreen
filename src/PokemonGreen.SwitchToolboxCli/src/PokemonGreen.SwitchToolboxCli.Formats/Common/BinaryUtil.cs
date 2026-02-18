using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Formats.Common;

internal static class BinaryUtil
{
    public static bool MatchAscii(Stream stream, long offset, string signature)
    {
        if (!stream.CanSeek || stream.Length < offset + signature.Length)
        {
            return false;
        }

        var start = stream.Position;
        try
        {
            stream.Position = offset;
            Span<byte> buffer = stackalloc byte[64];
            var sigBytes = Encoding.ASCII.GetBytes(signature);
            if (sigBytes.Length > buffer.Length)
            {
                return false;
            }

            var read = stream.Read(buffer[..sigBytes.Length]);
            if (read != sigBytes.Length)
            {
                return false;
            }

            for (var i = 0; i < sigBytes.Length; i++)
            {
                if (buffer[i] != sigBytes[i])
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            stream.Position = start;
        }
    }
}
