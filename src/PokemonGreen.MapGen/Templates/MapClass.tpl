#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class {{CLASS_NAME}} : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
{{BASE_TILE_DATA}}
    ];

    private static readonly int?[] OverlayTileData =
    [
{{OVERLAY_TILE_DATA}}
    ];

    private static readonly int[] WalkableTileIds =
    [
{{WALKABLE_TILE_IDS}}
    ];
{{WARP_DATA}}{{CONNECTION_DATA}}
    public static {{CLASS_NAME}} Instance { get; } = new();

    private {{CLASS_NAME}}()
        : base("{{MAP_ID}}", "{{DISPLAY_NAME}}", {{WIDTH}}, {{HEIGHT}}, {{TILE_SIZE}}, BaseTileData, OverlayTileData, WalkableTileIds{{EXTRA_CTOR_ARGS}})
    {
    }
}
