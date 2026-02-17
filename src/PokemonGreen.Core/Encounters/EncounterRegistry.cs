using System;
using System.Collections.Generic;
using System.Linq;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Encounters;

/// <summary>
/// Central service for querying encounter data from map definitions.
/// Encounter tables are baked into generated MapDefinition subclasses.
/// </summary>
public static class EncounterRegistry
{
    private static EncounterTable[] _currentEncounterGroups = [];
    private static float _currentProgressMultiplier;
    private static readonly Random _random = new();

    /// <summary>
    /// Load encounter data from a map definition. Called when the map becomes current.
    /// </summary>
    public static void LoadForMap(MapDefinition mapDef)
    {
        _currentEncounterGroups = mapDef.EncounterGroups.ToArray();
        _currentProgressMultiplier = mapDef.ProgressMultiplier;
    }

    /// <summary>
    /// Get the encounter table for the given encounter type on the current map.
    /// </summary>
    public static EncounterTable? GetTable(string encounterType)
    {
        foreach (var group in _currentEncounterGroups)
        {
            if (group.EncounterType == encounterType)
                return group;
        }
        return null;
    }

    /// <summary>
    /// Roll for a wild encounter on the current tile.
    /// Returns null if no encounter should happen.
    /// </summary>
    public static WildEncounterResult? TryEncounter(string encounterType, PlayerProgress progress)
    {
        var table = GetTable(encounterType);
        if (table == null)
            return null;

        // 1. Rate check
        float rate = table.BaseEncounterRate / 255f;
        if (_random.NextDouble() >= rate)
            return null;

        // 2. Filter entries by progress
        var available = FilterByProgress(table.Entries, progress);
        if (available.Length == 0)
            return null;

        // 3. Weighted random selection
        var entry = WeightedSelect(available);
        if (entry == null)
            return null;

        // 4. Calculate level
        int level = CalculateEncounterLevel(
            entry.MinLevel,
            entry.MaxLevel,
            progress.HighestPartyLevel,
            progress.BadgeCount,
            _currentProgressMultiplier);

        // 5. Build result
        return new WildEncounterResult(entry.SpeciesId, level, encounterType);
    }

    /// <summary>
    /// Clear loaded encounter data (e.g., when starting a new game).
    /// </summary>
    public static void ClearCache()
    {
        _currentEncounterGroups = [];
        _currentProgressMultiplier = 0f;
    }

    private static EncounterEntry[] FilterByProgress(EncounterEntry[] entries, PlayerProgress progress)
    {
        var result = new List<EncounterEntry>();
        foreach (var entry in entries)
        {
            if (progress.BadgeCount < entry.RequiredBadges)
                continue;

            if (entry.RequiredFlags != null)
            {
                bool allFlags = true;
                foreach (string flag in entry.RequiredFlags)
                {
                    if (!progress.StoryFlags.Contains(flag))
                    {
                        allFlags = false;
                        break;
                    }
                }
                if (!allFlags) continue;
            }

            result.Add(entry);
        }
        return result.ToArray();
    }

    private static EncounterEntry? WeightedSelect(EncounterEntry[] entries)
    {
        if (entries.Length == 0) return null;

        int totalWeight = 0;
        foreach (var entry in entries)
            totalWeight += entry.Weight;

        if (totalWeight <= 0) return null;

        int roll = _random.Next(totalWeight);
        int cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry;
        }

        return entries[^1];
    }

    private static int CalculateEncounterLevel(
        int minLevel, int maxLevel,
        int partyHighestLevel, int badgeCount,
        float progressMultiplier)
    {
        // Base level: random within authored range
        int baseLevel = _random.Next(minLevel, maxLevel + 1);

        if (progressMultiplier <= 0f)
            return baseLevel;

        // Badge scaling: each badge allows +2 levels above authored max
        int badgeBonus = badgeCount * 2;

        // Party-relative scaling
        int scaledLevel = baseLevel;
        if (partyHighestLevel > maxLevel)
        {
            int partyDelta = partyHighestLevel - maxLevel;
            int adjustment = (int)(partyDelta * progressMultiplier * 0.5f);
            scaledLevel = baseLevel + adjustment;
        }

        int ceiling = maxLevel + badgeBonus;
        return Math.Clamp(scaledLevel, minLevel, ceiling);
    }
}
