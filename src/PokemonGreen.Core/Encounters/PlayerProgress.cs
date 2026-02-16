using System.Collections.Generic;
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Encounters;

/// <summary>
/// Tracks player progress state consumed by the encounter system for gating and level scaling.
/// </summary>
public class PlayerProgress
{
    public int BadgeCount { get; set; }
    public int HighestPartyLevel { get; set; }
    public HashSet<string> StoryFlags { get; } = new();

    /// <summary>
    /// Update party-derived fields from the current party.
    /// </summary>
    public void UpdateFromParty(Party party)
    {
        if (party.Count == 0) return;

        int max = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].Level > max)
                max = party[i].Level;
        }
        HighestPartyLevel = max;
    }
}
