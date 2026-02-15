using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.UI;

/// <summary>
/// Shared rendering style matching the old engine's Window decorations
/// and FontColors definitions.
/// </summary>
public static class UIStyle
{
    // Battle decoration border gradient (top-to-bottom: black → dark gray → gray)
    private static readonly Color BorderLine1 = new(0, 0, 0, 200);
    private static readonly Color BorderLine2 = new(30, 30, 30, 200);
    private static readonly Color BorderLine3 = new(60, 60, 60, 200);
    private static readonly Color BorderInnerEdge = Color.White;
    private static readonly Color BattleFill = new(80, 80, 80, 200);

    // Text colors — from old engine FontColors.cs (3-channel: transparent, main, outline)
    // DefaultWhite_I
    public static readonly Color TextNormal = new(239, 239, 239);
    public static readonly Color TextNormalOutline = new(132, 132, 132);
    // DefaultYellow_O
    public static readonly Color TextSelected = new(255, 224, 22);
    public static readonly Color TextSelectedOutline = new(188, 165, 16);
    // DefaultDisabled
    public static readonly Color TextDisabled = new(133, 133, 141);
    public static readonly Color TextDisabledOutline = new(58, 50, 50);
    // DefaultDarkGray_I (used for standard message text)
    public static readonly Color TextDarkGray = new(90, 82, 82);
    public static readonly Color TextDarkGrayOutline = new(165, 165, 173);
    // Prompt color
    public static readonly Color TextPrompt = new(160, 160, 180);

    // Menu selection highlight
    public static readonly Color SelectionHighlight = new(100, 100, 120, 100);

    /// <summary>
    /// Draw a battle-style panel matching the old engine's Window.Decoration.Battle.
    /// Gradient border: 1px black → 1px dark gray → 1px gray, then 2px white inner edge,
    /// with semi-transparent dark fill.
    /// </summary>
    public static void DrawBattlePanel(SpriteBatch sb, Texture2D pixel, Rectangle r)
    {
        // Top gradient border (3 lines)
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), BorderLine1);
        sb.Draw(pixel, new Rectangle(r.X, r.Y + 1, r.Width, 1), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X, r.Y + 2, r.Width, 1), BorderLine3);

        // Bottom gradient border (reversed: gray → dark gray → black)
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 3, r.Width, 1), BorderLine3);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 1), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), BorderLine1);

        // Left gradient border (3 lines)
        sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), BorderLine1);
        sb.Draw(pixel, new Rectangle(r.X + 1, r.Y, 1, r.Height), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.X + 2, r.Y, 1, r.Height), BorderLine3);

        // Right gradient border (reversed)
        sb.Draw(pixel, new Rectangle(r.Right - 3, r.Y, 1, r.Height), BorderLine3);
        sb.Draw(pixel, new Rectangle(r.Right - 2, r.Y, 1, r.Height), BorderLine2);
        sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), BorderLine1);

        // White inner edge (2px all sides, inside the gradient)
        var inner = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
        sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), BorderInnerEdge);          // top
        sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), BorderInnerEdge); // bottom
        sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), BorderInnerEdge);         // left
        sb.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), BorderInnerEdge); // right

        // Fill area inside white edges
        var fill = new Rectangle(inner.X + 2, inner.Y + 2, inner.Width - 4, inner.Height - 4);
        sb.Draw(pixel, fill, BattleFill);
    }

    /// <summary>
    /// Draw text with a 1px outline offset for readability (approximates the engine's
    /// multi-color font rendering with SpriteFont).
    /// </summary>
    public static void DrawShadowedText(SpriteBatch sb, SpriteFont font, string text,
        Vector2 position, Color color, Color outline)
    {
        sb.DrawString(font, text, position + new Vector2(1, 1), outline);
        sb.DrawString(font, text, position, color);
    }

    /// <summary>
    /// Draw a small right-pointing triangle (menu cursor). Size is in pixels.
    /// </summary>
    public static void DrawArrowRight(SpriteBatch sb, Texture2D pixel, int x, int y, int size, Color color)
    {
        for (int row = 0; row < size; row++)
        {
            int halfSize = size / 2;
            int width = row <= halfSize ? row + 1 : size - row;
            sb.Draw(pixel, new Rectangle(x, y + row, width, 1), color);
        }
    }

    /// <summary>
    /// Draw a small downward-pointing triangle (advance prompt). Size is in pixels.
    /// </summary>
    public static void DrawArrowDown(SpriteBatch sb, Texture2D pixel, int x, int y, int size, Color color)
    {
        for (int row = 0; row < size; row++)
        {
            int width = size - row * 2;
            if (width <= 0) break;
            int offsetX = row;
            sb.Draw(pixel, new Rectangle(x + offsetX, y + row, width, 1), color);
        }
    }
}
