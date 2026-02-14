namespace PokemonGreen.Core.Player;

/// <summary>
/// Represents the four cardinal directions for player movement and facing.
/// </summary>
public enum Direction
{
    /// <summary>Facing or moving upward (north).</summary>
    Up,

    /// <summary>Facing or moving downward (south).</summary>
    Down,

    /// <summary>Facing or moving left (west).</summary>
    Left,

    /// <summary>Facing or moving right (east).</summary>
    Right
}

/// <summary>
/// Static helper methods for Direction operations.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Converts a direction to a unit vector (dx, dy).
    /// </summary>
    /// <param name="d">The direction to convert.</param>
    /// <returns>A tuple representing the direction as (dx, dy).</returns>
    public static (int dx, int dy) ToVector(this Direction d) => d switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        Direction.Right => (1, 0),
        _ => (0, 0)
    };

    /// <summary>
    /// Returns the opposite direction.
    /// </summary>
    /// <param name="d">The direction to invert.</param>
    /// <returns>The opposite direction.</returns>
    public static Direction Opposite(this Direction d) => d switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => d
    };
}
