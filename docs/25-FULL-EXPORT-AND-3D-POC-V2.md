# 25 - Full GARC Export, Character Browser & 3D POC v2

## Summary

This session completed a full re-export of the overworld GARC (`a/2/0/0`), replaced the broken OhanaCli `_fi` character exports with properly animated SpicaCli exports, built a two-level drill-down character browser, fixed a critical animation reset bug, added collectible cubes with persistence, and wired up the 2D game's UI components (MessageBox, MenuBox, SaveManager) into the 3D POC.

---

## 1. What We Accomplished

### Full GARC Re-Export (372 groups)

Ran SpicaCli `convert` with `--split-model-anims` against the overworld GARC. This produced 372 export groups:

| Type | Count | Description |
|------|-------|-------------|
| `tr0001_00` – `tr0018_00` | 18 | Trainer/player overworld characters (renamed from `group_0002`–`group_0019`) |
| `pm*` | 89 | Overworld Pokemon followers (Pikachu, Charizard, etc.) |
| `group_*` | 251 | Other models — objects, NPCs, props, skeleton-only entries |

Every export has a unified manifest with `semanticName` fields, clip DAEs, and textures. The old broken `_fi` folders (OhanaCli exports with zero clip DAEs) were removed.

### Animation Reset Bug Fix

**Root cause:** `Play("Jump", resetTime: true)` was called every frame while airborne. The `resetTime: true` parameter restarted the animation from frame 0 on each frame, so the jump animation never progressed — it appeared frozen on the first pose.

**Fix:** Track whether the tag actually changed between frames. Only reset time on tag transitions (Idle→Jump, Jump→Walk), not on same-tag continuations.

```csharp
// Before (broken):
_animController.Play(tag, loop: !hasJumpClip, resetTime: hasJumpClip);

// After (fixed):
bool isNewTag = !string.Equals(_animController.ActiveTag, tag, StringComparison.OrdinalIgnoreCase);
_animController.Play(tag, loop: !hasJumpClip, resetTime: isNewTag);
```

### Two-Level Character Browser

Replaced the flat 3x2 grid (which showed only 6 hardcoded characters) with a drill-down browser:

1. **Level 1 — Category Select**: Cards for "Trainers (18)", "Pokemon (89)", "Other Models (251)" etc., auto-grouped by folder prefix
2. **Level 2 — Item Select**: 4x3 paginated grid within the selected category, Q/E for page navigation

Categories are auto-detected from folder names (`tr*` → Trainers, `pm*` → Pokemon, `group*` → Other Models). No hardcoded character list — folders are scanned at startup from `characters/overworld/` by checking for `manifest.json`.

### Auto-Scan Character Discovery

Game1.cs no longer maintains a hardcoded `Characters` array. On startup, it scans the overworld directory:

```csharp
_characters = Directory.GetDirectories(overworldDir)
    .Where(d => File.Exists(Path.Combine(d, "manifest.json")))
    .Select(d => Path.GetFileName(d))
    .OrderBy(f => f)
    .Select(f => (folder: f, name: f))
    .ToArray();
```

Adding new characters is now drop-in: export to the folder, restart the app.

### Collectible Cubes with Full Persistence

- 42 golden cubes scattered across all map areas (center, north, south, east, west)
- Rotation + bobbing animation
- Proximity-based collection (1.5 unit radius)
- MessageBox popup on collection ("You found another cube!")
- Counter panel in upper-left corner using `UIStyle.DrawBattlePanel`
- Persisted via SaveManager slot 99 with story flags (`cube_0`, `cube_1`, etc.)
- Cubes stay collected across app restarts
- Pause menu (Enter key) with Resume / Reset Cubes / Close

### 2D UI Components Reused in 3D

| Component | Source | Usage in 3D POC |
|-----------|--------|-----------------|
| `Core.UI.MessageBox` | 2D battle/dialog system | Cube collection popup, cube reset confirmation |
| `Core.UI.MenuBox` | 2D pause/battle menus | Pause menu with Resume/Reset/Close |
| `Core.Save.SaveManager` | 2D game persistence | Cube collection + character selection persistence |
| `Core.UI.UIStyle` | 2D panel rendering | Cube counter background panel |
| `Core.UI.Fonts.KermFont` | 2D bitmap font system | All text rendering in 3D POC overlays |

### InputState Pagination Support

Added `PageLeft` and `PageRight` to `InputState` struct, mapped to Q/PageUp and E/PageDown. Used by the character browser for page navigation.

---

## 2. What Work Remains

### Character Identification

The 251 `group_*` folders have generic names because SpicaCli can't identify them from the GARC alone. Many are likely named NPCs, objects, or props. A mapping table (GARC entry index → character ID) would allow proper naming.

### Pokemon Animation Slot Mapping

Pokemon overworld models share some slots with trainers (Motion_0=Idle, Motion_1=Walk, Motion_2=Run) but differ beyond that:

| Slot | Trainers | Pokemon |
|------|----------|---------|
| Motion_0 | Idle | Idle |
| Motion_1 | Walk | Walk (untagged) |
| Motion_2 | Run | Run (untagged) |
| Motion_4 | Jump | (absent) |
| Motion_76 | (absent) | Unknown — possibly Jump? (1.5-1.7s) |
| Motion_141 | (absent) | Unknown — long interaction (4.0s) |

Only `Idle` is currently tagged for Pokemon. Walk and Run need to be added to the Pokemon slot map in SpicaCli.

### Pokemon Jump Animation

Pokemon followers DO jump in the original game (e.g., over ledges). Their jump is likely Motion_76 (present in 68 of 89 Pokemon, ~1.7s duration) rather than Motion_4 (which only trainers have). This needs visual confirmation in BgEditor or Blender, then addition to the Pokemon slot map.

### Skeleton-Only Entries

`group_0000` and `group_0001` are tiny (29KB model) with 139-141 clips and no textures. These are likely animation-only skeletons (player character base rigs?). They could be useful for understanding the full animation set but aren't renderable on their own.

---

## 3. Optimizations — Prime Suspects

### 3.1 Lazy Clip Loading
**Impact: High** — `SplitModelAnimationSetLoader.Load()` parses ALL clip DAEs eagerly. Characters with 20+ clips trigger 20 COLLADA XML parses on load. Most clips are never played.

**Approach:** Store clip file paths in the dictionary, defer `ColladaSkeletalLoader.LoadClip()` until first `AnimationController.Play()`. Requires a `LoadClipOnDemand` wrapper or `Lazy<SkeletalAnimationClip>` in the clip dictionaries.

### 3.2 Startup Scan Performance
**Impact: Medium** — The folder scan checks 358 directories for `manifest.json` on every startup. Currently fast (<100ms on SSD) but will slow down as more exports are added.

**Approach:** Cache the scan result to a JSON file. Re-scan only when the directory modification time changes, or add a "Refresh" button to the character browser.

### 3.3 Buffer Reuse in SkinnedDaeModel
**Impact: Medium** — `UpdatePose()` recreates `VertexBuffer`/`IndexBuffer` every frame. Allocating `DynamicVertexBuffer` once during `Load()` and calling `SetData()` per frame would eliminate per-frame GPU allocations.

### 3.4 Texture Cache Across Characters
**Impact: Low-Medium** — Characters share rim textures (`Chara_Rim_1_fi.png`, `Chara_Rim_Black_fi.png`). A static `Dictionary<string, Texture2D>` cache would eliminate redundant disk reads on character switch.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK
- MonoGame 3.8 (pulled via NuGet)
- The overworld GARC has already been exported — character folders are committed to the repo

### Step 1: Build and Run

```bash
cd D:\Projects\PokemonGreen
dotnet build src/PokemonGreen.3D/PokemonGreen.3D.csproj
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

### Step 2: Verify Core Functionality

| Feature | How to Test | Expected |
|---------|------------|----------|
| Character renders | App launches | Trainer on tile map, idle animation playing |
| Walk animation | WASD keys | Character walks, animation plays |
| Run animation | Shift + WASD | Faster movement, run animation |
| Jump animation | Space bar | Character jumps, jump animation plays (not frozen) |
| Cube collection | Walk near a golden cube | MessageBox: "You found another cube!", counter increments |
| Persistence | Restart app | Same cube count, same character selection |
| Character browser | Tab key | Category screen → drill into Trainers/Pokemon |
| Page navigation | Q/E in character browser | Pages through models within category |
| Pause menu | Enter key | Resume / Reset Cubes / Close |

### Step 3: Verify Manifests

Check that any character you load has the unified format:
```bash
cat src/PokemonGreen.Assets/Pokemon3D/characters/overworld/tr0001_00/manifest.json | head -20
```
Should show: `"mode": "split-model-anims"`, clips under `models[0].clips[]`, each with `semanticName`.

### Step 4: Test BgEditor (optional)

```bash
# Terminal 1
cd src/PokemonGreen.BgEditor/backend && npm run dev

# Terminal 2
cd src/PokemonGreen.BgEditor/frontend && npm run dev
```

Open `http://localhost:5173`, navigate to Animations page, paste a character folder path, click Load.

---

## 5. How to Start/Test the APIs

### 3D POC (MonoGame Desktop App)
```bash
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```
No API server — self-contained app. Reads manifests and models directly from disk. Persistence via SQLite at `%LOCALAPPDATA%\PokemonGreen\Saves\save99.db`.

### BgEditor Backend (Fastify, port 3001)
```bash
cd src/PokemonGreen.BgEditor/backend
npm install   # first time
npm run dev
```

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/manifests/read?dir=<path>` | Read manifest.json |
| POST | `/api/manifests/save` | Write updated manifest |
| GET | `/api/manifests?dir=<path>` | Scan tree for manifests |
| GET | `/serve/<token>/<file>` | Serve model/texture files |

### BgEditor Frontend (Vite, port 5173)
```bash
cd src/PokemonGreen.BgEditor/frontend
npm install   # first time
npm run dev
```

### SpicaCli (Export Tool)
```bash
# Re-export overworld GARC (already done — only needed if GARC changes)
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/2/0/0" \
  -o "src/PokemonGreen.Assets/Pokemon3D/characters/overworld" --split-model-anims
```

---

## 6. Known Issues & Strategies

### Issue 1: Pokemon Walk/Run Not Tagged

**Problem:** Only `Motion_0` → `Idle` is tagged for Pokemon. Motion_1 (Walk) and Motion_2 (Run) have the same slot numbers as trainers but aren't mapped because SpicaCli's Pokemon slot map only covers slot 0.

**Strategy A:** Add Pokemon overworld slot mapping to SpicaCli (Motion_0=Idle, Motion_1=Walk, Motion_2=Run). Re-export, or write a script to patch existing manifests.

**Strategy B:** Expand `InferTag` in `SplitModelAnimationSetLoader` to cover slots 0-2 for all manifests. This is a quick runtime fix but masks the real issue.

**Strategy C:** Use BgEditor's auto-tag feature with an expanded slot map to batch-tag Pokemon manifests without re-exporting.

### Issue 2: group_* Folders Have Generic Names

**Problem:** 251 folders named `group_0020` through `group_0368`. Many contain useful models (trainers with IDs like `tr0060_00`, objects like `ob0002_00`) but aren't identifiable without inspecting texture names.

**Strategy A:** Write a rename script that reads each manifest's texture filenames to extract the model ID (e.g., `tr0060_00_Body.tga` → rename folder to `tr0060_00`).

**Strategy B:** Add ID detection to SpicaCli's export logic — extract the model ID from material/texture names during export and use it as the folder name.

**Strategy C:** Leave as-is for now. The character browser categories already separate them from the named exports. Users can browse visually.

### Issue 3: Some Models May Crash on Load

**Problem:** Some `group_*` entries are skeleton-only (no mesh), have unusual bone configurations, or reference missing textures. Loading these in the 3D POC may throw exceptions.

**Strategy A:** Add try/catch around `LoadCharacterModel()` — on failure, show a message box and keep the current character.

**Strategy B:** Pre-validate manifests during the startup scan — skip folders where `model.dae` is missing or under 1KB.

**Strategy C:** Add a "loadable" flag to manifests that the export tool sets based on whether the model has renderable geometry.

### Issue 4: Animation Crossfade

**Problem:** Animation transitions are instant — Walk→Run, Idle→Walk all snap immediately. This looks jarring, especially at lower frame rates.

**Strategy:** Add `CrossFade(fromTag, toTag, duration)` to `AnimationController`. Interpolate between two pose arrays during the transition period. The `SkeletalAnimator` already computes per-bone matrices — blending is just a `Matrix.Lerp` per bone for the crossfade duration.

---

## 7. Architecture

### Current Data Flow

```
ROM GARC (a/2/0/0)
    │
    ▼
SpicaCli --split-model-anims
    │
    ├── tr0001_00/          (18 trainers)
    │   ├── model.dae
    │   ├── clips/*.dae     (16-25 clips each)
    │   ├── textures/*.png
    │   └── manifest.json   (unified format, semantic names)
    │
    ├── pm0025_00/          (89 Pokemon)
    │   └── ...
    │
    └── group_NNNN/         (251 other models)
        └── ...

3D POC Startup:
    Directory.GetDirectories("characters/overworld/")
        → filter: has manifest.json
        → auto-group by prefix (tr/pm/group)
        → CharacterSelectScreen categories

Character Load:
    manifest.json
        → SplitModelAnimationSetLoader.Load()
            → ColladaSkeletalLoader.LoadSkeleton(model.dae)
            → ColladaSkeletalLoader.LoadClip(clips/*.dae) × N
            → ClipsByTag dictionary (semanticName → clip)
        → AnimationController
            → Play("Idle"/"Walk"/"Run"/"Jump")
        → SkinnedDaeModel.UpdatePose(skinMatrices)

Persistence:
    SaveManager (SQLite, slot 99)
        → story_flags: cube_0, cube_1, ...
        → selected_character: tr0001_00
```

### Files Modified This Session

| File | Change |
|------|--------|
| `3D/Game1.cs` | Auto-scan characters, jump animation fix, collectible cubes, persistence, pause menu, message box, character selection saving |
| `Core/UI/Screens/CharacterSelectScreen.cs` | Complete rewrite — two-level drill-down with categories and pagination |
| `Core/UI/InputState.cs` | Added `PageLeft`, `PageRight` properties |
| `Assets/.../overworld/tr0001_00` – `tr0018_00` | Fresh SpicaCli exports replacing broken `_fi` folders |
| `Assets/.../overworld/pm*` | 89 Pokemon overworld models (new) |
| `Assets/.../overworld/group_*` | 251 other models from GARC (new) |

### Quick Wins

1. **Tag Pokemon Walk/Run** — Patch the 89 `pm*` manifests to add `semanticName: "Walk"` for Motion_1 and `"Run"` for Motion_2. A 10-line script, or expand the SpicaCli overworld slot map and re-export.

2. **Rename identifiable group_ folders** — Read texture names from each group's manifest, extract the model ID, rename the folder. Makes browsing much more useful.

3. **Guard against bad models** — Wrap `LoadCharacterModel` in try/catch. Some group_ entries are skeleton-only or malformed. One line of error handling prevents crashes when browsing.

4. **Persist page/category in browser** — Remember the last-viewed category and page so reopening the browser doesn't reset to page 1.

5. **Clip preview in BgEditor** — Render a single frame of each clip as a thumbnail. This is the fastest way to identify what Motion_76 actually does for Pokemon (jump? emote? battle cry?).
