#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Encapsulates the entire battle state machine: entering/exiting battle,
/// turn management, input handling, and 2D battle UI drawing.
/// </summary>
public class BattleScreen3D
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly KermFontRenderer? _kermFontRenderer;
    private readonly KermFont? _kermFont;
    private readonly GraphicsDevice _graphicsDevice;

    private BattlePokemon? _allyPokemon;
    private BattlePokemon? _foePokemon;
    private BattleTurnManager? _battleTurnManager;
    private readonly Core.UI.MessageBox _battleMessageBox = new();
    private readonly MenuBox _battleMainMenu = new() { Columns = 2 };
    private readonly MenuBox _battleMoveMenu = new() { Columns = 2 };
    private MenuBox _activeBattleMenu = null!;
    private bool _inFightMenu;

    /// <summary>True while the battle screen is active.</summary>
    public bool InBattle { get; private set; }

    public BattleScreen3D(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch,
        Texture2D pixel, KermFontRenderer? kermFontRenderer, KermFont? kermFont)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _kermFontRenderer = kermFontRenderer;
        _kermFont = kermFont;

        _activeBattleMenu = _battleMainMenu;
        _battleMoveMenu.OnCancel = CloseFightMenu;
    }

    /// <summary>Start a new battle encounter.</summary>
    public void EnterBattle()
    {
        InBattle = true;

        _allyPokemon = BattlePokemon.CreateTestAlly();
        _foePokemon = BattlePokemon.CreateTestFoe();

        _battleTurnManager = new BattleTurnManager(
            _allyPokemon, _foePokemon,
            showMessage: (msg, onDone) =>
            {
                _battleMessageBox.Clear();
                _battleMessageBox.Show(msg);
                _battleMessageBox.OnFinished = onDone;
            },
            hideMenu: () => _activeBattleMenu.IsActive = false,
            returnToMainMenu: () =>
            {
                BuildMoveMenu();
                _inFightMenu = false;
                _battleMainMenu.SetItems(
                    new MenuItem("Fight", OpenFightMenu),
                    new MenuItem("Bag", () => { }),
                    new MenuItem("Pokemon", () => { }),
                    new MenuItem("Run", ExitBattle));
                _battleMainMenu.Columns = 2;
                _activeBattleMenu = _battleMainMenu;
                _activeBattleMenu.SelectedIndex = 0;
                _activeBattleMenu.IsActive = true;
                _battleMessageBox.Clear();
                _battleMessageBox.Show("What will you do?");
            },
            exitBattle: ExitBattle);

        _inFightMenu = false;
        _battleMainMenu.SetItems(
            new MenuItem("Fight", OpenFightMenu),
            new MenuItem("Bag", () => { }),
            new MenuItem("Pokemon", () => { }),
            new MenuItem("Run", ExitBattle));
        _battleMainMenu.Columns = 2;
        _activeBattleMenu = _battleMainMenu;
        _activeBattleMenu.IsActive = false;
        _activeBattleMenu.SelectedIndex = 0;
        _battleMessageBox.Clear();
        _battleMessageBox.Show($"Wild {_foePokemon.Nickname.ToUpper()} appeared!");
        _battleMessageBox.OnFinished = () =>
        {
            _battleMessageBox.Show($"Go! {_allyPokemon.Nickname.ToUpper()}!");
            _battleMessageBox.OnFinished = () =>
            {
                _activeBattleMenu.IsActive = true;
                _battleMessageBox.Clear();
                _battleMessageBox.Show("What will you do?");
            };
        };
    }

    /// <summary>End the current battle and return to overworld.</summary>
    public void ExitBattle()
    {
        InBattle = false;
        _allyPokemon = null;
        _foePokemon = null;
        _battleTurnManager = null;
        _activeBattleMenu.IsActive = false;
        _battleMessageBox.Clear();
    }

    /// <summary>Process battle input and state each frame.</summary>
    public void Update(float dt, InputState uiInput)
    {
        _allyPokemon?.UpdateDisplayHP(dt);
        _foePokemon?.UpdateDisplayHP(dt);

        bool confirm = uiInput.Confirm;

        if (_activeBattleMenu.IsActive)
        {
            _battleMessageBox.Update(dt, false);
            _activeBattleMenu.Update(
                left: uiInput.Left, right: uiInput.Right,
                up: uiInput.Up, down: uiInput.Down,
                confirm: confirm,
                cancel: uiInput.Cancel,
                mousePosition: Point.Zero,
                mouseClicked: false);
        }
        else if (_battleMessageBox.IsActive)
        {
            _battleMessageBox.Update(dt, confirm);
        }
    }

    /// <summary>
    /// Draw the full battle UI in virtual coordinate space.
    /// Caller should set up SpriteBatch with a transform matrix before calling.
    /// </summary>
    public void Draw(int fontScale = 5, int virtualW = 1280, int virtualH = 960)
    {
        int w = virtualW;
        int h = virtualH;
        int panelH = 192;
        int panelY = h - panelH - 32;
        int infoBarW = 512;

        // Dark background (placeholder for 3D battle scene)
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(24, 24, 40));

        // Foe info bar (top-left)
        if (_foePokemon != null)
            BattleInfoBar.DrawFoeBar(_spriteBatch, _pixel, _kermFontRenderer, null!,
                new Rectangle(32, 32, infoBarW, 128), _foePokemon, fontScale);

        // Ally info bar (right, above menu)
        if (_allyPokemon != null)
            BattleInfoBar.DrawAllyBar(_spriteBatch, _pixel, _kermFontRenderer, null!,
                new Rectangle(w - infoBarW - 32, panelY - 180, infoBarW, 172), _allyPokemon, _allyPokemon.EXPPercent, fontScale);

        // Menu / message box
        if (_activeBattleMenu.IsActive && _kermFontRenderer != null && _kermFont != null)
        {
            int menuW = 448;
            int menuX = w - menuW - 32;

            if (_inFightMenu)
            {
                int moveGridW = w - menuW - 96;
                _battleMoveMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                    new Rectangle(32, panelY, moveGridW, panelH), fontScale);
                _battleMainMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                    new Rectangle(menuX, panelY, menuW, panelH), fontScale);
            }
            else
            {
                int textBoxW = w - menuW - 96;
                _activeBattleMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                    new Rectangle(menuX, panelY, menuW, panelH), fontScale);
                _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                    new Rectangle(32, panelY, textBoxW, panelH), fontScale);
            }
        }
        else if (_battleMessageBox.IsActive && _kermFontRenderer != null)
        {
            int boxW = w - 64;
            _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                new Rectangle(32, panelY, boxW, panelH), fontScale);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void OpenFightMenu()
    {
        _inFightMenu = true;
        BuildMoveMenu();
        _activeBattleMenu = _battleMoveMenu;
        _activeBattleMenu.SelectedIndex = 0;
        _activeBattleMenu.IsActive = true;
    }

    private void CloseFightMenu()
    {
        _inFightMenu = false;
        _activeBattleMenu = _battleMainMenu;
        _activeBattleMenu.SelectedIndex = 0;
        _activeBattleMenu.IsActive = true;
    }

    private void BuildMoveMenu()
    {
        if (_allyPokemon == null) return;
        var items = new List<MenuItem>();
        for (int i = 0; i < _allyPokemon.Moves.Length; i++)
        {
            var bm = _allyPokemon.Moves[i];
            var moveData = MoveRegistry.GetMove(bm.MoveId);
            string name = moveData?.Name ?? "???";
            int idx = i;
            items.Add(new MenuItem(name, () => UseMove(idx)));
        }
        _battleMoveMenu.SetItems(items.ToArray());
        _battleMoveMenu.Columns = 2;
    }

    private void UseMove(int moveIndex)
    {
        if (_allyPokemon == null || moveIndex >= _allyPokemon.Moves.Length) return;
        var bm = _allyPokemon.Moves[moveIndex];
        if (bm.CurrentPP <= 0) return;
        bm.CurrentPP--;
        _battleTurnManager?.StartTurn(moveIndex);
    }
}
