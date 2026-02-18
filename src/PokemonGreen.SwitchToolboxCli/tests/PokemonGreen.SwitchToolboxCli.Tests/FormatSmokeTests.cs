using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class FormatSmokeTests
{
    [Fact]
    public void DetectsRomFsByMagicAtStart()
    {
        var data = new byte[0x20];
        data[0] = (byte)'I';
        data[1] = (byte)'V';
        data[2] = (byte)'F';
        data[3] = (byte)'C';

        using var stream = new MemoryStream(data);

        var registry = new FormatRegistry();
        registry.Register(new RomFsFormat());

        var loader = new FileLoader(registry);
        var opened = loader.Open(stream, "romfs.bin");

        Assert.NotNull(opened);
        Assert.Equal("RomFS", opened?.Format.FormatName);
    }

    [Fact]
    public void DetectsNcchByHeaderOffset()
    {
        var data = new byte[0x200];
        data[0x100] = (byte)'N';
        data[0x101] = (byte)'C';
        data[0x102] = (byte)'C';
        data[0x103] = (byte)'H';

        using var stream = new MemoryStream(data);

        var registry = new FormatRegistry();
        registry.Register(new NcchFormat());

        var loader = new FileLoader(registry);
        var opened = loader.Open(stream, "test.cxi");

        Assert.NotNull(opened);
        Assert.Equal("NCCH", opened?.Format.FormatName);
    }

    [Fact]
    public void DetectsGarcByMagic()
    {
        var data = new byte[16];
        data[0] = (byte)'C';
        data[1] = (byte)'R';
        data[2] = (byte)'A';
        data[3] = (byte)'G';

        using var stream = new MemoryStream(data);

        var registry = new FormatRegistry();
        registry.Register(new GarcFormat());

        var loader = new FileLoader(registry);
        var opened = loader.Open(stream, "x.bin");

        Assert.NotNull(opened);
        Assert.Equal("GARC", opened?.Format.FormatName);
    }
}
