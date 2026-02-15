using System.Collections.Generic;
using System.Text.Json;

namespace PokemonGreen.Core.NPCs;

public static class NPCRegistry
{
    private static Dictionary<int, NPCDefinition>? _npcs;
    private static readonly object _lock = new();
    
    public static void Initialize(string jsonData)
    {
        lock (_lock)
        {
            var npcs = JsonSerializer.Deserialize<NPCData[]>(jsonData);
            _npcs = new Dictionary<int, NPCDefinition>();
            
            if (npcs != null)
            {
                foreach (var data in npcs)
                {
                    var role = ParseRole(data.Role);
                    var definition = new NPCDefinition(
                        data.Id,
                        data.Name,
                        data.Sprite,
                        role,
                        data.FrameWidth,
                        data.FrameHeight,
                        data.FramesPerDirection,
                        data.Animations
                    );
                    _npcs[data.Id] = definition;
                }
            }
        }
    }
    
    private static void EnsureInitialized()
    {
        if (_npcs == null)
        {
            var json = PokemonGreen.Assets.AssetLoader.LoadDataJson("npcs");
            if (json != null)
                Initialize(json);
            else
                _npcs = new Dictionary<int, NPCDefinition>();
        }
    }
    
    private static NPCRole ParseRole(string role) => role switch
    {
        "Civilian" => NPCRole.Civilian,
        "Service" => NPCRole.Service,
        "Trainer" => NPCRole.Trainer,
        "Story" => NPCRole.Story,
        "Gym" => NPCRole.Gym,
        "Elite" => NPCRole.Elite,
        "Villain" => NPCRole.Villain,
        "Specialist" => NPCRole.Specialist,
        _ => NPCRole.Civilian
    };
    
    public static NPCDefinition? GetNPC(int id)
    {
        EnsureInitialized();
        return _npcs!.TryGetValue(id, out var npc) ? npc : null;
    }

    public static IEnumerable<NPCDefinition> GetNPCsByRole(NPCRole role)
    {
        EnsureInitialized();
        return _npcs!.Values.Where(n => n.Role == role);
    }

    public static IEnumerable<NPCDefinition> AllNPCs
    {
        get
        {
            EnsureInitialized();
            return _npcs!.Values;
        }
    }
}
