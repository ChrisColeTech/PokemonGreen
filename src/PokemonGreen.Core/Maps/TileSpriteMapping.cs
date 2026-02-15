namespace PokemonGreen.Core.Maps;

public static class TileSpriteMapping
{
    private static readonly Dictionary<int, string> _npcSprites = new()
    {
        // Entity NPCs (48-55)
        [48] = "pokemon_civilian_villager_a",
        [49] = "pokemon_specialist_clerk_a",
        [50] = "pokemon_specialist_nurse_a",
        [51] = "pokemon_shop_clerk",
        [52] = "pokemon_story_gym_leader_a",
        [53] = "pokemon_story_rival_a",
        [54] = "pokemon_story_professor_a",
        [55] = "pokemon_story_mom_a",
        
        // Trainers (56-71)
        [56] = "pokemon_trainer_youngster_a",
        [57] = "pokemon_trainer_youngster_a",
        [58] = "pokemon_trainer_youngster_a",
        [59] = "pokemon_trainer_youngster_a",
        [60] = "pokemon_trainer_hiker_a",
        [61] = "pokemon_story_gym_leader_a",
        [62] = "pokemon_story_elite_trainer_a",
        [63] = "pokemon_story_champion_a",
        [64] = "pokemon_story_villain_grunt_a",
        [65] = "pokemon_story_villain_grunt_a",
        [66] = "pokemon_story_villain_grunt_a",
        [67] = "pokemon_story_villain_grunt_a",
        [68] = "pokemon_story_villain_grunt_a",
        [69] = "pokemon_story_villain_grunt_a",
        [70] = "pokemon_story_villain_grunt_a",
        [71] = "pokemon_story_villain_grunt_a",
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
        
        // Additional item tiles (104-111) - extend as needed
        [104] = "greatball",
        [105] = "ultraball",
        [106] = "masterball",
        [107] = "superpotion",
        [108] = "hyperpotion",
        [109] = "maxpotion",
        [110] = "fullheal",
        [111] = "stone_fire",
    };
    
    public static string? GetNPCSprite(int tileId) =>
        _npcSprites.TryGetValue(tileId, out var name) ? name : null;
    
    public static string? GetItemSprite(int tileId) =>
        _itemSprites.TryGetValue(tileId, out var name) ? name : null;
    
    public static bool IsNPCTile(int tileId) => tileId >= 48 && tileId <= 71;
    public static bool IsItemTile(int tileId) => tileId >= 96 && tileId <= 111;
}
