using System.Collections.Generic;

namespace PokemonGreen.Core.Encounters;

/// <summary>
/// Maps tile overlay behavior strings to encounter type strings.
/// </summary>
public static class EncounterTypeResolver
{
    private static readonly Dictionary<string, string> _map = new()
    {
        ["wild_encounter"] = "tall_grass",
        ["rare_encounter"] = "rare_grass",
        ["double_encounter"] = "dark_grass",
        ["cave_encounter"] = "cave_floor",
        ["water_encounter"] = "water_surface",
        ["surf_encounter"] = "surf",
        ["fishing"] = "fishing",
        ["headbutt"] = "headbutt",
        ["fire_encounter"] = "fire_terrain",
    };

    public static string? FromOverlayBehavior(string? behavior)
    {
        if (behavior == null) return null;
        return _map.TryGetValue(behavior, out var type) ? type : null;
    }
}
