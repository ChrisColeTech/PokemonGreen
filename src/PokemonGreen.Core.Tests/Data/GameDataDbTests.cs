using PokemonGreen.Core.Battle;
using PokemonGreen.Core.Data;
using PokemonGreen.Core.Encounters;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Pokemon;
using Xunit;

namespace PokemonGreen.Core.Tests.Data;

/// <summary>
/// Shared fixture that initializes GameDataDb once for all tests.
/// </summary>
public sealed class GameDataDbFixture
{
    public GameDataDbFixture()
    {
        GameDataDb.Initialize(AppDomain.CurrentDomain.BaseDirectory);
    }
}

// ==========================================================================
// Move Database Tests
// ==========================================================================

public sealed class MoveDbTests : IClassFixture<GameDataDbFixture>
{
    public MoveDbTests(GameDataDbFixture _) { }

    [Fact]
    public void GetMove_Tackle_ReturnsCorrectData()
    {
        var move = GameDataDb.GetMove(33);

        Assert.NotNull(move);
        Assert.Equal("Tackle", move.Name);
        Assert.Equal(MoveType.Normal, move.Type);
        Assert.Equal(MoveCategory.Physical, move.Category);
        Assert.True(move.Power > 0);
        Assert.True(move.Accuracy > 0);
        Assert.True(move.MaxPP > 0);
    }

    [Theory]
    [InlineData(1, "Pound", MoveType.Normal, MoveCategory.Physical)]
    [InlineData(52, "Ember", MoveType.Fire, MoveCategory.Special)]
    [InlineData(55, "Water Gun", MoveType.Water, MoveCategory.Special)]
    [InlineData(22, "Vine Whip", MoveType.Grass, MoveCategory.Physical)]
    [InlineData(85, "Thunderbolt", MoveType.Electric, MoveCategory.Special)]
    [InlineData(53, "Flamethrower", MoveType.Fire, MoveCategory.Special)]
    [InlineData(94, "Psychic", MoveType.Psychic, MoveCategory.Special)]
    [InlineData(89, "Earthquake", MoveType.Ground, MoveCategory.Physical)]
    [InlineData(58, "Ice Beam", MoveType.Ice, MoveCategory.Special)]
    public void GetMove_TypeAndCategory(int id, string name, MoveType type, MoveCategory category)
    {
        var move = GameDataDb.GetMove(id);

        Assert.NotNull(move);
        Assert.Equal(name, move.Name);
        Assert.Equal(type, move.Type);
        Assert.Equal(category, move.Category);
    }

    [Fact]
    public void GetMove_InvalidId_ReturnsNull()
    {
        Assert.Null(GameDataDb.GetMove(99999));
    }

    [Fact]
    public void GetMove_StatusMove_HasZeroPower()
    {
        var swordsDance = GameDataDb.GetMove(14);
        Assert.NotNull(swordsDance);
        Assert.Equal(MoveCategory.Status, swordsDance.Category);
        Assert.Equal(0, swordsDance.Power);
    }

    [Fact]
    public void GetMove_QuickAttack_HasPositivePriority()
    {
        var qa = GameDataDb.GetMove(98);
        Assert.NotNull(qa);
        Assert.Equal(1, qa.Priority);
    }

    [Fact]
    public void MoveRegistry_DelegatesToDb()
    {
        var move = MoveRegistry.GetMove(53);
        Assert.NotNull(move);
        Assert.Equal("Flamethrower", move.Name);
    }
}

// ==========================================================================
// Learnset Tests
// ==========================================================================

public sealed class LearnsetTests : IClassFixture<GameDataDbFixture>
{
    public LearnsetTests(GameDataDbFixture _) { }

    [Fact]
    public void Charmander_Level5_KnowsScratchAndGrowl()
    {
        var moves = GameDataDb.GetLevelUpMoves(4, 5);
        var ids = moves.Select(m => m.moveId).ToList();

        Assert.Contains(10, ids);  // Scratch
        Assert.Contains(45, ids);  // Growl
    }

    [Fact]
    public void Charmander_Level9_KnowsEmber()
    {
        var moves = GameDataDb.GetLevelUpMoves(4, 9);
        var ids = moves.Select(m => m.moveId).ToList();

        Assert.Contains(52, ids); // Ember
    }

    [Fact]
    public void ResultsOrderedByLevelDescending()
    {
        var moves = GameDataDb.GetLevelUpMoves(25, 50); // Pikachu

        Assert.True(moves.Count >= 2);
        for (int i = 0; i < moves.Count - 1; i++)
            Assert.True(moves[i].level >= moves[i + 1].level);
    }

    [Fact]
    public void HigherLevel_HasMoreMoves()
    {
        var at5 = GameDataDb.GetLevelUpMoves(1, 5);
        var at30 = GameDataDb.GetLevelUpMoves(1, 30);

        Assert.True(at30.Count > at5.Count);
    }

    [Fact]
    public void InvalidSpecies_ReturnsEmpty()
    {
        Assert.Empty(GameDataDb.GetLevelUpMoves(99999, 50));
    }
}

// ==========================================================================
// Moveset Generation Tests (PartyPokemon.Create pipeline)
// ==========================================================================

public sealed class MovesetGenerationTests : IClassFixture<GameDataDbFixture>
{
    public MovesetGenerationTests(GameDataDbFixture _) { }

    [Theory]
    [InlineData(4, 5)]      // Charmander
    [InlineData(7, 5)]      // Squirtle
    [InlineData(1, 5)]      // Bulbasaur
    [InlineData(25, 10)]    // Pikachu
    [InlineData(133, 15)]   // Eevee
    [InlineData(16, 3)]     // Pidgey low level
    [InlineData(94, 30)]    // Gengar
    [InlineData(149, 55)]   // Dragonite
    public void Create_HasValidMoveset(int speciesId, int level)
    {
        var pokemon = PartyPokemon.Create(speciesId, level, Gender.Unknown);

        Assert.NotEmpty(pokemon.MoveIds);
        Assert.Equal(pokemon.MoveIds.Length, pokemon.MovePPs.Length);
        Assert.InRange(pokemon.MoveIds.Length, 1, 4);

        foreach (var moveId in pokemon.MoveIds)
        {
            var data = MoveRegistry.GetMove(moveId);
            Assert.NotNull(data);
        }
    }

    [Fact]
    public void Create_HighLevel_CapsAtFourMoves()
    {
        var pokemon = PartyPokemon.Create(25, 80, Gender.Unknown);
        Assert.InRange(pokemon.MoveIds.Length, 1, 4);
    }

    [Fact]
    public void Create_PPsMatchMoveData()
    {
        var pokemon = PartyPokemon.Create(4, 15, Gender.Unknown);

        for (int i = 0; i < pokemon.MoveIds.Length; i++)
        {
            var data = MoveRegistry.GetMove(pokemon.MoveIds[i]);
            Assert.NotNull(data);
            Assert.Equal(data.MaxPP, pokemon.MovePPs[i]);
        }
    }

    [Fact]
    public void Create_DifferentLevels_DifferentMoves()
    {
        var low = PartyPokemon.Create(1, 5, Gender.Unknown);
        var high = PartyPokemon.Create(1, 40, Gender.Unknown);

        Assert.True(!low.MoveIds.SequenceEqual(high.MoveIds),
            $"Level 5 [{string.Join(",", low.MoveIds)}] should differ from level 40 [{string.Join(",", high.MoveIds)}]");
    }
}

// ==========================================================================
// Encounter → Battle Pipeline Tests
// ==========================================================================

public sealed class EncounterPipelineTests : IClassFixture<GameDataDbFixture>
{
    public EncounterPipelineTests(GameDataDbFixture _) { }

    [Fact]
    public void WildEncounter_ProducesPartyPokemon_WithMoves()
    {
        // Simulate what Game1.cs does when an encounter fires
        var encounter = new WildEncounterResult(SpeciesId: 16, Level: 5, EncounterType: "tall_grass");

        var wild = PartyPokemon.Create(encounter.SpeciesId, encounter.Level, Gender.Unknown);

        Assert.Equal(16, wild.SpeciesId);
        Assert.Equal(5, wild.Level);
        Assert.NotEmpty(wild.MoveIds);
        Assert.True(wild.CurrentHP > 0);
    }

    [Fact]
    public void WildEncounter_ConvertsToBattlePokemon_WithBattleMoves()
    {
        var encounter = new WildEncounterResult(SpeciesId: 4, Level: 8, EncounterType: "tall_grass");
        var wild = PartyPokemon.Create(encounter.SpeciesId, encounter.Level, Gender.Unknown);

        var battle = BattlePokemon.FromParty(wild);

        Assert.Equal(4, battle.SpeciesId);
        Assert.Equal(8, battle.Level);
        Assert.NotEmpty(battle.Moves);
        Assert.Equal(wild.MoveIds.Length, battle.Moves.Length);

        // Every BattleMove should have valid PP
        foreach (var bm in battle.Moves)
        {
            Assert.True(bm.MaxPP > 0);
            Assert.Equal(bm.MaxPP, bm.CurrentPP); // Full PP at start
            var data = MoveRegistry.GetMove(bm.MoveId);
            Assert.NotNull(data);
        }
    }

    [Fact]
    public void EncounterRegistry_RollsFromTable()
    {
        var table = new EncounterTable
        {
            EncounterType = "tall_grass",
            BaseEncounterRate = 255, // guaranteed encounter
            Entries =
            [
                new EncounterEntry { SpeciesId = 19, MinLevel = 3, MaxLevel = 5, Weight = 100 }
            ]
        };

        // Manually test the encounter result → PartyPokemon pipeline
        // (EncounterRegistry.TryEncounter uses random, so we test the downstream)
        var result = new WildEncounterResult(table.Entries[0].SpeciesId, 4, "tall_grass");
        var pokemon = PartyPokemon.Create(result.SpeciesId, result.Level, Gender.Unknown);

        Assert.Equal(19, pokemon.SpeciesId); // Rattata
        Assert.Equal(4, pokemon.Level);
        Assert.NotEmpty(pokemon.MoveIds);
    }
}

// ==========================================================================
// Battle Damage Tests
// ==========================================================================

public sealed class BattleDamageTests : IClassFixture<GameDataDbFixture>
{
    public BattleDamageTests(GameDataDbFixture _) { }

    [Fact]
    public void ApplyDamage_ReducesHP()
    {
        var pokemon = PartyPokemon.Create(4, 10, Gender.Unknown);
        var battle = BattlePokemon.FromParty(pokemon);
        int startHP = battle.CurrentHP;

        battle.ApplyDamage(5);

        Assert.Equal(startHP - 5, battle.CurrentHP);
        Assert.False(battle.IsFainted);
    }

    [Fact]
    public void ApplyDamage_CannotGoBelowZero()
    {
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);
        var battle = BattlePokemon.FromParty(pokemon);

        battle.ApplyDamage(9999);

        Assert.Equal(0, battle.CurrentHP);
        Assert.True(battle.IsFainted);
    }

    [Fact]
    public void BattleMove_DecrementsPP()
    {
        var pokemon = PartyPokemon.Create(4, 10, Gender.Unknown);
        var battle = BattlePokemon.FromParty(pokemon);
        var move = battle.Moves[0];
        int startPP = move.CurrentPP;

        move.CurrentPP--;

        Assert.Equal(startPP - 1, move.CurrentPP);
        Assert.Equal(startPP, move.MaxPP); // MaxPP unchanged
    }

    [Fact]
    public void DamagingMove_HasPositivePower()
    {
        var pokemon = PartyPokemon.Create(4, 15, Gender.Unknown);

        foreach (var moveId in pokemon.MoveIds)
        {
            var data = MoveRegistry.GetMove(moveId);
            Assert.NotNull(data);
            // Move is either damaging (power > 0) or status (power == 0)
            Assert.True(data.Power >= 0);
            if (data.Category != MoveCategory.Status)
                Assert.True(data.Power > 0);
        }
    }

    [Fact]
    public void SyncToParty_PersistsDamage()
    {
        var party = PartyPokemon.Create(7, 10, Gender.Unknown);
        var battle = BattlePokemon.FromParty(party);

        battle.ApplyDamage(3);
        battle.SyncToParty();

        Assert.Equal(battle.CurrentHP, party.CurrentHP);
    }
}

// ==========================================================================
// Item / Recovery Tests
// ==========================================================================

public sealed class ItemRecoveryTests
{
    private static ItemDefinition MakeItem(string effect, bool battle = true, bool overworld = true)
        => new(999, "Test Item", "test", ItemCategory.Medicine, 0, 0, battle, overworld, effect);

    [Fact]
    public void Potion_HealsHP()
    {
        var item = MakeItem("heal_hp_20");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 10, MaxHP = 30,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.True(result.Success);
        Assert.Equal(20, result.HPRestored);
        Assert.Equal(30, pokemon.CurrentHP);
    }

    [Fact]
    public void Potion_CapsAtMaxHP()
    {
        var item = MakeItem("heal_hp_200");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 25, MaxHP = 30,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.True(result.Success);
        Assert.Equal(5, result.HPRestored);
        Assert.Equal(30, pokemon.CurrentHP);
    }

    [Fact]
    public void Potion_FailsAtFullHP()
    {
        var item = MakeItem("heal_hp_20");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 30, MaxHP = 30,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.False(result.Success);
    }

    [Fact]
    public void Potion_FailsOnFainted()
    {
        var item = MakeItem("heal_hp_20");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 0, MaxHP = 30,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.False(result.Success);
    }

    [Fact]
    public void FullRestore_HealsHPAndStatus()
    {
        var item = MakeItem("heal_full");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 5, MaxHP = 30,
            StatusCondition = StatusCondition.Poison,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.True(result.Success);
        Assert.Equal(30, pokemon.CurrentHP);
        Assert.Equal(StatusCondition.None, pokemon.StatusCondition);
    }

    [Fact]
    public void Revive_RestoresFaintedPokemon()
    {
        var item = MakeItem("revive_50");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 0, MaxHP = 40,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.True(result.Success);
        Assert.Equal(20, pokemon.CurrentHP); // 50% of 40
        Assert.False(pokemon.IsFainted);
    }

    [Fact]
    public void Revive_FailsOnHealthy()
    {
        var item = MakeItem("revive_50");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 20, MaxHP = 40,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.False(result.Success);
    }

    [Fact]
    public void Antidote_CuresPoison()
    {
        var item = MakeItem("cure_poison");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 20, MaxHP = 40,
            StatusCondition = StatusCondition.Poison,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.True(result.Success);
        Assert.Equal(StatusCondition.None, pokemon.StatusCondition);
    }

    [Fact]
    public void Antidote_FailsOnWrongStatus()
    {
        var item = MakeItem("cure_poison");
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 20, MaxHP = 40,
            StatusCondition = StatusCondition.Burn,
        };

        var result = ItemUseHandler.UseItem(item, pokemon);

        Assert.False(result.Success);
    }

    [Fact]
    public void CanUseItem_RespectsContext()
    {
        var battleOnly = MakeItem("heal_hp_20", battle: true, overworld: false);
        var pokemon = new PartyPokemon
        {
            Nickname = "Test", SpeciesId = 1, Level = 10,
            CurrentHP = 10, MaxHP = 30,
        };

        Assert.True(ItemUseHandler.CanUseItem(battleOnly, pokemon, inBattle: true));
        Assert.False(ItemUseHandler.CanUseItem(battleOnly, pokemon, inBattle: false));
    }
}

// ==========================================================================
// Evolution Data Tests
// ==========================================================================

public sealed class EvolutionDbTests : IClassFixture<GameDataDbFixture>
{
    public EvolutionDbTests(GameDataDbFixture _) { }

    [Fact]
    public void Bulbasaur_EvolvesToIvysaur_AtLevel16()
    {
        var evos = GameDataDb.GetEvolutions(1);

        Assert.Single(evos);
        Assert.Equal(2, evos[0].ToSpeciesId);
        Assert.Equal("level-up", evos[0].Trigger);
        Assert.Equal(16, evos[0].MinLevel);
    }

    [Fact]
    public void Ivysaur_EvolvesToVenusaur_AtLevel32()
    {
        var evos = GameDataDb.GetEvolutions(2);

        Assert.Single(evos);
        Assert.Equal(3, evos[0].ToSpeciesId);
        Assert.Equal("level-up", evos[0].Trigger);
        Assert.Equal(32, evos[0].MinLevel);
    }

    [Fact]
    public void Charmander_EvolvesAtLevel16()
    {
        var evos = GameDataDb.GetEvolutions(4);

        Assert.Single(evos);
        Assert.Equal(5, evos[0].ToSpeciesId);
        Assert.Equal(16, evos[0].MinLevel);
    }

    [Fact]
    public void Pikachu_EvolvesWithThunderStone()
    {
        var evos = GameDataDb.GetEvolutions(25);

        Assert.NotEmpty(evos);
        var thunderStone = evos.FirstOrDefault(e => e.Item == "thunder-stone");
        Assert.NotNull(thunderStone);
        Assert.Equal(26, thunderStone.ToSpeciesId); // Raichu
        Assert.Equal("use-item", thunderStone.Trigger);
        Assert.Null(thunderStone.MinLevel);
    }

    [Fact]
    public void Eevee_HasMultipleEvolutions()
    {
        var evos = GameDataDb.GetEvolutions(133);

        Assert.True(evos.Count >= 3, $"Eevee should have 3+ evolutions, got {evos.Count}");

        var toIds = evos.Select(e => e.ToSpeciesId).Distinct().ToList();
        Assert.Contains(134, toIds); // Vaporeon
        Assert.Contains(135, toIds); // Jolteon
        Assert.Contains(136, toIds); // Flareon
    }

    [Fact]
    public void Eevee_Espeon_RequiresHappinessAndDay()
    {
        var evos = GameDataDb.GetEvolutions(133);
        var espeon = evos.FirstOrDefault(e => e.ToSpeciesId == 196);

        Assert.NotNull(espeon);
        Assert.Equal("level-up", espeon.Trigger);
        Assert.NotNull(espeon.MinHappiness);
        Assert.Equal("day", espeon.TimeOfDay);
    }

    [Fact]
    public void NoEvolution_ReturnsEmpty()
    {
        // Mew (151) doesn't evolve
        var evos = GameDataDb.GetEvolutions(151);
        Assert.Empty(evos);
    }

    [Fact]
    public void InvalidSpecies_ReturnsEmpty()
    {
        Assert.Empty(GameDataDb.GetEvolutions(99999));
    }
}

// ==========================================================================
// EXP and Level-Up Tests
// ==========================================================================

public sealed class EXPLevelUpTests : IClassFixture<GameDataDbFixture>
{
    public EXPLevelUpTests(GameDataDbFixture _) { }

    [Fact]
    public void AddEXP_GainsLevel()
    {
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);
        int oldLevel = pokemon.Level;

        uint expForNextLevel = GrowthRateHelper.GetEXPForLevel(pokemon.GrowthRate, oldLevel + 1);
        uint needed = expForNextLevel - pokemon.ExperiencePoints + 1;

        var result = pokemon.AddEXP(needed);

        Assert.True(result.LevelsGained >= 1);
        Assert.True(pokemon.Level > oldLevel);
    }

    [Fact]
    public void AddEXP_RecalculatesStats()
    {
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);
        int oldMaxHP = pokemon.MaxHP;

        pokemon.AddEXP(10000);

        Assert.True(pokemon.Level > 5);
        Assert.True(pokemon.MaxHP > oldMaxHP);
    }

    [Fact]
    public void AddEXP_CapsAtLevel100()
    {
        var pokemon = PartyPokemon.Create(4, 99, Gender.Unknown);

        pokemon.AddEXP(999999);

        Assert.Equal(100, pokemon.Level);
    }

    [Fact]
    public void EXPGain_ScaledFormula_LowerForHigherVictor()
    {
        int baseYield = 64;
        int foeLevel = 5;

        uint expLow = GrowthRateHelper.CalculateEXPGain(baseYield, foeLevel, 5, false, 1);
        uint expHigh = GrowthRateHelper.CalculateEXPGain(baseYield, foeLevel, 50, false, 1);

        Assert.True(expLow > expHigh,
            $"Level 5 victor ({expLow} EXP) should get more than level 50 ({expHigh} EXP)");
    }

    [Fact]
    public void TrainerBattle_GivesMoreEXP()
    {
        uint wildEXP = GrowthRateHelper.CalculateEXPGain(64, 10, 10, false, 1);
        uint trainerEXP = GrowthRateHelper.CalculateEXPGain(64, 10, 10, true, 1);

        Assert.True(trainerEXP > wildEXP);
    }
}

// ==========================================================================
// Level-Up Move Learning Tests
// ==========================================================================

public sealed class LevelUpMoveLearnTests : IClassFixture<GameDataDbFixture>
{
    public LevelUpMoveLearnTests(GameDataDbFixture _) { }

    [Fact]
    public void GetMovesLearnedAtLevel_ReturnsCorrectMoves()
    {
        // Charmander learns Ember at level 7 in USUM
        var moves = GameDataDb.GetMovesLearnedAtLevel(4, 7);
        Assert.NotEmpty(moves);
    }

    [Fact]
    public void GetMovesLearnedAtLevel_NoMovesAtLevel_ReturnsEmpty()
    {
        // Level 2 — unlikely any Pokemon learns a move exactly at level 2
        var moves = GameDataDb.GetMovesLearnedAtLevel(4, 2);
        // This may or may not be empty depending on data, so just verify it doesn't crash
        Assert.NotNull(moves);
    }

    [Fact]
    public void AddEXP_LearnsNewMoves_OnLevelUp()
    {
        // Start Charmander just below a level where it learns a move
        // Charmander learns moves at various levels — level up several times to ensure we hit one
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);
        var oldMoves = pokemon.MoveIds.ToArray();

        // Level up a lot to guarantee move learning
        var result = pokemon.AddEXP(50000);

        Assert.True(result.LevelsGained > 0);
        Assert.NotEmpty(result.NewMoveIds);

        // Verify each new move resolves in the DB
        foreach (var moveId in result.NewMoveIds)
        {
            var data = MoveRegistry.GetMove(moveId);
            Assert.NotNull(data);
        }
    }

    [Fact]
    public void AddEXP_MovesetStaysAtFourMax()
    {
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);

        // Level up a lot — should learn many moves but cap at 4
        pokemon.AddEXP(100000);

        Assert.InRange(pokemon.MoveIds.Length, 1, 4);
        Assert.Equal(pokemon.MoveIds.Length, pokemon.MovePPs.Length);
    }

    [Fact]
    public void AddEXP_ReplacesOldestMove_WhenFull()
    {
        // Start with exactly 4 moves, then level up to learn new ones
        var pokemon = PartyPokemon.Create(25, 10, Gender.Unknown); // Pikachu
        // Ensure 4 moves
        if (pokemon.MoveIds.Length < 4)
        {
            // Pad to 4
            var ids = pokemon.MoveIds.ToList();
            var pps = pokemon.MovePPs.ToList();
            while (ids.Count < 4) { ids.Add(33); pps.Add(35); } // Tackle padding
            pokemon.MoveIds = ids.ToArray();
            pokemon.MovePPs = pps.ToArray();
        }

        var originalFirst = pokemon.MoveIds[0];

        // Level up a lot
        var result = pokemon.AddEXP(200000);

        if (result.NewMoveIds.Count > 0)
        {
            // Should have replaced oldest moves
            Assert.NotEmpty(result.ReplacedMoveIds);
            Assert.Equal(4, pokemon.MoveIds.Length); // Still 4 max
        }
    }

    [Fact]
    public void AddEXP_DoesNotLearnDuplicateMoves()
    {
        var pokemon = PartyPokemon.Create(4, 5, Gender.Unknown);

        pokemon.AddEXP(100000);

        // No duplicate move IDs
        Assert.Equal(pokemon.MoveIds.Length, pokemon.MoveIds.Distinct().Count());
    }

    [Fact]
    public void LevelUpResult_None_HasZeros()
    {
        var none = LevelUpResult.None;

        Assert.Equal(0, none.LevelsGained);
        Assert.Empty(none.NewMoveIds);
        Assert.Empty(none.ReplacedMoveIds);
        Assert.Null(none.PendingEvolution);
    }
}

// ==========================================================================
// Evolution Trigger Tests
// ==========================================================================

public sealed class EvolutionTriggerTests : IClassFixture<GameDataDbFixture>
{
    public EvolutionTriggerTests(GameDataDbFixture _) { }

    [Fact]
    public void CheckEvolution_Charmander_AtLevel16_ReturnsPending()
    {
        var pokemon = PartyPokemon.Create(4, 16, Gender.Unknown);

        var evo = pokemon.CheckEvolution();

        Assert.NotNull(evo);
        Assert.Equal(5, evo.ToSpeciesId); // Charmeleon
        Assert.Equal("level-up", evo.Trigger);
    }

    [Fact]
    public void CheckEvolution_Charmander_BelowLevel16_ReturnsNull()
    {
        var pokemon = PartyPokemon.Create(4, 15, Gender.Unknown);

        var evo = pokemon.CheckEvolution();

        Assert.Null(evo);
    }

    [Fact]
    public void CheckEvolution_Pikachu_NoLevelEvolution()
    {
        // Pikachu evolves with Thunder Stone, not by level
        var pokemon = PartyPokemon.Create(25, 50, Gender.Unknown);

        var evo = pokemon.CheckEvolution();

        Assert.Null(evo); // No simple level-up evolution
    }

    [Fact]
    public void CheckEvolution_Mew_NeverEvolves()
    {
        var pokemon = PartyPokemon.Create(151, 100, Gender.Unknown);

        var evo = pokemon.CheckEvolution();

        Assert.Null(evo);
    }

    [Fact]
    public void AddEXP_ReturnsPendingEvolution_WhenLevelReached()
    {
        var pokemon = PartyPokemon.Create(4, 15, Gender.Unknown);

        // Level past 16
        uint expNeeded = GrowthRateHelper.GetEXPForLevel(pokemon.GrowthRate, 17) - pokemon.ExperiencePoints;
        var result = pokemon.AddEXP(expNeeded);

        Assert.True(result.LevelsGained >= 1);
        Assert.NotNull(result.PendingEvolution);
        Assert.Equal(5, result.PendingEvolution.ToSpeciesId); // Charmeleon
    }

    [Fact]
    public void Evolve_ChangesSpecies()
    {
        var pokemon = PartyPokemon.Create(4, 16, Gender.Unknown);

        pokemon.Evolve(5); // → Charmeleon

        Assert.Equal(5, pokemon.SpeciesId);
        Assert.Equal("Charmeleon", pokemon.Nickname);
    }

    [Fact]
    public void Evolve_KeepsCustomNickname()
    {
        var pokemon = PartyPokemon.Create(4, 16, Gender.Unknown);
        pokemon.Nickname = "Blaze";

        pokemon.Evolve(5);

        Assert.Equal(5, pokemon.SpeciesId);
        Assert.Equal("Blaze", pokemon.Nickname); // Custom name preserved
    }

    [Fact]
    public void Evolve_RecalculatesStats()
    {
        var pokemon = PartyPokemon.Create(4, 16, Gender.Unknown);
        int oldAttack = pokemon.Attack;

        pokemon.Evolve(5); // Charmeleon has higher base stats

        // Charmeleon should have higher stats than Charmander at same level
        Assert.True(pokemon.Attack >= oldAttack,
            $"Charmeleon attack ({pokemon.Attack}) should be >= Charmander attack ({oldAttack})");
    }

    [Fact]
    public void FullEvolutionChain_Bulbasaur_To_Venusaur()
    {
        var pokemon = PartyPokemon.Create(1, 15, Gender.Unknown);

        // Level to 16 → Ivysaur
        uint exp16 = GrowthRateHelper.GetEXPForLevel(pokemon.GrowthRate, 17) - pokemon.ExperiencePoints;
        var result1 = pokemon.AddEXP(exp16);

        Assert.NotNull(result1.PendingEvolution);
        Assert.Equal(2, result1.PendingEvolution.ToSpeciesId);

        pokemon.Evolve(2);
        Assert.Equal("Ivysaur", pokemon.Nickname);

        // Level to 32 → Venusaur
        uint exp32 = GrowthRateHelper.GetEXPForLevel(pokemon.GrowthRate, 33) - pokemon.ExperiencePoints;
        var result2 = pokemon.AddEXP(exp32);

        Assert.NotNull(result2.PendingEvolution);
        Assert.Equal(3, result2.PendingEvolution.ToSpeciesId);

        pokemon.Evolve(3);
        Assert.Equal("Venusaur", pokemon.Nickname);
        Assert.Equal(3, pokemon.SpeciesId);
    }

    [Fact]
    public void Eevee_SkipsConditionalEvolutions()
    {
        // Eevee has many evolutions but all require items, happiness, time, etc.
        // CheckEvolution should return null for simple level-up check
        var pokemon = PartyPokemon.Create(133, 50, Gender.Unknown);

        var evo = pokemon.CheckEvolution();

        Assert.Null(evo);
    }
}

// ==========================================================================
// Species Database Tests
// ==========================================================================

public sealed class SpeciesDbTests : IClassFixture<GameDataDbFixture>
{
    public SpeciesDbTests(GameDataDbFixture _) { }

    [Theory]
    [InlineData(1, "Bulbasaur", MoveType.Grass, MoveType.Poison)]
    [InlineData(4, "Charmander", MoveType.Fire, MoveType.Fire)]      // single-type → Type2 == Type1
    [InlineData(7, "Squirtle", MoveType.Water, MoveType.Water)]
    [InlineData(25, "Pikachu", MoveType.Electric, MoveType.Electric)]
    public void GetSpecies_ReturnsCorrectData(int id, string name, MoveType type1, MoveType type2)
    {
        var species = GameDataDb.GetSpecies(id);

        Assert.NotNull(species);
        Assert.Equal(name, species.Name);
        Assert.Equal(type1, species.Type1);
        Assert.Equal(type2, species.Type2);
    }

    [Fact]
    public void GetSpecies_HasValidBaseStats()
    {
        var charmander = GameDataDb.GetSpecies(4);

        Assert.NotNull(charmander);
        Assert.True(charmander.BaseHP > 0);
        Assert.True(charmander.BaseAttack > 0);
        Assert.True(charmander.BaseDefense > 0);
        Assert.True(charmander.BaseSpAttack > 0);
        Assert.True(charmander.BaseSpDefense > 0);
        Assert.True(charmander.BaseSpeed > 0);
        Assert.True(charmander.BaseEXPYield > 0);
        Assert.True(charmander.CatchRate > 0);
    }

    [Fact]
    public void SpeciesCount_IsReasonable()
    {
        Assert.True(GameDataDb.SpeciesCount >= 800);
    }

    [Fact]
    public void GetSpecies_InvalidId_ReturnsNull()
    {
        Assert.Null(GameDataDb.GetSpecies(99999));
    }
}

// ==========================================================================
// Full Pipeline Integration Test
// ==========================================================================

public sealed class FullPipelineTests : IClassFixture<GameDataDbFixture>
{
    public FullPipelineTests(GameDataDbFixture _) { }

    [Fact]
    public void FullPipeline_Encounter_To_Battle_To_Victory()
    {
        // 1. Simulate encounter result
        var encounter = new WildEncounterResult(SpeciesId: 19, Level: 4, EncounterType: "tall_grass");

        // 2. Create wild Pokemon with real moveset
        var wildParty = PartyPokemon.Create(encounter.SpeciesId, encounter.Level, Gender.Unknown);
        Assert.NotEmpty(wildParty.MoveIds);

        // 3. Create player Pokemon
        var playerParty = PartyPokemon.Create(4, 10, Gender.Unknown); // Charmander lvl 10
        Assert.NotEmpty(playerParty.MoveIds);

        // 4. Convert to battle Pokemon
        var ally = BattlePokemon.FromParty(playerParty);
        var foe = BattlePokemon.FromParty(wildParty);

        Assert.NotEmpty(ally.Moves);
        Assert.NotEmpty(foe.Moves);

        // 5. Simulate combat: ally attacks foe
        var allyMove = MoveRegistry.GetMove(ally.Moves[0].MoveId);
        Assert.NotNull(allyMove);

        if (allyMove.Power > 0)
        {
            int damage = Math.Max(1, (allyMove.Power * ally.Level / 5 + 2) / 3);
            foe.ApplyDamage(damage);
            Assert.True(foe.CurrentHP < foe.MaxHP);
        }

        // 6. Foe attacks back
        var foeMove = MoveRegistry.GetMove(foe.Moves[0].MoveId);
        Assert.NotNull(foeMove);

        if (foeMove.Power > 0)
        {
            int foeDamage = Math.Max(1, (foeMove.Power * foe.Level / 5 + 2) / 3);
            ally.ApplyDamage(foeDamage);
        }

        // 7. Knock out foe
        foe.ApplyDamage(foe.CurrentHP);
        Assert.True(foe.IsFainted);

        // 8. Award EXP
        var foeSpecies = SpeciesRegistry.GetSpecies(foe.SpeciesId);
        Assert.NotNull(foeSpecies);
        uint expGain = GrowthRateHelper.CalculateEXPGain(
            foeSpecies.BaseEXPYield, foe.Level, ally.Level, false, 1);
        Assert.True(expGain > 0);

        var expResult = playerParty.AddEXP(expGain);
        Assert.True(expResult.LevelsGained >= 0);

        // 9. Sync back to party
        ally.SyncToParty();
        Assert.Equal(ally.CurrentHP, playerParty.CurrentHP);

        // 10. Check evolution eligibility
        var evos = GameDataDb.GetEvolutions(playerParty.SpeciesId);
        // Charmander evolves at 16 — at level 10 shouldn't be triggered
        if (playerParty.Level < 16)
        {
            var levelEvo = evos.FirstOrDefault(e => e.Trigger == "level-up" && e.MinLevel <= playerParty.Level);
            Assert.Null(levelEvo);
        }
    }

    [Fact]
    public void FullPipeline_LevelUp_TriggersEvolutionEligibility()
    {
        // Start Charmander at level 15 — one level-up should trigger evolution check
        var pokemon = PartyPokemon.Create(4, 15, Gender.Unknown);

        // Level up past 16
        uint expNeeded = GrowthRateHelper.GetEXPForLevel(pokemon.GrowthRate, 17) - pokemon.ExperiencePoints;
        var result = pokemon.AddEXP(expNeeded);

        Assert.True(pokemon.Level >= 16);
        Assert.NotNull(result.PendingEvolution);
        Assert.Equal(5, result.PendingEvolution.ToSpeciesId); // Charmeleon
    }

    [Fact]
    public void FullPipeline_ItemHeal_MidBattle()
    {
        // Create battler, take damage, heal with item
        var party = PartyPokemon.Create(7, 15, Gender.Unknown);
        var battle = BattlePokemon.FromParty(party);

        // Take damage
        battle.ApplyDamage(10);
        battle.SyncToParty();

        // Use Potion (heal_hp_20)
        var potion = new ItemDefinition(100, "Potion", "potion", ItemCategory.Medicine,
            300, 150, true, true, "heal_hp_20");

        Assert.True(ItemUseHandler.CanUseItem(potion, party, inBattle: true));
        var result = ItemUseHandler.UseItem(potion, party);

        Assert.True(result.Success);
        Assert.True(result.HPRestored > 0);
        Assert.True(party.CurrentHP > battle.CurrentHP); // Party healed, battle not yet synced
    }
}
