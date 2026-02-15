using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Draws a battle info bar for one Pokemon (name, level, HP bar, optional HP text/EXP bar).
/// </summary>
public static class BattleInfoBar
{
    private static readonly Color PanelBG = new(48, 48, 48, 180);
    private static readonly Color NameColor = new(239, 239, 239);
    private static readonly Color LevelColor = new(200, 200, 200);
    private static readonly Color HPTextColor = new(180, 180, 180);
    private static readonly Color GenderMale = new(80, 140, 255);
    private static readonly Color GenderFemale = new(255, 100, 130);

    /// <summary>
    /// Draw the foe's info bar (shorter — no HP numbers, no EXP bar).
    /// </summary>
    public static void DrawFoeBar(SpriteBatch sb, Texture2D pixel,
        KermFontRenderer? fontRenderer, SpriteFont fallbackFont,
        Rectangle bounds, BattlePokemon pkmn, int fontScale = 2)
    {
        // Background panel
        sb.Draw(pixel, bounds, PanelBG);

        int pad = 8;
        int fontH = fontScale * 7; // approx KermFont height

        // Name
        int nameY = bounds.Y + pad;
        DrawText(sb, fontRenderer, fallbackFont, pkmn.Nickname,
            new Vector2(bounds.X + pad, nameY), NameColor, fontScale);

        // Gender symbol after name
        int nameWidth = pkmn.Nickname.Length * fontScale * 6; // approx char width
        DrawGenderSymbol(sb, fontRenderer, fallbackFont, pkmn.Gender,
            bounds.X + pad + nameWidth + 4, nameY, fontScale);

        // Level (right side)
        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = lvText.Length * fontScale * 6;
        DrawText(sb, fontRenderer, fallbackFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), LevelColor, fontScale);

        // HP bar
        int barY = nameY + fontH + 4;
        int barH = 6;
        UIStyle.DrawTripleLineHPBar(sb, pixel,
            new Rectangle(bounds.X + pad, barY, bounds.Width - pad * 2, barH),
            pkmn.DisplayHPPercent);

        // Status
        if (pkmn.Status != null)
        {
            DrawText(sb, fontRenderer, fallbackFont, pkmn.Status,
                new Vector2(bounds.X + pad, barY + barH + 3), new Color(255, 100, 100), fontScale);
        }
    }

    /// <summary>
    /// Draw the ally's info bar (full — HP numbers + EXP bar).
    /// </summary>
    public static void DrawAllyBar(SpriteBatch sb, Texture2D pixel,
        KermFontRenderer? fontRenderer, SpriteFont fallbackFont,
        Rectangle bounds, BattlePokemon pkmn, float expPercent, int fontScale = 2)
    {
        // Background panel
        sb.Draw(pixel, bounds, PanelBG);

        int pad = 8;
        int fontH = fontScale * 7;

        // Name
        int nameY = bounds.Y + pad;
        DrawText(sb, fontRenderer, fallbackFont, pkmn.Nickname,
            new Vector2(bounds.X + pad, nameY), NameColor, fontScale);

        // Gender symbol
        int nameWidth = pkmn.Nickname.Length * fontScale * 6;
        DrawGenderSymbol(sb, fontRenderer, fallbackFont, pkmn.Gender,
            bounds.X + pad + nameWidth + 4, nameY, fontScale);

        // Level (right side)
        string lvText = $"Lv{pkmn.Level}";
        int lvWidth = lvText.Length * fontScale * 6;
        DrawText(sb, fontRenderer, fallbackFont, lvText,
            new Vector2(bounds.Right - pad - lvWidth, nameY), LevelColor, fontScale);

        // HP bar
        int barY = nameY + fontH + 4;
        int barH = 6;
        UIStyle.DrawTripleLineHPBar(sb, pixel,
            new Rectangle(bounds.X + pad, barY, bounds.Width - pad * 2, barH),
            pkmn.DisplayHPPercent);

        // HP text below bar
        int hpTextY = barY + barH + 3;
        string hpText = $"{(int)pkmn.DisplayHP}/{pkmn.MaxHP}";
        int hpTextWidth = hpText.Length * fontScale * 6;
        DrawText(sb, fontRenderer, fallbackFont, hpText,
            new Vector2(bounds.Right - pad - hpTextWidth, hpTextY), HPTextColor, fontScale);

        // Status
        if (pkmn.Status != null)
        {
            DrawText(sb, fontRenderer, fallbackFont, pkmn.Status,
                new Vector2(bounds.X + pad, hpTextY), new Color(255, 100, 100), fontScale);
        }

        // EXP bar at bottom
        int expY = bounds.Bottom - 5 - pad / 2;
        UIStyle.DrawEXPBar(sb, pixel,
            new Rectangle(bounds.X + pad, expY, bounds.Width - pad * 2, 5),
            expPercent);
    }

    private static void DrawGenderSymbol(SpriteBatch sb, KermFontRenderer? fontRenderer,
        SpriteFont fallbackFont, Gender gender, int x, int y, int scale)
    {
        if (gender == Gender.Unknown) return;
        string symbol = gender == Gender.Male ? "M" : "F";
        Color color = gender == Gender.Male ? GenderMale : GenderFemale;
        DrawText(sb, fontRenderer, fallbackFont, symbol, new Vector2(x, y), color, scale);
    }

    private static void DrawText(SpriteBatch sb, KermFontRenderer? fontRenderer,
        SpriteFont fallbackFont, string text, Vector2 position, Color color, int scale)
    {
        if (fontRenderer != null)
            fontRenderer.DrawString(sb, text, position, scale, color);
        else
            sb.DrawString(fallbackFont, text, position, color);
    }
}
