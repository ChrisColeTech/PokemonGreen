namespace PokemonGreen.Core.Maps;

public static class TileSpriteMapping
{
    private static readonly Dictionary<int, string> _npcSprites = new()
    {
        // Entity NPCs (48-55)
        [48] = "npc_youngster",
        [49] = "npc_nurse",
        [50] = "npc_nurse",
        [51] = "npc_youngster",
        [52] = "npc_hiker",
        [53] = "npc_youngster",
        [54] = "npc_youngster",
        [55] = "npc_nurse",
        
        // Trainers (56-71)
        [56] = "npc_youngster",
        [57] = "npc_youngster",
        [58] = "npc_youngster",
        [59] = "npc_youngster",
        [60] = "npc_hiker",
        [61] = "npc_hiker",
        [62] = "npc_youngster",
        [63] = "npc_youngster",
        [64] = "npc_youngster",
        [65] = "npc_youngster",
        [66] = "npc_hiker",
        [67] = "npc_youngster",
        [68] = "npc_youngster",
        [69] = "npc_hiker",
        [70] = "npc_youngster",
        [71] = "npc_youngster",
    };
    
    private static readonly Dictionary<int, string> _itemSprites = new()
    {
        // Items (96-103)
        [96] = "pokeball",
        [97] = "potion",
        [98] = "fullrestore",
        [99] = "fruit_apple",
        [100] = "megastone_blue",
        [101] = "megastone_red",
        [102] = "megaring",
        [103] = "berry_red",
    };
    
    public static string? GetNPCSprite(int tileId) =>
        _npcSprites.TryGetValue(tileId, out var name) ? name : null;
    
    public static string? GetItemSprite(int tileId) =>
        _itemSprites.TryGetValue(tileId, out var name) ? name : null;
    
    public static bool IsNPCTile(int tileId) => tileId >= 48 && tileId <= 71;
    public static bool IsItemTile(int tileId) => tileId >= 96 && tileId <= 103;
}
