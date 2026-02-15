namespace PokemonGreen.Core.NPCs;

public record NPCDefinition(
    int Id,
    string Name,
    string SpriteName,
    NPCRole Role,
    int FrameWidth,
    int FrameHeight,
    int FramesPerDirection,
    string[] Animations
);
