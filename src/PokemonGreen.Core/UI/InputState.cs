using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.UI;

/// <summary>
/// Bundles per-frame "just pressed" input flags so overlays don't reference MonoGame input directly.
/// </summary>
public readonly struct InputState
{
    public bool Left { get; init; }
    public bool Right { get; init; }
    public bool Up { get; init; }
    public bool Down { get; init; }
    public bool Confirm { get; init; }
    public bool Cancel { get; init; }
    public Point MousePosition { get; init; }
    public bool MouseClicked { get; init; }
}
