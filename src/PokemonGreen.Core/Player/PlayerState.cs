namespace PokemonGreen.Core.Player;

/// <summary>
/// Represents the current state of the player character.
/// </summary>
public enum PlayerState
{
    /// <summary>Player is standing still.</summary>
    Idle,

    /// <summary>Player is walking at normal speed.</summary>
    Walk,

    /// <summary>Player is running at increased speed.</summary>
    Run,

    /// <summary>Player is jumping (e.g., over ledges).</summary>
    Jump,

    /// <summary>Player is climbing (e.g., ladders, vines).</summary>
    Climb,

    /// <summary>Player is engaged in combat.</summary>
    Combat,

    /// <summary>Player is casting a spell or using an ability.</summary>
    Spellcast
}
