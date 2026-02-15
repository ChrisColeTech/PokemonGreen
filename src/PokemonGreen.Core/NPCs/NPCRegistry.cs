using System.Collections.Generic;

namespace PokemonGreen.Core.NPCs;

public static class NPCRegistry
{
    private static readonly Dictionary<int, NPCDefinition> _npcs = new()
    {
        // Trainers (0-99)
        [0] = new NPCDefinition(0, "Youngster", "npc_youngster", NPCRole.Trainer, 64, 64, 4,
            new[] { "walk", "idle", "run" }),
        [1] = new NPCDefinition(1, "Hiker", "npc_hiker", NPCRole.Trainer, 64, 64, 4,
            new[] { "walk", "idle", "run" }),

        // Service (100-199)
        [100] = new NPCDefinition(100, "Nurse", "npc_nurse", NPCRole.Service, 64, 64, 4,
            new[] { "walk", "idle", "run", "emote" }),
    };

    public static NPCDefinition? GetNPC(int id) => _npcs.TryGetValue(id, out var npc) ? npc : null;

    public static IEnumerable<NPCDefinition> GetNPCsByRole(NPCRole role) =>
        _npcs.Values.Where(n => n.Role == role);

    public static IEnumerable<NPCDefinition> AllNPCs => _npcs.Values;
}
