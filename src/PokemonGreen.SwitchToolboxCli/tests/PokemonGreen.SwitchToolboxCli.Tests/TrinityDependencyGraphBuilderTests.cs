using PokemonGreen.SwitchToolboxCli.Formats.Common;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrinityDependencyGraphBuilderTests
{
    [Fact]
    public void BuildHashCandidates_UsesDeterministicNormalizedPathVariants()
    {
        var builder = new TrinityDependencyGraphBuilder();

        var candidates = builder.BuildPathCandidates("\\Shared\\Assets\\Pikachu_Idle.tranm");
        Assert.Equal(
        [
            "Shared/Assets/Pikachu_Idle.tranm",
            "shared/assets/pikachu_idle.tranm",
        ],
            candidates);

        var hashes = builder.BuildHashCandidates("\\Shared\\Assets\\Pikachu_Idle.tranm");
        var expectedHashes = candidates
            .Select(Fnv1a64.Compute)
            .Distinct()
            .ToList();

        Assert.Equal(expectedHashes, hashes);
    }

    [Fact]
    public void ResolveArchiveEntryLinks_RemainsDeterministicAcrossInputOrdering()
    {
        var builder = new TrinityDependencyGraphBuilder();
        var dependencies = new List<TrinityDependencyReference>
        {
            new("shared/assets/pikachu_body.trmbf", ".trmbf"),
            new("shared/assets/pikachu_body.trmsh", ".trmsh"),
        };

        var availableEntries = new List<TrinityGraphArchiveEntry>
        {
            new("file_002.bin", Fnv1a64.Compute("shared/assets/pikachu_body.trmbf"), 30),
            new("shared/assets/pikachu_body.trmsh", null, 10),
            new("file_010.bin", Fnv1a64.Compute("shared/assets/unused.trmbf"), 40),
        };

        var ordered = builder.ResolveArchiveEntryLinks(
            dependencies,
            availableEntries,
            consumeMatches: true,
            allowGenericBinHashMatches: true);
        var reversed = builder.ResolveArchiveEntryLinks(
            dependencies,
            availableEntries.AsEnumerable().Reverse().ToList(),
            consumeMatches: true,
            allowGenericBinHashMatches: true);

        Assert.Equal(2, ordered.Count);
        Assert.Equal(2, reversed.Count);

        Assert.Equal(ordered.Select(item => item.DependencyPath), reversed.Select(item => item.DependencyPath));
        Assert.Equal(ordered.Select(item => item.ResolvedEntryPath), reversed.Select(item => item.ResolvedEntryPath));
        Assert.Equal("shared/assets/pikachu_body.trmbf", ordered[0].DependencyPath);
        Assert.Equal("file_002.bin", ordered[0].ResolvedEntryPath);
        Assert.Equal("hash", ordered[0].MatchKind);
        Assert.Equal("shared/assets/pikachu_body.trmsh", ordered[1].DependencyPath);
        Assert.Equal("shared/assets/pikachu_body.trmsh", ordered[1].ResolvedEntryPath);
        Assert.Equal("path_exact", ordered[1].MatchKind);
    }
}
