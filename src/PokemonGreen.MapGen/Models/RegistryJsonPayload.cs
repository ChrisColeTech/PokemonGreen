using System.Text.Json.Serialization;

namespace PokemonGreen.MapGen.Models;

public sealed class RegistryJsonPayload
{
    public required RegistryMetadataPayload Metadata { get; init; }
    public required IReadOnlyList<RegistryCategoryPayload> Categories { get; init; }
    public required IReadOnlyList<RegistryTilePayload> Tiles { get; init; }
    public required IReadOnlyList<RegistryBuildingPayload> Buildings { get; init; }
}

public sealed class RegistryMetadataPayload
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
}

public sealed class RegistryCategoryPayload
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required bool ShowInPalette { get; init; }
}

public sealed class RegistryTilePayload
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required bool Walkable { get; init; }
    public required string Category { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsOverlay { get; init; }
}

public sealed class RegistryBuildingPayload
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int?[][] Tiles { get; init; }
}
