#nullable enable

namespace PokemonGreen.Core.Maps;

/// <summary>
/// Static registry for world definitions.
/// Provides centralized access to all registered worlds in the game.
/// </summary>
public static class WorldRegistry
{
    private static readonly Dictionary<string, WorldDefinition> _worlds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The default world to load on game start.</summary>
    public static string DefaultWorldId => "small_world";

    /// <summary>All registered world definitions.</summary>
    public static IReadOnlyCollection<WorldDefinition> AllWorlds => _worlds.Values;

    /// <summary>
    /// Initializes the registry with all known worlds.
    /// </summary>
    public static void Initialize()
    {
        Register(new WorldDefinition("small_world", "Small World", "test_map_center", 8, 8));
    }

    /// <summary>
    /// Registers a world definition.
    /// </summary>
    public static void Register(WorldDefinition world)
    {
        _worlds[world.Id] = world;
    }

    /// <summary>
    /// Gets a world definition by its ID, or null if not found.
    /// </summary>
    public static WorldDefinition? GetWorld(string id)
    {
        return _worlds.TryGetValue(id, out var world) ? world : null;
    }

    /// <summary>
    /// Clears all registered worlds.
    /// </summary>
    public static void Clear()
    {
        _worlds.Clear();
    }
}
