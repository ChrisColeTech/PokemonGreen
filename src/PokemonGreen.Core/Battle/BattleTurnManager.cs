using System;
using System.Collections.Generic;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Simple state machine managing battle turns.
/// Queues messages and HP changes, then advances through phases.
/// </summary>
public class BattleTurnManager
{
    public enum TurnPhase
    {
        Idle,           // Waiting for player to pick Fight
        PlayerAttack,   // Showing player's attack message, applying damage
        FoeAttack,      // Showing foe's attack message, applying damage
        TurnEnd,        // Check faint, decide next turn or battle over
        BattleOver      // Victory/defeat message shown
    }

    private readonly BattlePokemon _ally;
    private readonly BattlePokemon _foe;
    private readonly Action<string, Action?> _showMessage;
    private readonly Action _returnToMainMenu;
    private readonly Action _exitBattle;
    private readonly Action _hideMenu;

    public TurnPhase Phase { get; private set; } = TurnPhase.Idle;

    private int _playerMoveIndex;
    private readonly Random _rng = new();

    public BattleTurnManager(
        BattlePokemon ally,
        BattlePokemon foe,
        Action<string, Action?> showMessage,
        Action hideMenu,
        Action returnToMainMenu,
        Action exitBattle)
    {
        _ally = ally;
        _foe = foe;
        _showMessage = showMessage;
        _hideMenu = hideMenu;
        _returnToMainMenu = returnToMainMenu;
        _exitBattle = exitBattle;
    }

    /// <summary>Start a turn after the player selects a move.</summary>
    public void StartTurn(int moveIndex)
    {
        _playerMoveIndex = moveIndex;
        _hideMenu();
        Phase = TurnPhase.PlayerAttack;
        ExecutePlayerAttack();
    }

    private void ExecutePlayerAttack()
    {
        var bm = _ally.Moves[_playerMoveIndex];
        var moveData = MoveRegistry.GetMove(bm.MoveId);
        string moveName = moveData?.Name ?? "???";

        if (moveData != null && moveData.Power > 0)
        {
            int damage = CalculateDamage(moveData, _ally.Level);
            _foe.ApplyDamage(damage);
            _showMessage($"{_ally.Nickname} used {moveName}!", () => AfterPlayerAttack());
        }
        else
        {
            // Status move — no damage
            _showMessage($"{_ally.Nickname} used {moveName}!", () => AfterPlayerAttack());
        }
    }

    private void AfterPlayerAttack()
    {
        if (_foe.IsFainted)
        {
            Phase = TurnPhase.BattleOver;
            _showMessage($"Wild {_foe.Nickname} fainted!", () =>
            {
                _showMessage("You win!", () => _exitBattle());
            });
            return;
        }

        Phase = TurnPhase.FoeAttack;
        ExecuteFoeAttack();
    }

    private void ExecuteFoeAttack()
    {
        // Pick a random foe move that has PP
        var usable = new List<int>();
        for (int i = 0; i < _foe.Moves.Length; i++)
        {
            if (_foe.Moves[i].CurrentPP > 0)
                usable.Add(i);
        }

        if (usable.Count == 0)
        {
            // Foe has no moves with PP — struggle equivalent
            _showMessage($"Wild {_foe.Nickname} has no moves left!", () => AfterFoeAttack());
            return;
        }

        int foeIdx = usable[_rng.Next(usable.Count)];
        var foeBm = _foe.Moves[foeIdx];
        foeBm.CurrentPP--;
        var foeMove = MoveRegistry.GetMove(foeBm.MoveId);
        string foeName = foeMove?.Name ?? "???";

        if (foeMove != null && foeMove.Power > 0)
        {
            int damage = CalculateDamage(foeMove, _foe.Level);
            _ally.ApplyDamage(damage);
            _showMessage($"Wild {_foe.Nickname} used {foeName}!", () => AfterFoeAttack());
        }
        else
        {
            _showMessage($"Wild {_foe.Nickname} used {foeName}!", () => AfterFoeAttack());
        }
    }

    private void AfterFoeAttack()
    {
        if (_ally.IsFainted)
        {
            Phase = TurnPhase.BattleOver;
            _showMessage($"{_ally.Nickname} fainted!", () =>
            {
                _showMessage("You blacked out!", () => _exitBattle());
            });
            return;
        }

        Phase = TurnPhase.Idle;
        _returnToMainMenu();
    }

    /// <summary>Simplified damage formula.</summary>
    private int CalculateDamage(MoveData move, int level)
    {
        int damage = (move.Power * level / 5 + 2) / 3;
        // Add a small random factor (+/- 15%)
        float roll = 0.85f + (float)_rng.NextDouble() * 0.15f;
        damage = (int)(damage * roll);
        return Math.Max(1, damage);
    }
}
