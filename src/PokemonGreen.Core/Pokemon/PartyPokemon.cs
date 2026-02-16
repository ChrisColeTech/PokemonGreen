using System;

namespace PokemonGreen.Core.Pokemon;

public enum Gender : byte { Male, Female, Unknown }

/// <summary>
/// A Pokemon in the player's party with full stats, EXP tracking, and level-up support.
/// </summary>
public class PartyPokemon
{
    public string Nickname { get; set; } = "MissingNo";
    public int SpeciesId { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentHP { get; set; } = 10;
    public int MaxHP { get; set; } = 10;
    public Gender Gender { get; set; } = Gender.Unknown;
    public string? Status { get; set; }
    public int? HeldItemId { get; set; }

    // Stats
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }

    // EXP
    public uint ExperiencePoints { get; set; }
    public GrowthRate GrowthRate { get; set; } = GrowthRate.MediumFast;

    // IVs and EVs: [HP, Atk, Def, SpA, SpD, Spe]
    public int[] IVs { get; set; } = new int[6];
    public int[] EVs { get; set; } = new int[6];

    // Moves (up to 4)
    public int[] MoveIds { get; set; } = Array.Empty<int>();
    public int[] MovePPs { get; set; } = Array.Empty<int>();

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;

    /// <summary>
    /// Species name from registry, or nickname.
    /// </summary>
    public string SpeciesName => SpeciesRegistry.GetSpecies(SpeciesId)?.Name ?? Nickname;

    /// <summary>
    /// EXP bar fill percentage (0..1) within the current level.
    /// </summary>
    public float EXPPercent => GrowthRateHelper.GetEXPPercent(ExperiencePoints, Level, GrowthRate);

    /// <summary>
    /// Add EXP and handle level-ups. Returns the number of levels gained.
    /// </summary>
    public int AddEXP(uint amount)
    {
        if (Level >= GrowthRateHelper.MaxLevel) return 0;

        ExperiencePoints += amount;
        int levelsGained = 0;

        while (Level < GrowthRateHelper.MaxLevel)
        {
            uint expNeeded = GrowthRateHelper.GetEXPForLevel(GrowthRate, Level + 1);
            if (ExperiencePoints < expNeeded) break;

            Level++;
            levelsGained++;
            RecalculateStats();
        }

        // Cap EXP at max level threshold
        if (Level >= GrowthRateHelper.MaxLevel)
        {
            ExperiencePoints = GrowthRateHelper.GetEXPForLevel(GrowthRate, GrowthRateHelper.MaxLevel);
        }

        return levelsGained;
    }

    /// <summary>
    /// Recalculate all stats from base stats, IVs, EVs, and level.
    /// Adjusts current HP proportionally (adds the MaxHP difference).
    /// </summary>
    public void RecalculateStats()
    {
        var species = SpeciesRegistry.GetSpecies(SpeciesId);
        if (species == null) return;

        int oldMaxHP = MaxHP;
        var stats = StatCalculator.CalculateAll(species, Level, IVs, EVs);
        MaxHP = stats.hp;
        Attack = stats.atk;
        Defense = stats.def;
        SpAttack = stats.spAtk;
        SpDefense = stats.spDef;
        Speed = stats.speed;

        // On level-up, add HP difference so damage taken stays the same
        if (CurrentHP > 0 && MaxHP != oldMaxHP)
        {
            CurrentHP = Math.Max(1, CurrentHP + (MaxHP - oldMaxHP));
        }
    }

    /// <summary>
    /// Create a PartyPokemon from species data with random IVs and calculated stats.
    /// </summary>
    public static PartyPokemon Create(int speciesId, int level, Gender gender, Random? rng = null)
    {
        var species = SpeciesRegistry.GetSpecies(speciesId);
        if (species == null)
            throw new ArgumentException($"Unknown species ID: {speciesId}");

        rng ??= Random.Shared;
        var ivs = StatCalculator.RandomIVs(rng);
        var evs = StatCalculator.ZeroEVs();
        var stats = StatCalculator.CalculateAll(species, level, ivs, evs);

        // Default moveset: Tackle (id 1) so every Pokemon has at least one move
        var moveIds = new[] { 1 };
        var movePPs = new[] { 35 };

        return new PartyPokemon
        {
            Nickname = species.Name,
            SpeciesId = speciesId,
            Level = level,
            CurrentHP = stats.hp,
            MaxHP = stats.hp,
            Gender = gender,
            GrowthRate = species.GrowthRate,
            ExperiencePoints = GrowthRateHelper.GetEXPForLevel(species.GrowthRate, level),
            IVs = ivs,
            EVs = evs,
            Attack = stats.atk,
            Defense = stats.def,
            SpAttack = stats.spAtk,
            SpDefense = stats.spDef,
            Speed = stats.speed,
            MoveIds = moveIds,
            MovePPs = movePPs,
        };
    }
}
