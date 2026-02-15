using System.Collections.Generic;
using System.Linq;

namespace PokemonGreen.Core.Items;

/// <summary>
/// Player's bag, organized by pouch (category).
/// </summary>
public class PlayerInventory
{
    private readonly Dictionary<ItemCategory, List<InventorySlot>> _pouches = new();

    public IReadOnlyList<InventorySlot> GetPouch(ItemCategory category)
    {
        return _pouches.TryGetValue(category, out var pouch)
            ? pouch
            : (IReadOnlyList<InventorySlot>)[];
    }

    public IReadOnlyList<InventorySlot> GetPouch(ItemCategory[] categories)
    {
        var result = new List<InventorySlot>();
        foreach (var cat in categories)
        {
            if (_pouches.TryGetValue(cat, out var pouch))
                result.AddRange(pouch);
        }
        return result;
    }

    public void AddItem(int itemId, int quantity = 1)
    {
        var def = ItemRegistry.GetItem(itemId);
        if (def == null) return;

        if (!_pouches.TryGetValue(def.Category, out var pouch))
        {
            pouch = new List<InventorySlot>();
            _pouches[def.Category] = pouch;
        }

        var existing = pouch.FirstOrDefault(s => s.ItemId == itemId);
        if (existing != null)
            existing.Quantity += quantity;
        else
            pouch.Add(new InventorySlot(itemId, quantity));
    }

    /// <summary>Create an inventory with test items for development.</summary>
    public static PlayerInventory CreateTestInventory()
    {
        var inv = new PlayerInventory();
        // Medicine
        inv.AddItem(1, 5);   // Potion
        inv.AddItem(2, 2);   // Super Potion
        inv.AddItem(3, 1);   // Antidote
        // Pokeballs
        inv.AddItem(10, 10); // Poke Ball
        inv.AddItem(11, 3);  // Great Ball
        // Battle items
        inv.AddItem(20, 2);  // X Attack
        // Berries
        inv.AddItem(30, 5);  // Oran Berry
        inv.AddItem(31, 3);  // Sitrus Berry
        return inv;
    }
}
