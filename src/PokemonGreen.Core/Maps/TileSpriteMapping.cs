using PokemonGreen.Core.Items;
using PokemonGreen.Core.NPCs;

namespace PokemonGreen.Core.Maps;

public static class TileSpriteMapping
{
    public static string? GetSpriteName(int tileId)
    {
        var tile = TileRegistry.GetTile(tileId);
        if (tile == null || tile.EntityId == null)
            return null;

        if (IsNPCTile(tileId))
        {
            var npc = NPCRegistry.GetNPC(tile.EntityId.Value);
            return npc?.SpriteName;
        }
        
        if (IsItemTile(tileId))
        {
            var item = ItemRegistry.GetItem(tile.EntityId.Value);
            return item?.SpriteName;
        }

        return null;
    }

    public static int? GetEntityId(int tileId)
    {
        var tile = TileRegistry.GetTile(tileId);
        return tile?.EntityId;
    }

    public static bool IsNPCTile(int tileId) => tileId >= 48 && tileId <= 71;
    
    public static bool IsItemTile(int tileId) => tileId >= 96 && tileId <= 111;
}
