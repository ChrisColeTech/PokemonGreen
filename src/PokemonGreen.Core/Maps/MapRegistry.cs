using System.Reflection;

namespace PokemonGreen.Core.Maps;

/// <summary>
/// Discovers and initializes all MapDefinition subclasses via reflection.
/// No hardcoded list — delete or add any .g.cs map file and it just works.
/// </summary>
public static class MapRegistry
{
    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var mapTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MapDefinition)) && !t.IsAbstract);

        foreach (var type in mapTypes)
        {
            // Each generated map has a static Instance property (singleton)
            var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                // Accessing the property triggers the static constructor, which registers the map
                instanceProp.GetValue(null);
            }
        }
    }
}
