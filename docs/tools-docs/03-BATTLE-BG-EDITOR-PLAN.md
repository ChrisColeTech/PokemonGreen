# 03 - Battle Background Editor Implementation Plan

## Goal

Build a lightweight web tool in `tools/battle-bg-editor` using React + Vite + TypeScript that loads Collada (.dae) battle background models, displays a live 3D preview, allows color/texture adjustments (hue, saturation, brightness, tint), and exports the modified textures back as PNGs ready for the game engine.

This is a **client-only** tool — no backend required. All processing happens in the browser using Three.js and canvas APIs.

---

## Principles

- Keep it simple — this is a texture color editor, not a full 3D modeling tool.
- All rendering and export happens client-side (no server needed).
- The tool works directly with the `Assets/BattleBG/` folder structure.
- Preview should match the in-game camera angle so what-you-see-is-what-you-get.
- Non-destructive workflow: original textures are never overwritten unless the user explicitly exports.

---

## Context: Asset Structure

The game's battle backgrounds live in `Assets/BattleBG/` with this structure:

```text
Assets/BattleBG/
  Grass/
    Grass.dae              # Background model (sky + ground)
    batt_field01.png       # Ground texture
    batt_sky01.png         # Sky texture
  Cave/
    Cave.dae
    batt_field05.png
    batt_sky05.png
  Dark/
    Dark.dae
    batt_field15.png
    batt_sky15.png
  PlatformGrassAlly/
    GrassAlly.dae          # Ally platform model
    batt_bk01.png          # Platform backdrop
    batt_stage06.png       # Platform surface
  PlatformGrassFoe/
    GrassFoe.dae
    batt_bk01.png
    batt_stage06.png
  PlatformTallGrassAlly/
    TallGrassAlly.dae
    batt_bk01.png
    batt_grass23a.png
    batt_leaf23a.png
    batt_leafb23a.png
    batt_sdw23.png
    batt_stage23a.png
  PlatformTallGrassFoe/
    TallGrassFoe.dae
    (same textures as TallGrassAlly)
  PlatformCaveAlly/
    CaveAlly.dae
    batt_bk05.png
    batt_rock05.png
    batt_stage05.png
    batt_stone05.png
    batt_stoneb05.png
  PlatformCaveFoe/
    CaveFoe.dae
    (same textures as CaveAlly)
  PlatformDark/
    Dark.dae               # Single model used for both ally and foe
    batt_bk15.png
    batt_stage15.png
```

Each `.dae` file references its textures by filename. The tool edits the PNG textures; the `.dae` geometry stays unchanged.

---

## What We Need

### Primary Use Case

The user has existing battle backgrounds (Grass, Cave, Dark) but needs **new color variants** for different terrains (e.g. a fire/lava theme). Rather than hand-editing PNGs in GIMP, they want to:

1. Load an existing background set (e.g. Dark)
2. See a 3D preview matching the in-game camera
3. Adjust colors (hue shift, saturation, brightness, tint overlay)
4. Adjust per-texture independently (sky vs ground vs platform)
5. Export the modified textures to a new folder (e.g. `Fire/`)
6. Optionally create the full folder structure (bg + ally platform + foe platform)

### Non-Goals

- Geometry editing (no vertex manipulation)
- UV mapping changes
- Creating new models from scratch
- Animation or rigging

---

## Target Architecture

### Tech Stack

- **Three.js** — 3D scene rendering, Collada loading
  - `ColladaLoader` from `three/examples/jsm/loaders/ColladaLoader`
- **React + Vite + TypeScript** — UI framework
- **Zustand** — lightweight state management
- **Canvas API** — texture color manipulation (pixel-level hue/sat/brightness)

### App Layers

- **UI Layer (components):** layout shell, 3D viewport, texture list, color controls, export panel.
- **State Layer (store):** loaded scene data, per-texture adjustments, export settings.
- **Service Layer (services):** Three.js scene management, texture processing, file I/O.
- **Type Layer (types):** shared interfaces for scene definitions, texture adjustments, export configs.

### Folder Structure

```text
tools/battle-bg-editor/
  index.html
  src/
    App.tsx
    main.tsx
    components/
      layout/
        Header.tsx
        Sidebar.tsx
        Viewport.tsx
      controls/
        SceneSelector.tsx       # Pick which BG set to load
        TextureList.tsx         # List of textures in the loaded model
        ColorControls.tsx       # Hue/Sat/Brightness/Tint sliders
        ExportPanel.tsx         # Export name, folder, download
      preview/
        BattlePreview.tsx       # Three.js canvas wrapper
    services/
      sceneService.ts           # Three.js scene setup, model loading
      textureProcessor.ts       # Canvas-based hue/sat/brightness manipulation
      exportService.ts          # Package modified textures for download
    store/
      editorStore.ts            # Zustand store
    types/
      editor.ts                 # Type definitions
    utils/
      colorUtils.ts             # HSL conversion, pixel manipulation helpers
  package.json
  vite.config.ts
  tsconfig.json
```

---

## Feature Specification

### Scene Selector

- Dropdown or card grid listing available background sets:
  - **Backgrounds:** Grass, Cave, Dark
  - **Platforms:** GrassAlly, GrassFoe, TallGrassAlly, TallGrassFoe, CaveAlly, CaveFoe, Dark (shared)
- User can load individual models or a **composite scene** (background + ally platform + foe platform) to preview the full battle layout.
- Models are loaded from the local file system via drag-and-drop or a file picker (since this is a dev tool, we can also hardcode paths relative to the project root).

### 3D Viewport

- Three.js renderer showing the loaded .dae model with its textures applied.
- Camera matches the in-game battle camera:
  - Position: `(7, 7, 15)` (default full view)
  - Yaw: `-22deg`, Pitch: `13deg`
  - FOV: `26deg`
- Optional orbit controls for inspection (toggleable, defaults to game camera).
- Background color: dark gray or configurable.

### Texture List

- After loading a model, display all textures found in the scene as a list/grid of thumbnails.
- Each texture shows:
  - Thumbnail preview (original and modified side-by-side)
  - Filename (e.g. `batt_field01.png`)
  - Current adjustment values
- Clicking a texture selects it for editing.

### Color Controls

Per-texture adjustment sliders:

| Control | Range | Default | Description |
|---------|-------|---------|-------------|
| Hue Shift | -180 to +180 | 0 | Rotate hue wheel |
| Saturation | -100 to +100 | 0 | Desaturate or oversaturate |
| Brightness | -100 to +100 | 0 | Darken or lighten |
| Tint Color | Color picker | none | Overlay a color tint |
| Tint Strength | 0 to 100 | 0 | How much tint to apply |

- Changes apply in real-time to the 3D preview via canvas texture updates.
- "Apply to All" button to apply the same adjustments to every texture in the scene.
- "Reset" button to revert a texture to its original state.

### Export Panel

- **Export Name:** text input for the new variant name (e.g. "Fire")
- **Export Mode:**
  - **Textures Only:** download a zip of the modified PNGs
  - **Full Scene:** download zip with the original .dae + modified PNGs (ready to drop into `Assets/BattleBG/`)
- **Folder Structure Preview:** show what the output folder will look like
- **Download** button to trigger the export

### Composite Preview Mode

For the full battle scene preview, the tool loads and positions models the same way the game does:

- Background at origin
- Foe platform at `(0, -0.20, -15)`
- Ally platform at `(0, -0.20, 3)`

This lets the user see exactly how the background, sky, and platforms look together with their color changes before exporting.

---

## Implementation Phases

### Phase 0 — Foundation Setup

**Scope:** Initialize project, install dependencies, verify Three.js Collada loading works.

**Deliverables:**
- Vite + React + TypeScript project scaffolded
- Three.js installed with ColladaLoader
- Basic canvas rendering a test .dae file
- Zustand store skeleton

**Acceptance Criteria:**
- `npm run dev` starts the tool
- A .dae file from `Assets/BattleBG/` renders in the viewport
- Textures display correctly on the model

---

### Phase 1 — Scene Loading and Preview

**Scope:** Build the scene selector, 3D viewport with game camera, and texture extraction.

**Deliverables:**
- Scene selector UI (file picker / drag-and-drop for .dae + textures)
- Three.js viewport with correct battle camera (yaw/pitch/FOV matching game)
- Texture extraction from loaded scene (list all texture maps)
- Texture thumbnail display

**Acceptance Criteria:**
- Can load any .dae from `Assets/BattleBG/`
- 3D preview matches the in-game view angle
- All textures referenced by the model are listed with thumbnails

---

### Phase 2 — Color Adjustment

**Scope:** Implement per-texture color manipulation with live preview.

**Deliverables:**
- Color control sliders (hue, saturation, brightness, tint)
- Canvas-based texture processing pipeline
- Real-time Three.js texture updates when sliders change
- "Apply to All" and "Reset" actions
- Original vs modified thumbnail comparison

**Acceptance Criteria:**
- Moving hue slider visibly shifts texture colors in the 3D preview
- Changes are non-destructive (original texture data preserved)
- Performance is smooth (no visible lag when adjusting sliders)

---

### Phase 3 — Export

**Scope:** Package modified textures for download.

**Deliverables:**
- Export name input and mode selection
- Modified PNG generation via canvas `toBlob()`
- Zip packaging (using JSZip or similar)
- Folder structure matching `Assets/BattleBG/` convention
- Download trigger

**Acceptance Criteria:**
- Exported PNGs load correctly when placed in `Assets/BattleBG/`
- Folder structure matches existing convention
- Exported textures visually match the 3D preview

---

### Phase 4 — Composite Scene and Polish

**Scope:** Full battle scene preview (bg + platforms), presets, and UX polish.

**Deliverables:**
- Composite scene mode loading bg + ally + foe platforms at correct positions
- Preset system (save/load named color adjustment configurations)
- Undo/redo for adjustments
- Dark theme matching other tools (map editor aesthetic)

**Acceptance Criteria:**
- Composite preview shows the full battle scene as it appears in-game
- Presets persist across sessions (localStorage)
- UI is intuitive for quick color iteration

---

## Key Library Choices

| Need | Library | Why |
|------|---------|-----|
| 3D Rendering | `three` | Industry standard, has ColladaLoader |
| Collada Import | `three/examples/jsm/loaders/ColladaLoader` | Built-in Three.js addon |
| React + Three.js | Raw Three.js (no R3F) | Simpler for a single viewport tool; R3F is overkill here |
| State | `zustand` | Same as map editor, lightweight |
| Zip Export | `jszip` | Well-maintained, browser-native |
| File Save | `file-saver` | Cross-browser download triggering |

---

## Color Processing Algorithm

The texture processor works on raw pixel data via canvas `getImageData()` / `putImageData()`:

```
For each pixel (r, g, b, a):
  1. Convert RGB → HSL
  2. Apply hue shift:   h = (h + hueShift) % 360
  3. Apply saturation:  s = clamp(s + satAdj, 0, 1)
  4. Apply brightness:  l = clamp(l + brightAdj, 0, 1)
  5. Convert HSL → RGB
  6. Apply tint:        r = lerp(r, tintR, tintStrength)
                        g = lerp(g, tintG, tintStrength)
                        b = lerp(b, tintB, tintStrength)
  7. Write back (r, g, b, a)  // alpha unchanged
```

This runs on an offscreen canvas. For performance, we debounce slider changes and process on requestAnimationFrame.

---

## State Shape

```typescript
interface TextureAdjustment {
  hueShift: number;       // -180 to 180
  saturation: number;     // -100 to 100
  brightness: number;     // -100 to 100
  tintColor: string;      // hex color
  tintStrength: number;   // 0 to 100
}

interface LoadedTexture {
  name: string;
  originalData: ImageData;       // preserved for non-destructive editing
  originalDataUrl: string;       // thumbnail of original
  modifiedDataUrl: string;       // thumbnail of modified (updated on change)
  threeTexture: THREE.Texture;   // live reference in the scene
  adjustment: TextureAdjustment;
}

interface EditorState {
  // Scene
  sceneName: string | null;
  textures: LoadedTexture[];
  selectedTextureIndex: number;

  // Export
  exportName: string;
  exportMode: 'textures' | 'full';

  // Actions
  loadScene: (files: FileList) => Promise<void>;
  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => void;
  applyToAll: () => void;
  resetTexture: (index: number) => void;
  resetAll: () => void;
  exportScene: () => Promise<void>;
}
```

---

## Risks and Mitigations

- **Risk:** ColladaLoader may not resolve texture paths when loading from drag-and-drop.
  - **Mitigation:** Manually pair textures by matching filenames from the dropped file list. The .dae references textures by name — we intercept the texture load and supply the dropped PNG files.

- **Risk:** Canvas pixel manipulation could be slow for large textures.
  - **Mitigation:** Battle BG textures are small (likely 256x256 or 512x512 given DS/3DS origins). Debounce slider input and process on rAF.

- **Risk:** Three.js texture updates may cause flickering.
  - **Mitigation:** Update the texture's `image` property and set `needsUpdate = true` — Three.js handles the GPU upload on next frame.

- **Risk:** Exported PNGs may have color space differences from originals.
  - **Mitigation:** Use canvas with consistent color space settings. Validate by loading exported textures back into the tool.

---

## Immediate Next Work Items

1. Create `tools/battle-bg-editor` directory with Vite + React + TS scaffold.
2. Install `three`, `zustand`, `jszip`, `file-saver`.
3. Build minimal viewport that loads a .dae file via drag-and-drop.
4. Extract and display texture list from loaded model.
5. Implement hue/saturation/brightness sliders with canvas processing.
6. Wire processed textures back to Three.js for live preview.
7. Implement export as zip with modified PNGs.
8. Add composite scene mode (bg + platforms at game positions).

---

## Definition of Done

- Tool loads any .dae from `Assets/BattleBG/` and displays it correctly.
- Color adjustments (hue, saturation, brightness, tint) update the 3D preview in real-time.
- Exported textures work in the game engine without modification.
- UI matches the dark-theme aesthetic of existing tools.
- Tool is self-contained with no backend dependency.
