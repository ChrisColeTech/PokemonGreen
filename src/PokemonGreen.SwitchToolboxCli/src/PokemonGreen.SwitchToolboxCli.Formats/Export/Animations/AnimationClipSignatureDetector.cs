namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

public sealed class AnimationClipSignatureDetector
{
    private static readonly Dictionary<string, string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".gfbanm"] = ".gfbanm",
        [".bfska"] = ".bfska",
        [".seanim"] = ".seanim",
        [".smd"] = ".smd",
        [".anim"] = ".anim",
        [".chr0"] = ".chr0",
        [".tranm"] = ".tranm",
        [".traef"] = ".traef",
        [".tracm"] = ".tracm",
        [".tracs"] = ".tracs",
        [".tracl"] = ".tracl",
        [".tracr"] = ".tracr",
        [".tracp"] = ".tracp",
    };

    public bool TryGetClipExtension(string? fileName, ReadOnlySpan<byte> bytes, out string extension)
    {
        extension = string.Empty;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var sourceExtension = Path.GetExtension(fileName);
            if (KnownExtensions.TryGetValue(sourceExtension, out var normalizedKnown))
            {
                extension = normalizedKnown;
                return true;
            }

            if (sourceExtension.Equals(".json", StringComparison.OrdinalIgnoreCase) && IsLikelyAnimationJson(bytes))
            {
                extension = ".json";
                return true;
            }
        }

        var detected = DetectFromSignature(bytes);
        if (detected is not null)
        {
            extension = detected;
            return true;
        }

        return false;
    }

    public string? DetectFromSignature(ReadOnlySpan<byte> bytes)
    {
        if (StartsWithAscii(bytes, "CHR0"))
        {
            return ".chr0";
        }

        if (StartsWithAscii(bytes, "BFSKA") || StartsWithAscii(bytes, "FSKA"))
        {
            return ".bfska";
        }

        if (StartsWithAscii(bytes, "SEAnim") || StartsWithAscii(bytes, "SEANIM"))
        {
            return ".seanim";
        }

        if (LooksLikeSmd(bytes))
        {
            return ".smd";
        }

        if (StartsWithAscii(bytes, "ANIM"))
        {
            return ".anim";
        }

        if (IsLikelyAnimationJson(bytes))
        {
            return ".json";
        }

        return null;
    }

    private static bool LooksLikeSmd(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        var sampleLength = Math.Min(bytes.Length, 512);
        var sample = bytes[..sampleLength];
        if (sample.Contains((byte)0))
        {
            return false;
        }

        var text = System.Text.Encoding.UTF8.GetString(sample).Replace("\r", string.Empty);
        return text.StartsWith("version ", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("\nnodes\n", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("\nskeleton\n", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyAnimationJson(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        var sampleLength = Math.Min(bytes.Length, 4096);
        var sample = bytes[..sampleLength];
        if (sample.Contains((byte)0))
        {
            return false;
        }

        var text = System.Text.Encoding.UTF8.GetString(sample).TrimStart();
        if (text.Length == 0)
        {
            return false;
        }

        var startsLikeJson = text[0] == '{' || text[0] == '[';
        if (!startsLikeJson)
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        var hits = 0;
        if (lower.Contains("\"animation\"", StringComparison.Ordinal) || lower.Contains("\"animations\"", StringComparison.Ordinal)) hits++;
        if (lower.Contains("\"track\"", StringComparison.Ordinal) || lower.Contains("\"tracks\"", StringComparison.Ordinal)) hits++;
        if (lower.Contains("\"bone\"", StringComparison.Ordinal) || lower.Contains("\"bones\"", StringComparison.Ordinal)) hits++;
        if (lower.Contains("\"keyframe\"", StringComparison.Ordinal) || lower.Contains("\"keyframes\"", StringComparison.Ordinal)) hits++;
        if (lower.Contains("\"skeleton\"", StringComparison.Ordinal)) hits++;
        if (lower.Contains("\"frame\"", StringComparison.Ordinal) || lower.Contains("\"frames\"", StringComparison.Ordinal)) hits++;

        return hits >= 2;
    }

    private static bool StartsWithAscii(ReadOnlySpan<byte> bytes, string ascii)
    {
        if (bytes.Length < ascii.Length)
        {
            return false;
        }

        for (var i = 0; i < ascii.Length; i++)
        {
            if (bytes[i] != (byte)ascii[i])
            {
                return false;
            }
        }

        return true;
    }
}
