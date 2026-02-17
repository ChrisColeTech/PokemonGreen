# 27 - Battle Screen Decomposition, Camera, Deploy/Recall & Switch-In Handoff

## Summary

This session decomposed the monolithic `BattleScreen3D.cs` (~886 lines) into focused components, rewrote the camera system, added deploy/recall scale animations, wired up EXP rewards, implemented Pokemon party switch-in, and improved the battle UI layout. Battle exit transition issues were later fixed with a dedicated fade-state overhaul (documented below).

---

## 1. What We Accomplished

### BattleScreen3D Decomposition

Split the monolithic 886-line `BattleScreen3D.cs` into 4 focused files:

| File | Responsibility | Lines |
|------|---------------|-------|
| `BattleScreen3D.cs` | Thin orchestrator — wires camera, renderer, UI, turn manager | ~330 |
| `BattleCamera.cs` | Camera position, zoom animation, view matrix | ~100 |
| `BattleSceneRenderer.cs` | 3D rendering: backgrounds, platforms, Pokemon models, placeholder cubes | ~370 |
| `BattleUIManager.cs` | All 2D UI: menus, message box, overlay stack, info bars | ~390 |

### Camera Rewrite

Replaced confusing quaternion-based camera with simple const-driven `Matrix.CreateLookAt`:

```
BattleCamera.cs constants (top of file):
  LookAt:   (0, 2, -6)    — where camera points at
  Start:    (0, 7, 8)     — close-up position at battle start
  End:      (7, 7, 15)    — pulled-back final position
  Duration: 0.5s          — zoom-out time
  ArcHeight: 3            — vertical arc during zoom curve
  FOV:      26 degrees    — field of view
```

Camera starts at StartPosition, curves to EndPosition with ease-out and a vertical sine arc. All values are `private const` at the top of the file for easy tweaking.

### Deploy/Recall Scale Animations

Pokemon cubes/models grow and shrink for deploy and recall:

- `BattleSceneRenderer.DeployFoe()` — instant (scale jumps to 1)
- `BattleSceneRenderer.DeployAlly()` — animated grow (0 → 1)
- `BattleSceneRenderer.RecallAlly()` / `RecallFoe()` — animated shrink (1 → 0)
- `DeploySpeed = 1.2` units/sec (configurable in BattleSceneRenderer.cs)
- Foe appears instantly at battle start; ally grows in on "Go! X!"
- Scale state resets in `ClearPokemonModels()` between battles

### Random Placeholder Cubes

`RandomizePlaceholderCubes()` generates random cube sizes each battle for testing camera framing:
- Foe: 1.5 to 12.0 units
- Ally: 1.0 to 6.0 units

### Pokemon Party Switch-In

Full switch-in flow implemented:

1. Player selects "Pokemon" from battle menu → Party screen overlay opens
2. Player selects a Pokemon → action popup shows (Switch In / Summary / Cancel)
3. "Switch In" immediately sets `SelectedSwitchIndex` and exits overlay
4. `BattleScreen3D.BeginSwitch()`: "Come back, X!" message + recall animation play simultaneously
5. When recall animation completes (`IsAllyRecalled`): creates new `BattlePokemon` from party
6. "Go! Y!" message + deploy animation play simultaneously
7. When deploy animation completes (`IsAllyDeployed`): menu reappears with "What will you do?"

Key changes:
- `PartyScreen.ConfirmSwitchIn()` sets `SelectedSwitchIndex` and exits
- `BattleUIManager.PopOverlay()` extracts `SelectedSwitchIndex` from `PartyScreen`
- `BattleScreen3D` uses `SwitchPhase` enum (None → Recalling → Deploying → None)
- `BattleTurnManager.SetAlly()` swaps the active ally mid-battle
- Mouse controls removed from PartyScreen — keyboard only

### EXP System Wiring

The EXP infrastructure was already built (GrowthRate, EXP calculator, StatCalculator, BattleTurnManager.AwardEXP). The bug was that `EnterBattle()` used `CreateTestAlly()` which passed `Source=null`, so EXP was never awarded. Fixed by using `PartyPokemon.Create()` + `BattlePokemon.FromParty()` to ensure `Source` is set.

### Battle UI Layout Improvements

- Bottom panel: proportional `h/4` height, fills edge-to-edge
- Menu: `w/4` width, right-aligned
- Info bars: `w/3` width, foe top-left, ally right-aligned above panel
- `GetBattleUITransform()` in Game1: scales by height but fills full window width (no letterboxing)
- MenuBox: scale-aware arrows (`fontScale*3`) and text indent (`fontScale*6`)
- MessageBox: scale-aware padding (`6*fontScale`)
- BattleInfoBar: larger padding (16), HP bar (12px), EXP bar (6px)

### Two-Phase Battle Exit

Split `ExitBattle()` into two steps:
- `ExitBattle()` — fires `OnBattleExit` callback (triggers transition)
- `CleanupBattle()` — called by Game1 after fade completes, sets `InBattle=false` and clears state

---

## 2. What Work Remains

### Battle Exit Transition Bug (RESOLVED)

**Original Symptoms:** Exiting battle could produce a blue flash, black-frame pop, horizontal-line artifact, or overworld popping in before fade timing finished.

**Final Fix Implemented (Game1 transition overhaul):**
1. Replaced single `FadeOutBattle` phase with two explicit phases:
   - `FadeToBlackFromBattle`
   - `FadeFromBlackToOverworld`
2. `OnBattleExit` now starts fade at alpha `0` and ramps to `1` over `FadeDuration`.
3. At full black (`alpha = 1`), call `CleanupBattle()` exactly once, then switch to fade-in phase.
4. During `FadeFromBlackToOverworld`, allow normal overworld update so world/camera are live while alpha drops `1 -> 0`.
5. Rendering clear-color rule:
   - Use `Color.Black` only during `FadeToBlackFromBattle`
   - Use normal overworld clear color during `FadeFromBlackToOverworld`

This sequence removes scene-pop artifacts and keeps exit transitions smooth and deterministic.

### Other Remaining Work

- **Victory/defeat transitions** — same exit transition issue applies to "You win!" and "You blacked out!"
- **Bag screen in battle** — opens but items don't apply (use item → effect → close overlay)
- **Switch-in during enemy turn** — currently switch only happens from menu, not forced on faint
- **Wild Pokemon catch** — Pokeball item from bag → catch calculation → add to party
- **Trainer battles** — NPC encounter system, can't run, switch on faint
- **Move PP sync** — PP deducted in `BattleUIManager.SelectMove` but not synced back to `PartyPokemon.MovePPs`

---

## 3. Optimization Suspects

### 1. Transition State Machine in Game1

The `TransitionPhase` enum and `UpdateTransition` are fragile — phases interact with `InBattle`, draw order, and input blocking in non-obvious ways. Each phase has different rules about what draws and what updates.

**Optimization:** Extract a `TransitionManager` class that owns its own render target. The transition draws to the render target, then composites over whatever the main scene draws. This decouples "what fades" from "what's happening underneath."

### 2. Battle Exit Lifecycle

`ExitBattle()` and `CleanupBattle()` are split across two classes (BattleScreen3D and Game1) with implicit ordering. The battle must stay drawable during the fade but shouldn't receive input.

**Optimization:** Add an `ExitingBattle` state to `BattleScreen3D` (between `InBattle=true` and `InBattle=false`). During this state, the battle draws but ignores input. Game1 checks `IsExiting` instead of managing cleanup timing.

### 3. Redundant Menu Rebuilding

`SetupMenuCallbacks()` and `ReturnToMainMenu()` are called repeatedly — after overlays close, after switches, after turns. Each call creates new `MenuItem` arrays and closures.

**Optimization:** Build menu items once at battle start. Store callbacks as fields. `ReturnToMainMenu` just re-activates the existing menu without rebuilding.

### 4. Placeholder Cube Vertex Buffer

A new vertex buffer and index buffer are created per `LoadBattleModels` call but never disposed.

**Optimization:** Implement `IDisposable` on `BattleSceneRenderer` and dispose GPU resources on cleanup.

---

## 4. Step-by-Step: Get App Fully Working

1. **Build:** `dotnet build src/PokemonGreen.3D/PokemonGreen.3D.csproj`
2. **Run:** `dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj`
3. **Debug battle mode:** Set `DebugStartInBattle = true` in `Game1.cs` (line ~90) to skip overworld and launch directly into battle
4. **Test fight:** Select Fight → pick a move → watch HP drain and EXP award
5. **Test switch:** Select Pokemon → pick a party member → Switch In → watch recall/deploy animations
6. **Test run:** Select Run → "You got away safely!" → battle exits (transition has blue flash bug)
7. **Normal mode:** Set `DebugStartInBattle = false` → walk on encounter tiles → random battles trigger with flash+fade transition

### Prerequisites
- .NET 8 SDK
- MonoGame 3.8.2 (NuGet restored automatically)
- Battle background models in `assets/BattleBG/` (Grass, TallGrass, Cave, Dark)
- Species data in `assets/data/species.json`
- Move data in `assets/data/moves.json`

---

## 5. How to Start/Test

### Quick Start
```bash
cd D:\Projects\PokemonGreen
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

### Debug Controls
- **Arrow keys / WASD** — navigate menus
- **Enter / Space / E** — confirm
- **Escape / Backspace / B** — cancel / back
- **Walk on grass** — triggers random encounter (15% chance per 0.4s of walking)

### Test Matrix
| Feature | How to Test | Expected |
|---------|------------|----------|
| Battle entry | Walk on encounter tile | Flash → fade → battle screen |
| Fight | Fight → Scratch | Damage dealt, HP bar drains |
| EXP | Defeat foe | "X gained Y EXP!" message, EXP bar fills |
| Party switch | Pokemon → Charmander → Switch In | Recall shrink → "Go! Pidgey!" → deploy grow |
| Run | Run | "Got away safely!" → fade to overworld |
| Foe appears | Battle start | Foe cube appears instantly (no grow) |
| Ally appears | After camera zoom | Ally cube grows in |

---

## 6. Known Issues & Strategies

### Issue 1: Battle Exit Blue Flash (Resolved)

**Implemented approach:** single-responsibility exit phases in `Game1`:
- fade battle to black (`FadeToBlackFromBattle`)
- cleanup/swap scene at full black
- fade from black to overworld (`FadeFromBlackToOverworld`)
- allow overworld update during fade-in
- black clear only in fade-to-black phase

**Why this worked:** it fully separates scene swap timing from visual fade timing and avoids drawing stale or uninitialized frames during handoff.

### Issue 2: Camera Reset Takes Height Parameters

`BattleCamera.Reset()` still accepts `foeHeight` and `allyHeight` and uses `ZoomPerUnit` to offset the start position Z. This dynamic math was supposed to be removed but was re-added by the user for start position only. Verify this is the desired behavior or simplify further.

### Issue 3: PartyScreen Mouse Code Removed

Mouse controls were fully removed from PartyScreen. If mouse support is needed later, it should be re-added with proper hit-testing that doesn't interfere with keyboard navigation (e.g., only update selection on actual mouse movement, not position).

### Issue 4: Move PP Not Synced Back

`BattleUIManager.SelectMove()` decrements `BattleMove.CurrentPP` but this is never written back to `PartyPokemon.MovePPs`. After battle, PP changes are lost.

---

## 7. Architecture & Quick Wins

### New Architecture

```
Game1 (MonoGame)
  ├── BattleScreen3D (orchestrator)
  │     ├── BattleCamera         — position, zoom, view matrix
  │     ├── BattleSceneRenderer  — 3D: backgrounds, platforms, models, cubes
  │     ├── BattleUIManager      — 2D: menus, messages, overlays, info bars
  │     └── BattleTurnManager    — turn state machine, damage, EXP
  ├── Transition system          — flash/fade in Game1.UpdateTransition
  └── Overworld                  — tiles, player, NPCs, encounters
```

### Key Patterns

- **Overlay Stack:** `BattleUIManager` pushes `IScreenOverlay` instances (PartyScreen, BagScreen). Topmost overlay receives input. `IsFinished` signals pop.
- **Deploy/Recall Animation:** `_displayScale` lerps toward `_targetScale` at `DeploySpeed`. Draw multiplies model/cube size by displayScale. Hidden when scale < 0.001.
- **Switch State Machine:** `SwitchPhase` enum drives the recall→deploy sequence. Each phase waits for animation completion (`IsAllyRecalled` / `IsAllyDeployed`).
- **Two-Step Exit Handoff:** fade battle to black, cleanup at full black, then fade in overworld.

### Quick Wins

1. **Completed:** Transition state-machine split for battle exit (`FadeToBlackFromBattle` + `FadeFromBlackToOverworld`) with swap at full black.

2. **Sync PP back to party:** In `BattlePokemon.SyncToParty()`, copy `Moves[i].CurrentPP` back to `Source.MovePPs[i]`. ~5 lines.

3. **Forced switch on faint:** In `BattleTurnManager`, when ally faints, instead of "You blacked out!", open party screen. If no non-fainted Pokemon, then black out. Reuses existing switch-in flow.

4. **Remove CornflowerBlue:** Change `GraphicsDevice.Clear(Color.CornflowerBlue)` to `Color.Black` in overworld Draw. Eliminates all clear-color flash issues.

---

## File Reference

| File | Path |
|------|------|
| BattleScreen3D | `src/PokemonGreen.Core/Battle/BattleScreen3D.cs` |
| BattleCamera | `src/PokemonGreen.Core/Battle/BattleCamera.cs` |
| BattleSceneRenderer | `src/PokemonGreen.Core/Battle/BattleSceneRenderer.cs` |
| BattleUIManager | `src/PokemonGreen.Core/Battle/BattleUIManager.cs` |
| BattleTurnManager | `src/PokemonGreen.Core/Battle/BattleTurnManager.cs` |
| BattlePokemon | `src/PokemonGreen.Core/Battle/BattlePokemon.cs` |
| PartyScreen | `src/PokemonGreen.Core/UI/Screens/PartyScreen.cs` |
| MenuBox | `src/PokemonGreen.Core/UI/MenuBox.cs` |
| MessageBox | `src/PokemonGreen.Core/UI/MessageBox.cs` |
| BattleInfoBar | `src/PokemonGreen.Core/Battle/BattleInfoBar.cs` |
| Game1 (3D) | `src/PokemonGreen.3D/Game1.cs` |
| Game1 (2D) | `src/PokemonGreen/Game1.cs` |
