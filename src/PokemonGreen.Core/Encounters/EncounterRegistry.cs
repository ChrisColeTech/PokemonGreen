using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PokemonGreen.Core.Encounters;

/// <summary>
/// Central service for loading, caching, and querying encounter data.
/// All encounter definitions are data-driven via JSON files.
/// </summary>
public static class EncounterRegistry
{
    private static readonly Dictionary<string, MapEncounterData> _cache = new();
    private static MapEncounterData? _currentMapEncounters;
    private static readonly Random _random = new();

    /// <summary>
    /// Load encounter data for a map. Called when the map becomes current.
    /// </summary>
    public static void LoadForMap(string mapId)
    {
        if (_cache.TryGetValue(mapId, out var cached))
        {
            _currentMapEncounters = cached;
            return;
        }

        string path = Path.Combine("Content", "Data", "Encounters", $"{mapId}.json");
        if (!File.Exists(path))
        {
            _currentMapEncounters = null;
            return;
        }

        var data = JsonSerializer.Deserialize<MapEncounterData>(File.ReadAllText(path));
        if (data != null)
        {
            _cache[mapId] = data;
            _currentMapEncounters = data;
        }
        else
        {
            _currentMapEncounters = null;
        }
    }

    /// <summary>
    /// Get the encounter table for the given encounter type on the current map.
    /// </summary>
    public static EncounterTable? GetTable(string encounterType)
    {
        if (_currentMapEncounters == null)
            return null;

        foreach (var group in _currentMapEncounters.EncounterGroups)
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
            _currentMapEncounters?.ProgressMultiplier ?? 0f);

        // 5. Build result
        return new WildEncounterResult(entry.SpeciesId, level, encounterType);
    }

    /// <summary>
    /// Clear the cache (e.g., when starting a new game).
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _currentMapEncounters = null;
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
