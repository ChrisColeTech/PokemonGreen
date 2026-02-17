#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;
using PokemonGreen.Core.UI.Screens;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Manages all 2D battle UI: menus, message box, overlay stack, info bars.
/// </summary>
public class BattleUIManager
{
    // Rendering deps
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private KermFontRenderer? _kermFontRenderer;
    private KermFont? _kermFont;
    private SpriteFont? _fallbackFont;

    // Menus
    private readonly MessageBox _messageBox = new();
    private readonly MenuBox _mainMenu = new() { Columns = 2 };
    private readonly MenuBox _moveMenu = new() { Columns = 2 };
    private MenuBox _activeMenu = null!;
    private bool _inFightMenu;
    private int _fightGridCol;
    private int _fightGridRow;

    // Overlays
    private readonly Stack<IScreenOverlay> _overlayStack = new();
    private Party? _party;
    private PlayerInventory? _inventory;

    public bool IsOverlayActive => _overlayStack.Count > 0;
    public bool IsMenuActive => _activeMenu.IsActive;
    public Party? Party => _party;

    /// <summary>
    /// Store rendering dependencies. Call once from Initialize.
    /// </summary>
    public void Initialize(SpriteBatch spriteBatch, Texture2D pixel,
        KermFontRenderer? kermFontRenderer, KermFont? kermFont,
        SpriteFont? fallbackFont = null)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _kermFontRenderer = kermFontRenderer;
        _kermFont = kermFont;
        _fallbackFont = fallbackFont;
        _activeMenu = _mainMenu;
    }

    public void SetPartyAndInventory(Party party, PlayerInventory inventory)
    {
        _party = party;
        _inventory = inventory;
    }

    // ── Menu setup ──

    public void SetMainMenuItems(Action onFight, Action onBag, Action onParty, Action onRun)
    {
        _mainMenu.SetItems(
            new MenuItem("Fight", onFight),
            new MenuItem("Bag", onBag),
            new MenuItem("Pokemon", onParty),
            new MenuItem("Run", onRun));
        _mainMenu.Columns = 2;
    }

    public void OpenFightMenu(BattlePokemon? allyPokemon, Action closeFightMenu)
    {
        BuildMoveMenu(allyPokemon, closeFightMenu);

        _mainMenu.SetItems(
            new MenuItem("Back", closeFightMenu),
            new MenuItem("Mega", () => { }),
            new MenuItem("Power", () => { }));
        _mainMenu.Columns = 2;
        _mainMenu.SelectedIndex = -1;

        _activeMenu = _moveMenu;
        _activeMenu.SelectedIndex = 0;
        _activeMenu.IsActive = true;
        _messageBox.Clear();
        _inFightMenu = true;
        _fightGridCol = 0;
        _fightGridRow = 0;
    }

    public void CloseFightMenu(Action<Action, Action, Action, Action> setupMainMenu)
    {
        _inFightMenu = false;
        _activeMenu.IsActive = false;
        _activeMenu = _mainMenu;
        _activeMenu.SelectedIndex = 0;
        _activeMenu.IsActive = true;
        _messageBox.Clear();
        _messageBox.Show("What will you do?");
    }

    public void ReturnToMainMenu(Action<Action, Action, Action, Action> setupMainMenu,
        BattlePokemon? allyPokemon, Action closeFightMenu)
    {
        BuildMoveMenu(allyPokemon, closeFightMenu);
        _inFightMenu = false;
        _activeMenu = _mainMenu;
        _activeMenu.SelectedIndex = 0;
        _activeMenu.IsActive = true;
        _messageBox.Clear();
        _messageBox.Show("What will you do?");
    }

    public void ActivateMenu()
    {
        _activeMenu.IsActive = true;
    }

    public void DeactivateMenu()
    {
        _activeMenu.IsActive = false;
    }

    public void ResetMenuState()
    {
        _inFightMenu = false;
        _activeMenu = _mainMenu;
        _activeMenu.IsActive = false;
        _activeMenu.SelectedIndex = 0;
    }

    // ── Message box ──

    public void ShowMessage(string text, Action? onFinished = null)
    {
        _messageBox.Clear();
        _messageBox.Show(text);
        _messageBox.OnFinished = onFinished;
    }

    public void ClearMessage()
    {
        _messageBox.Clear();
    }

    // ── Overlays ──

    public void OpenBagScreen()
    {
        if (_inventory == null) return;
        PushOverlay(new BagScreen(_inventory));
    }

    public void OpenPartyScreen()
    {
        if (_party == null) return;
        PushOverlay(new PartyScreen(_party, PartyScreenMode.BattleSwitchIn));
    }

    private void PushOverlay(IScreenOverlay overlay)
    {
        _activeMenu.IsActive = false;
        _overlayStack.Push(overlay);
    }

    public void PopOverlay(Action<int> onResult)
    {
        int switchIndex = -1;
        if (_overlayStack.Count > 0)
        {
            var overlay = _overlayStack.Pop();
            if (overlay is PartyScreen ps)
                switchIndex = ps.SelectedSwitchIndex;
        }

        if (_overlayStack.Count == 0)
            onResult(switchIndex);
    }

    public void ClearOverlays()
    {
        _overlayStack.Clear();
    }

    // ── Move menu ──

    private void BuildMoveMenu(BattlePokemon? allyPokemon, Action closeFightMenu)
    {
        if (allyPokemon == null) return;
        var moves = allyPokemon.Moves;
        var items = new MenuItem[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var bm = moves[i];
            var data = MoveRegistry.GetMove(bm.MoveId);
            string name = data?.Name ?? $"Move#{bm.MoveId}";
            bool enabled = bm.CurrentPP > 0;
            int moveIndex = i;
            items[i] = new MenuItem(name, () => SelectMove(allyPokemon, moveIndex), enabled);
        }
        _moveMenu.SetItems(items);
        _moveMenu.OnCancel = closeFightMenu;
    }

    /// <summary>Fired when a move is selected. Set by the orchestrator.</summary>
    public Action<int>? OnMoveSelected { get; set; }

    private void SelectMove(BattlePokemon allyPokemon, int moveIndex)
    {
        var bm = allyPokemon.Moves[moveIndex];
        if (bm.CurrentPP <= 0)
        {
            _messageBox.Clear();
            _messageBox.Show("No PP left for this move!");
            return;
        }
        bm.CurrentPP--;
        OnMoveSelected?.Invoke(moveIndex);
    }

    // ── Update ──

    public void UpdateOverlay(float dt, InputState input)
    {
        var overlay = _overlayStack.Peek();
        overlay.Update(dt, input);
    }

    public bool IsTopOverlayFinished()
    {
        return _overlayStack.Count > 0 && _overlayStack.Peek().IsFinished;
    }

    public void UpdateInput(float dt, InputState input)
    {
        bool confirm = input.Confirm;
        bool anyKey = input.AnyKey;

        if (_activeMenu.IsActive)
        {
            _messageBox.Update(dt, false);
            bool cancel = input.Cancel;

            if (_inFightMenu)
            {
                int newCol = _fightGridCol;
                int newRow = _fightGridRow;
                if (input.Right) newCol++;
                if (input.Left) newCol--;
                if (input.Down) newRow++;
                if (input.Up) newRow--;
                newCol = Math.Clamp(newCol, 0, 3);
                newRow = Math.Clamp(newRow, 0, 1);

                bool valid;
                if (newCol < 2)
                {
                    int idx = newRow * 2 + newCol;
                    valid = idx < _moveMenu.Items.Count;
                }
                else
                {
                    int idx = newRow * 2 + (newCol - 2);
                    valid = idx < _mainMenu.Items.Count;
                }
                if (valid)
                {
                    _fightGridCol = newCol;
                    _fightGridRow = newRow;
                }

                if (_fightGridCol < 2)
                {
                    _activeMenu = _moveMenu;
                    _moveMenu.SelectedIndex = _fightGridRow * 2 + _fightGridCol;
                    _mainMenu.SelectedIndex = -1;
                }
                else
                {
                    _activeMenu = _mainMenu;
                    _mainMenu.SelectedIndex = _fightGridRow * 2 + (_fightGridCol - 2);
                    _moveMenu.SelectedIndex = -1;
                }

                _activeMenu.Update(
                    left: false, right: false, up: false, down: false,
                    confirm: confirm, cancel: cancel,
                    mousePosition: Point.Zero, mouseClicked: false);
            }
            else
            {
                _activeMenu.Update(
                    left: input.Left, right: input.Right,
                    up: input.Up, down: input.Down,
                    confirm: confirm, cancel: cancel,
                    mousePosition: Point.Zero, mouseClicked: false);
            }
        }
        else if (_messageBox.IsActive)
        {
            _messageBox.Update(dt, anyKey);
        }
    }

    // ── Draw ──

    public void DrawUI(int fontScale, int w, int h,
        bool introComplete, bool allySentOut,
        BattlePokemon? allyPokemon, BattlePokemon? foePokemon)
    {
        if (_spriteBatch == null || _pixel == null) return;

        // Bottom panel fills the entire width, ~1/4 of screen height
        int panelH = h / 4;
        int panelY = h - panelH;

        // Menu takes right 1/4, message/moves fill left 3/4
        int menuW = w / 4;
        int menuX = w - menuW;
        int leftW = w - menuW;

        // Info bars
        int infoBarW = w / 3;
        int infoMargin = 20;
        int infoFontScale = fontScale;
        if (introComplete && foePokemon != null)
            BattleInfoBar.DrawFoeBar(_spriteBatch, _pixel, _kermFontRenderer, _fallbackFont!,
                new Rectangle(infoMargin, infoMargin, infoBarW, panelH / 2), foePokemon, infoFontScale);

        if (allySentOut && allyPokemon != null)
            BattleInfoBar.DrawAllyBar(_spriteBatch, _pixel, _kermFontRenderer, _fallbackFont!,
                new Rectangle(w - infoBarW, panelY - panelH / 2 - 14, infoBarW, panelH / 2 + 10),
                allyPokemon, allyPokemon.EXPPercent, infoFontScale);

        if (_activeMenu.IsActive)
        {
            if (_inFightMenu)
            {
                if (_kermFontRenderer != null && _kermFont != null)
                {
                    _moveMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(0, panelY, leftW, panelH), fontScale);
                    _mainMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH), fontScale);
                }
                else if (_fallbackFont != null)
                {
                    _moveMenu.Draw(_spriteBatch, _fallbackFont, _pixel,
                        new Rectangle(0, panelY, leftW, panelH));
                    _mainMenu.Draw(_spriteBatch, _fallbackFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH));
                }
            }
            else
            {
                if (_kermFontRenderer != null && _kermFont != null)
                {
                    _activeMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH), fontScale);
                    _messageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                        new Rectangle(0, panelY, leftW, panelH), fontScale);
                }
                else if (_fallbackFont != null)
                {
                    _activeMenu.Draw(_spriteBatch, _fallbackFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH));
                    _messageBox.Draw(_spriteBatch, _fallbackFont, _pixel,
                        new Rectangle(0, panelY, leftW, panelH));
                }
            }
        }
        else if (_messageBox.IsActive)
        {
            // Full-width message box (intro / battle messages)
            if (_kermFontRenderer != null)
                _messageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                    new Rectangle(0, panelY, w, panelH), fontScale);
            else if (_fallbackFont != null)
                _messageBox.Draw(_spriteBatch, _fallbackFont, _pixel,
                    new Rectangle(0, panelY, w, panelH));
        }

        // Draw overlay on top
        if (_overlayStack.Count > 0)
        {
            var overlay = _overlayStack.Peek();
            overlay.Draw(_spriteBatch, _pixel, _kermFontRenderer, _kermFont,
                _fallbackFont!, w, h, fontScale);
        }
    }
}
