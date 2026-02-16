public static class CliConventions
{
    public const int ExitSuccess = 0;
    public const int ExitFatal = 1;
    public const int ExitPartial = 2;

    public static bool TryNormalizeFormat(string? format, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        normalized = format.Trim().ToLowerInvariant();
        bool isSupported = normalized == "dae" || normalized == "obj";
        if (!isSupported)
        {
            normalized = string.Empty;
        }

        return isSupported;
    }

    public static int AggregateExitCode(int successCount, int partialCount, int fatalCount)
    {
        if (fatalCount > 0 && successCount == 0 && partialCount == 0)
        {
            return ExitFatal;
        }

        if (partialCount > 0 || fatalCount > 0)
        {
            return ExitPartial;
        }

        return ExitSuccess;
    }
}
