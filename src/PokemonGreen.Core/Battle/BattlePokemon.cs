using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// A Pokemon participating in battle. Wraps stats, moves, and display HP for animation.
/// </summary>
public class BattlePokemon
{
    public string Nickname { get; }
    public int Species { get; }
    public int Level { get; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; }
    public Gender Gender { get; }
    public string? Status { get; set; }
    public BattleMove[] Moves { get; }

    /// <summary>Smoothly animated HP for the bar display. Lerps toward CurrentHP.</summary>
    public float DisplayHP { get; set; }

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public float DisplayHPPercent => MaxHP > 0 ? DisplayHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;

    public void ApplyDamage(int damage)
    {
        CurrentHP = System.Math.Max(0, CurrentHP - damage);
    }

    public BattlePokemon(string nickname, int species, int level,
                         int currentHP, int maxHP, Gender gender,
                         params BattleMove[] moves)
    {
        Nickname = nickname;
        Species = species;
        Level = level;
        CurrentHP = currentHP;
        MaxHP = maxHP;
        Gender = gender;
        Moves = moves;
        DisplayHP = currentHP;
    }

    /// <summary>Update DisplayHP to smoothly approach CurrentHP.</summary>
    public void UpdateDisplayHP(float deltaTime, float drainSpeed = 80f)
    {
        if (System.Math.Abs(DisplayHP - CurrentHP) < 0.5f)
        {
            DisplayHP = CurrentHP;
            return;
        }
        float dir = CurrentHP > DisplayHP ? 1f : -1f;
        DisplayHP += dir * drainSpeed * deltaTime;
        if ((dir > 0 && DisplayHP > CurrentHP) || (dir < 0 && DisplayHP < CurrentHP))
            DisplayHP = CurrentHP;
    }

    public static BattlePokemon CreateTestAlly() => new(
        "Charmander", 4, 5, 20, 20, Gender.Male,
        new BattleMove(2, 35),   // Scratch
        new BattleMove(3, 40),   // Growl
        new BattleMove(5, 25),   // Ember
        new BattleMove(4, 30));  // Leer

    public static BattlePokemon CreateTestFoe() => new(
        "Pidgey", 16, 3, 15, 15, Gender.Female,
        new BattleMove(1, 35),   // Tackle
        new BattleMove(8, 15));  // Sand Attack
}
