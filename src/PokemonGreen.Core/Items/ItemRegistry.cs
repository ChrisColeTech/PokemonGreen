using System.Collections.Generic;
using PokemonGreen.Core.Items;

namespace PokemonGreen.Core.Items;

public static class ItemRegistry
{
    private static readonly Dictionary<int, ItemDefinition> _items = new()
    {
        // Pokeballs (0-99)
        [0] = new ItemDefinition(0, "Poke Ball", "pokeball", ItemCategory.Pokeball, 200, 100, false, false, "catch_rate_1.0"),
        [1] = new ItemDefinition(1, "Great Ball", "greatball", ItemCategory.Pokeball, 600, 300, false, false, "catch_rate_1.5"),
        [2] = new ItemDefinition(2, "Ultra Ball", "ultraball", ItemCategory.Pokeball, 1200, 600, false, false, "catch_rate_2.0"),
        [3] = new ItemDefinition(3, "Master Ball", "masterball", ItemCategory.Pokeball, 0, 0, false, false, "catch_rate_infinite"),

        // Medicine (100-199)
        [100] = new ItemDefinition(100, "Potion", "potion", ItemCategory.Medicine, 300, 150, true, true, "heal_hp_20"),
        [101] = new ItemDefinition(101, "Super Potion", "superpotion", ItemCategory.Medicine, 700, 350, true, true, "heal_hp_50"),
        [102] = new ItemDefinition(102, "Hyper Potion", "hyperpotion", ItemCategory.Medicine, 1200, 600, true, true, "heal_hp_200"),
        [103] = new ItemDefinition(103, "Max Potion", "maxpotion", ItemCategory.Medicine, 2500, 1250, true, true, "heal_hp_full"),
        [104] = new ItemDefinition(104, "Full Restore", "fullrestore", ItemCategory.Medicine, 3000, 1500, true, true, "heal_full_status"),

        // Continue with more as needed...
    };

    public static ItemDefinition? GetItem(int id) => _items.TryGetValue(id, out var item) ? item : null;

    public static IEnumerable<ItemDefinition> GetItemsByCategory(ItemCategory category) => 
        _items.Values.Where(i => i.Category == category);

    public static IEnumerable<ItemDefinition> AllItems => _items.Values;
}
