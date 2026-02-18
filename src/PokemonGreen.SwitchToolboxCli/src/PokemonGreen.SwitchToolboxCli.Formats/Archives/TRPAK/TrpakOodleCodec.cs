using System.Runtime.InteropServices;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPAK;

internal static class TrpakOodleCodec
{
    private const string DefaultDllName = "oo2core_6_win64.dll";
    private static readonly object Sync = new();
    private static bool _initialized;
    private static bool _isAvailable;
    private static string _availabilityDetail = "not initialized";
    private static nint _libraryHandle;
    private static OodleLzDecompressDelegate? _decompress;

    public static bool TryDecompress(byte[] compressedPayload, int expectedSize, out byte[] decompressedPayload, out string detail)
    {
        decompressedPayload = Array.Empty<byte>();
        if (compressedPayload.Length == 0)
        {
            detail = "compressed payload is empty.";
            return false;
        }

        if (expectedSize <= 0)
        {
            detail = "missing decompressed size metadata in TRPAK entry.";
            return false;
        }

        EnsureInitialized();
        if (!_isAvailable || _decompress is null)
        {
            detail = _availabilityDetail;
            return false;
        }

        var output = new byte[expectedSize];
        try
        {
            unsafe
            {
                fixed (byte* compressed = compressedPayload)
                fixed (byte* raw = output)
                {
                    var result = _decompress(
                        compressed,
                        compressedPayload.LongLength,
                        raw,
                        output.LongLength,
                        1,
                        0,
                        0,
                        null,
                        0,
                        nint.Zero,
                        nint.Zero,
                        nint.Zero,
                        0,
                        3);

                    if (result <= 0)
                    {
                        detail = $"OodleLZ_Decompress returned {result}.";
                        return false;
                    }

                    if (result != output.LongLength)
                    {
                        Array.Resize(ref output, (int)result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            detail = $"oodle decode exception: {ex.Message}";
            return false;
        }

        decompressedPayload = output;
        detail = $"decompressed_size={decompressedPayload.Length}; dll={GetResolvedDllPath()}";
        return true;
    }

    private static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var dllPath = GetResolvedDllPath();
            if (!File.Exists(dllPath))
            {
                _availabilityDetail = $"oo2core DLL unavailable at '{dllPath}' (set PG_OO2CORE_PATH to override).";
                return;
            }

            if (!NativeLibrary.TryLoad(dllPath, out _libraryHandle))
            {
                _availabilityDetail = $"failed to load oo2core DLL '{dllPath}'.";
                return;
            }

            if (!NativeLibrary.TryGetExport(_libraryHandle, "OodleLZ_Decompress", out var export))
            {
                _availabilityDetail = $"OodleLZ_Decompress export not found in '{dllPath}'.";
                NativeLibrary.Free(_libraryHandle);
                _libraryHandle = 0;
                return;
            }

            _decompress = Marshal.GetDelegateForFunctionPointer<OodleLzDecompressDelegate>(export);
            _isAvailable = true;
            _availabilityDetail = $"loaded oo2core DLL '{dllPath}'.";
        }
    }

    private static string GetResolvedDllPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("PG_OO2CORE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(AppContext.BaseDirectory, DefaultDllName);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate long OodleLzDecompressDelegate(
        byte* compBuf,
        long compBufSize,
        byte* rawBuf,
        long rawLen,
        int fuzzSafe,
        int checkCrc,
        int verbosity,
        byte* decBufBase,
        long decBufSize,
        nint fpCallback,
        nint callbackUserData,
        nint decoderMemory,
        long decoderMemorySize,
        int threadPhase);
}
