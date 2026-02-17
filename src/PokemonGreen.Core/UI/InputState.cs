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
    /// <summary>True when any key was freshly pressed this frame (for message dismissal).</summary>
    public bool AnyKey { get; init; }
    public bool PageLeft { get; init; }
    public bool PageRight { get; init; }
    public Point MousePosition { get; init; }
    public bool MouseClicked { get; init; }
}
