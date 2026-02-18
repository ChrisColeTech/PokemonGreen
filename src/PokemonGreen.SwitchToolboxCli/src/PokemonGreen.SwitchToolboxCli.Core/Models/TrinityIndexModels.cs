using PokemonGreen.SwitchToolboxCli.Core.Abstractions;

namespace PokemonGreen.SwitchToolboxCli.Core.Models;

public enum TrinityEntryRole
{
    DirectModel,
    ModelContainer,
    Model,
    Mesh,
    Skeleton,
    Material,
    BlendShape,
    Texture,
    AnimationClip,
    EffectClip,
}

public sealed record TrinityBundleEntry(
    string EntryPath,
    string Extension,
    TrinityEntryRole Role,
    bool IsSniffed = false,
    string? DetailMessage = null);

public sealed record TrinityClipReference(string EntryPath, TrinityEntryRole Role);

public sealed record TrinityIndexedBundle(
    string SourceArchivePath,
    string LogicalModelKey,
    IReadOnlyList<TrinityBundleEntry> Entries,
    IReadOnlyList<TrinityClipReference> ClipReferences);

public sealed record TrinityArchiveSource(string ArchivePath, IArchiveFile Archive);
