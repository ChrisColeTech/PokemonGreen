using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.UI.Screens;

/// <summary>
/// Full-screen character select overlay. 3x2 grid of character cards.
/// Returns the selected folder name via SelectedFolder when confirmed.
/// </summary>
public class CharacterSelectScreen : IScreenOverlay
{
    private enum Phase { FadeIn, Navigation, FadeOut }

    private const float FadeDuration = 0.25f;
    private const int GridColumns = 3;
    private const int GridRows = 2;
    private const int CardSpacing = 12;
    private const int BottomSectionHeight = 60;
    private const int Padding = 24;

    private static readonly Color CardFill = new(48, 48, 58, 200);
    private static readonly Color CardFillSelected = new(60, 80, 120, 220);
    private static readonly Color CardBorder = new(100, 200, 255, 220);
    private static readonly Color BackButtonNormal = new(48, 48, 48);
    private static readonly Color BackButtonSelected = new(96, 48, 48);

    private static readonly Color GradTop = new(30, 60, 100);
    private static readonly Color GradMid = new(20, 40, 80);
    private static readonly Color GradBot = new(60, 120, 180);

    private readonly string[] _folders;
    private readonly string[] _names;

    private Phase _phase = Phase.FadeIn;
    private float _fadeTimer;
    private int _selectedIndex;
    private bool _onBackButton;

    private Rectangle[] _cardRects = Array.Empty<Rectangle>();
    private Rectangle _backRect;

    public string? SelectedFolder { get; private set; }
    public bool IsFinished { get; private set; }

    public CharacterSelectScreen(string[] folders, string[] displayNames)
    {
        _folders = folders;
        _names = displayNames;
    }

    public void Update(float deltaTime, InputState input)
    {
        switch (_phase)
        {
            case Phase.FadeIn:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration)
                {
                    _fadeTimer = FadeDuration;
                    _phase = Phase.Navigation;
                }
                break;

            case Phase.Navigation:
                UpdateNavigation(input);
                break;

            case Phase.FadeOut:
                _fadeTimer += deltaTime;
                if (_fadeTimer >= FadeDuration)
                    IsFinished = true;
                break;
        }
    }

    private void UpdateNavigation(InputState input)
    {
        if (input.Cancel)
        {
            SelectedFolder = null;
            BeginExit();
            return;
        }

        // Mouse click
        if (input.MouseClicked)
        {
            for (int i = 0; i < _cardRects.Length && i < _folders.Length; i++)
            {
                if (_cardRects[i].Contains(input.MousePosition))
                {
                    _selectedIndex = i;
                    _onBackButton = false;
                    SelectedFolder = _folders[i];
                    BeginExit();
                    return;
                }
            }
            if (_backRect.Contains(input.MousePosition))
            {
                SelectedFolder = null;
                BeginExit();
                return;
            }
        }

        // Mouse hover
        if (!input.MouseClicked)
        {
            for (int i = 0; i < _cardRects.Length && i < _folders.Length; i++)
            {
                if (_cardRects[i].Contains(input.MousePosition))
                {
                    _selectedIndex = i;
                    _onBackButton = false;
                }
            }
            if (_backRect.Contains(input.MousePosition))
                _onBackButton = true;
        }

        if (_onBackButton)
        {
            if (input.Up && _folders.Length > 0)
            {
                _onBackButton = false;
                _selectedIndex = Math.Min(_folders.Length - 1, (GridRows - 1) * GridColumns);
            }
            if (input.Confirm)
            {
                SelectedFolder = null;
                BeginExit();
            }
            return;
        }

        // Grid navigation
        int col = _selectedIndex % GridColumns;
        int row = _selectedIndex / GridColumns;

        if (input.Left && col > 0)
            _selectedIndex--;
        if (input.Right && col < GridColumns - 1 && _selectedIndex + 1 < _folders.Length)
            _selectedIndex++;
        if (input.Up && row > 0)
            _selectedIndex -= GridColumns;
        if (input.Down)
        {
            int nextIdx = _selectedIndex + GridColumns;
            if (nextIdx < _folders.Length)
                _selectedIndex = nextIdx;
            else
                _onBackButton = true;
        }

        if (input.Confirm && _selectedIndex < _folders.Length)
        {
            SelectedFolder = _folders[_selectedIndex];
            BeginExit();
        }
    }

    private void BeginExit()
    {
        _phase = Phase.FadeOut;
        _fadeTimer = 0f;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel,
                     KermFontRenderer? fontRenderer, KermFont? font,
                     SpriteFont fallbackFont, int screenWidth, int screenHeight)
    {
        var fullRect = new Rectangle(0, 0, screenWidth, screenHeight);

        // Gradient background
        UIStyle.DrawTripleGradient(sb, pixel, fullRect, GradTop, GradMid, GradBot);

        // Title
        DrawText(sb, fontRenderer, fallbackFont, "SELECT CHARACTER",
            new Vector2(Padding, Padding - 4), Color.White, 3);

        // Grid area
        int gridX = Padding;
        int gridY = Padding + 40;
        int gridW = screenWidth - Padding * 2;
        int gridH = screenHeight - gridY - Padding - BottomSectionHeight;
        int cardW = (gridW - CardSpacing * (GridColumns - 1)) / GridColumns;
        int cardH = (gridH - CardSpacing * (GridRows - 1)) / GridRows;

        if (_cardRects.Length != _folders.Length)
            _cardRects = new Rectangle[_folders.Length];

        for (int i = 0; i < _folders.Length; i++)
        {
            int c = i % GridColumns;
            int r = i / GridColumns;
            int cx = gridX + c * (cardW + CardSpacing);
            int cy = gridY + r * (cardH + CardSpacing);
            var cardRect = new Rectangle(cx, cy, cardW, cardH);
            _cardRects[i] = cardRect;

            bool selected = !_onBackButton && i == _selectedIndex;
            sb.Draw(pixel, cardRect, selected ? CardFillSelected : CardFill);

            if (selected)
                DrawBorder(sb, pixel, cardRect, 2, CardBorder);

            // Character name centered in card
            string name = i < _names.Length ? _names[i] : _folders[i];
            var namePos = new Vector2(
                cx + cardW / 2 - name.Length * 5,
                cy + cardH / 2 - 8);
            DrawText(sb, fontRenderer, fallbackFont, name, namePos, Color.White, 3);

            // Folder name below (smaller)
            var folderPos = new Vector2(cx + 8, cy + cardH - 24);
            DrawText(sb, fontRenderer, fallbackFont, _folders[i],
                folderPos, new Color(160, 160, 180), 2);
        }

        // Back button
        int backW = 120;
        int backH = 40;
        int backX = screenWidth - backW - Padding;
        int backY = screenHeight - BottomSectionHeight + (BottomSectionHeight - backH) / 2;
        _backRect = new Rectangle(backX, backY, backW, backH);

        sb.Draw(pixel, _backRect, _onBackButton ? BackButtonSelected : BackButtonNormal);
        if (_onBackButton)
            DrawBorder(sb, pixel, _backRect, 2, CardBorder);

        DrawText(sb, fontRenderer, fallbackFont, "Back",
            new Vector2(backX + backW / 2 - 24, backY + 8), Color.White, 3);

        // Fade overlay
        float fadeAlpha = _phase switch
        {
            Phase.FadeIn => 1f - _fadeTimer / FadeDuration,
            Phase.FadeOut => _fadeTimer / FadeDuration,
            _ => 0f
        };
        if (fadeAlpha > 0f)
            sb.Draw(pixel, fullRect, Color.Black * fadeAlpha);
    }

    private static void DrawText(SpriteBatch sb, KermFontRenderer? fontRenderer,
                                  SpriteFont fallbackFont, string text,
                                  Vector2 position, Color color, int scale)
    {
        if (fontRenderer != null)
            fontRenderer.DrawString(sb, text, position, scale, color);
        else
            sb.DrawString(fallbackFont, text, position, color);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, int thickness, Color color)
    {
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }
}
