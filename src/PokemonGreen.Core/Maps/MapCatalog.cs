namespace PokemonGreen.Core.Maps;

/// <summary>
/// Static registry for map definitions.
/// Provides centralized access to all registered maps in the game.
/// </summary>
public static class MapCatalog
{
    private static readonly Dictionary<string, MapDefinition> _maps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a map definition in the catalog.
    /// </summary>
    /// <param name="map">The map definition to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when map is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a map with the same ID is already registered.</exception>
    public static void Register(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (_maps.ContainsKey(map.Id))
            throw new ArgumentException($"A map with ID '{map.Id}' is already registered.", nameof(map));

        _maps[map.Id] = map;
    }

    /// <summary>
    /// Registers a map definition if not already present.
    /// </summary>
    /// <param name="map">The map definition to register.</param>
    /// <returns>True if the map was registered, false if a map with the same ID already exists.</returns>
    public static bool TryRegister(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _maps.TryAdd(map.Id, map);
    }

    /// <summary>
    /// Gets a map definition by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the map.</param>
    /// <returns>The map definition.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no map with the specified ID is found.</exception>
    public static MapDefinition GetMap(string id)
    {
        if (_maps.TryGetValue(id, out var map))
            return map;

        throw new KeyNotFoundException($"No map found with ID '{id}'.");
    }

    /// <summary>
    /// Gets all registered map definitions.
    /// </summary>
    /// <returns>A read-only collection of all registered maps.</returns>
    public static IReadOnlyCollection<MapDefinition> GetAllMaps()
    {
        return _maps.Values;
    }

    /// <summary>
    /// Attempts to get a map definition by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the map.</param>
    /// <param name="map">When this method returns, contains the map definition if found; otherwise, null.</param>
    /// <returns>True if a map with the specified ID was found; otherwise, false.</returns>
    public static bool TryGetMap(string id, out MapDefinition? map)
    {
        return _maps.TryGetValue(id, out map);
    }

    /// <summary>
    /// Clears all registered maps from the catalog.
    /// Primarily intended for testing purposes.
    /// </summary>
    internal static void Clear()
    {
        _maps.Clear();
    }
}
