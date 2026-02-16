using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PokemonGreen.Core.Battle;

namespace PokemonGreen.Core.Pokemon;

/// <summary>
/// Static registry of all Pokemon species data.
/// Loads from Data/species.json at startup.
/// </summary>
public static class SpeciesRegistry
{
    private static readonly Dictionary<int, SpeciesData> _species = new();
    private static bool _initialized;

    /// <summary>
    /// Load species data from a JSON file.
    /// Call once at startup (e.g. from Game1.Initialize).
    /// </summary>
    public static void Initialize(string? dataDirectory = null)
    {
        if (_initialized) return;

        string baseDir = dataDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        string jsonPath = Path.Combine(baseDir, "Data", "species.json");

        if (File.Exists(jsonPath))
        {
            var json = File.ReadAllText(jsonPath);
            var entries = JsonSerializer.Deserialize<List<SpeciesJsonEntry>>(json);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var data = new SpeciesData
                    {
                        SpeciesId = entry.id,
                        Name = entry.name,
                        BaseHP = entry.hp,
                        BaseAttack = entry.attack,
                        BaseDefense = entry.defense,
                        BaseSpAttack = entry.spAttack,
                        BaseSpDefense = entry.spDefense,
                        BaseSpeed = entry.speed,
                        Type1 = ParseType(entry.type1),
                        Type2 = entry.type2 != null ? ParseType(entry.type2) : ParseType(entry.type1),
                        BaseEXPYield = entry.baseExpYield,
                        GrowthRate = ParseGrowthRate(entry.growthRate),
                        CatchRate = entry.catchRate,
                        ModelFolder = $"pm{entry.id:D4}_00",
                    };
                    _species[data.SpeciesId] = data;
                }
            }
        }

        _initialized = true;
    }

    public static SpeciesData? GetSpecies(int speciesId)
    {
        if (!_initialized) Initialize();
        return _species.TryGetValue(speciesId, out var data) ? data : null;
    }

    public static IReadOnlyCollection<SpeciesData> GetAllSpecies()
    {
        if (!_initialized) Initialize();
        return _species.Values;
    }

    public static int Count
    {
        get
        {
            if (!_initialized) Initialize();
            return _species.Count;
        }
    }

    private static MoveType ParseType(string typeName)
    {
        if (Enum.TryParse<MoveType>(typeName, ignoreCase: true, out var result))
            return result;
        return MoveType.Normal;
    }

    private static GrowthRate ParseGrowthRate(string rateName)
    {
        if (Enum.TryParse<GrowthRate>(rateName, ignoreCase: true, out var result))
            return result;
        return GrowthRate.MediumFast;
    }

    // JSON deserialization shape
    private class SpeciesJsonEntry
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public int hp { get; set; }
        public int attack { get; set; }
        public int defense { get; set; }
        public int spAttack { get; set; }
        public int spDefense { get; set; }
        public int speed { get; set; }
        public string type1 { get; set; } = "Normal";
        public string? type2 { get; set; }
        public int baseExpYield { get; set; }
        public string growthRate { get; set; } = "MediumFast";
        public int catchRate { get; set; } = 45;
    }
}
