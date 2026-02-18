namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

public sealed class TextureSignatureDetector
{
    private static readonly Dictionary<string, string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = ".png",
        [".dds"] = ".dds",
        [".astc"] = ".astc",
        [".bmp"] = ".bmp",
        [".jpg"] = ".jpg",
        [".jpeg"] = ".jpeg",
        [".tif"] = ".tif",
        [".tiff"] = ".tiff",
    };

    public string? DetectExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 4 && bytes[0] == (byte)'D' && bytes[1] == (byte)'D' && bytes[2] == (byte)'S' && bytes[3] == (byte)' ')
        {
            return ".dds";
        }

        if (bytes.Length >= 4 && bytes[0] == 0x13 && bytes[1] == 0xAB && bytes[2] == 0xA1 && bytes[3] == 0x5C)
        {
            return ".astc";
        }

        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
        {
            return ".bmp";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 4)
        {
            var littleTiff = bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2A && bytes[3] == 0x00;
            var bigTiff = bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0x00 && bytes[3] == 0x2A;
            if (littleTiff || bigTiff)
            {
                return ".tif";
            }
        }

        return null;
    }

    public bool IsKnownTextureExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return KnownExtensions.ContainsKey(extension);
    }

    public string? NormalizeKnownExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        return KnownExtensions.TryGetValue(extension, out var normalized) ? normalized : null;
    }
}
