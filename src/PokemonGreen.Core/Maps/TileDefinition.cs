namespace PokemonGreen.Core.Maps;

public record TileDefinition(
    int Id,
    string Name,
    bool Walkable,
    string Color,
    TileCategory Category,
    string? OverlayBehavior = null,
    int? EntityId = null,
    string? SpriteName = null,
    int AnimationFrames = 0
)
{
    public string GetSpriteName() => SpriteName ?? TileNameToSpriteName(Name);
    
    private static string TileNameToSpriteName(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append('_');
            result.Append(char.ToLower(c));
        }
        return $"tile_{result}";
    }
}
