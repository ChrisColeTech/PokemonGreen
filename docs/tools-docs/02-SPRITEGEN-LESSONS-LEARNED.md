# Lessons Learned & Handoff: SpriteGen Tool

**Date:** 2026-02-14
**Sprint:** SpriteGen scaffolding, backend services, frontend shell, UI redesign

---

## 1. What We Accomplished

### Backend API (Complete — Fully Functional)
- Fastify server on port 3001 with CORS, 10 REST endpoints
- 4 generator services: grass, flower (5 color variants), tree (green/autumn), bush
- Seeded random (mulberry32) — deterministic: same seed always produces same sprites
- Gallery file operations: list, read, save, delete, clear all SVGs
- Import endpoint: accepts frames with base name, writes `{name}.svg` + `{name}_{n}.svg`
- Service registry pattern — generators register themselves, looked up by type string
- All TypeScript, compiles clean with strict mode

### Frontend Shell (Complete — UI Only, Not Wired)
- React 19 + Vite 6 + Tailwind 4 (via `@tailwindcss/vite` zero-config plugin)
- VS Code-inspired dark theme matching MapEditor: `#1e1e1e` bg, `#2d2d2d` borders, `#094771` accent
- Lucide-react icons, system fonts, collapsible panels with section headers
- MenuBar with File/Edit/View dropdowns (same pattern as MapEditor's `MenuBar.tsx`)
- Left sidebar: Generate section (type, variant, seed, frames, speed, actions) + Import section (drop zone, file list, naming)
- Center: preview canvas with pixel grid overlay, animation player (prev/play/next), frame strip
- Right sidebar: gallery panel with search filter, sprite grid, collapse toggle
- Zustand store with generator, preview, import, gallery, and UI state
- Typed API client with all 6 methods (generate, getGenerators, getGallery, save, deleteSprite, clearAll)

### Architecture Decisions
- Backend-first approach: services are pure functions, isolated from HTTP layer
- Frontend has API client ready but deliberately not wired yet — shell-first strategy
- Two-panel sidebar (generate + import) on single screen, no tab switching
- Import uses drag-and-drop zone in sidebar, not a separate screen

---

## 2. What Work Remains

### Phase 3: Frontend-Backend Wiring (Approved Plan Exists)
9 files need changes, none require more than ~30 lines:

1. **`store/index.ts`** — add `showGrid` boolean + `toggleGrid()` action
2. **`apiClient.ts`** — add `importFrames()` method for `POST /api/import`
3. **`GeneratorControls.tsx`** — wire Generate Preview → `api.generate()` → `setFrames()`, wire Save → `api.save()`, disable Export
4. **`Header.tsx`** — wire File menu (Save Frames, Clear All), View > Show Pixel Grid → `toggleGrid()`, disable Export PNG/Undo/Redo/Reset Zoom
5. **`ImportDropZone.tsx`** — wire Save Frames → read files as text → `api.importFrames()` → clear + refresh gallery
6. **`GalleryPanel.tsx`** — `useEffect` to load gallery from API on mount/filter change, click item → load SVG into preview
7. **`AnimationPlayer.tsx`** — `setInterval` loop when `isPlaying`, advance frames at `playbackSpeed` ms, cleanup on pause/unmount
8. **`PreviewCanvas.tsx`** — read `showGrid` from store, conditionally render grid overlay
9. **`App.tsx`** — error bar below header, reads `error` from store, click to dismiss

### Phase 4: Advanced Features (Future)
- Batch generation modal
- Custom palette editor
- PNG export (needs `/api/convert` endpoint + sharp library)
- Sprite sheet assembly
- History / preset save-load
- `grass-single` generator (type defined, no implementation)

### Phase 5: Polish (Future)
- Error boundaries
- Loading skeletons
- Keyboard shortcuts (Ctrl+G generate, Ctrl+S save, Ctrl+N new)
- Performance: gallery virtualization for large sprite counts

---

## 3. Optimizations — Where to Begin

### 1. Gallery Thumbnail Caching
The `GalleryItem` type has a `thumbnail` field that's never populated. Currently every gallery item shows the filename as text. Populating thumbnails on the backend (read first ~bytes of SVG or generate a data URI) would make the gallery actually useful visually. The `listSprites()` function in `fileUtils.ts` is the place to add this.

### 2. Generator Parameter System
The type system defines `ParameterDefinition` (number/color/select with min/max/options) and `GeneratorService.parameters`, but no generator exposes any parameters. This is the biggest quick win for user control — e.g. blade count for grass, petal count for flowers, berry probability for bushes. The UI already has the `GeneratorControls` section where these would slot in as dynamic inputs.

### 3. Frame Count Defaults per Generator
All 4 generators hardcode `defaultFrames: 3` but the legacy tools used 5-6 frames. The frontend slider goes 1-12 but the backend ignores `frames` parameter in the save endpoint (always uses generator default). The `/api/save` route should pass the frame count through, and each generator should have a more accurate default.

### 4. SVG Size Consistency
Generators produce 16x16 SVGs but the legacy system used 32x32. The preview canvas applies `scale(12)` which works for 16px but would need adjustment for 32px. If sprites need to match existing assets, the generators should match the legacy viewport size.

---

## 4. Step-by-Step: Get App Fully Working

```
1. cd D:/Projects/PokemonGreen/src/PokemonGreen.SpriteGen
2. npm run install:all          # installs backend + frontend deps
3. npm run dev                  # starts both servers via concurrently

   — Backend: http://localhost:3001
   — Frontend: http://localhost:5173 (proxies /api → 3001)

4. Execute the 9-file wiring plan (see Section 2 above)
5. After wiring, verify:
   a. Select type + seed → click Generate Preview → frames render
   b. Click Play → frames auto-cycle
   c. Click Save → gallery populates
   d. Click gallery item → loads into preview
   e. Drop images into Import → set name → Save Frames → appears in gallery
   f. File > Clear All Sprites → gallery empties
   g. View > Show Pixel Grid → toggles overlay
   h. Stop backend → trigger action → error bar appears
```

---

## 5. How to Start and Test the API

### Start
```bash
cd D:/Projects/PokemonGreen/src/PokemonGreen.SpriteGen
npm run dev                    # both servers
npm run dev:backend            # backend only
npm run dev:frontend           # frontend only
```

### Test Endpoints Manually
```bash
# Health check
curl http://localhost:3001/api/health

# List generators
curl http://localhost:3001/api/generators

# Generate grass frames
curl -X POST http://localhost:3001/api/generate \
  -H "Content-Type: application/json" \
  -d '{"type":"grass","seed":12345,"frames":3}'

# Generate flower with variant
curl -X POST http://localhost:3001/api/generate \
  -H "Content-Type: application/json" \
  -d '{"type":"flower","seed":42,"variant":"yellow","frames":5}'

# Save to disk
curl -X POST http://localhost:3001/api/save \
  -H "Content-Type: application/json" \
  -d '{"type":"grass","seed":12345}'

# List gallery
curl http://localhost:3001/api/gallery

# Get specific sprite
curl http://localhost:3001/api/sprites/grass-12345-frame0.svg

# Delete specific sprite
curl -X DELETE http://localhost:3001/api/sprites/grass-12345-frame0.svg

# Clear all
curl -X POST http://localhost:3001/api/clear

# Import frames
curl -X POST http://localhost:3001/api/import \
  -H "Content-Type: application/json" \
  -d '{"baseName":"tile_test","frames":[{"index":0,"content":"<svg>...</svg>","filename":"test.svg"}]}'
```

### Sprites Output Directory
Default: `{backend cwd}/../../PokemonGreen.Assets/Sprites/`
Override: set `SPRITES_DIR` environment variable

---

## 6. Known Issues & Strategies

### Issue 1: API Client is 100% Unused
Every `api.*` method exists but no component calls any of them. The Zustand store has actions (`setLoading`, `setError`, `setGalleryItems`) that are never invoked.

**Strategy:** The wiring plan (Section 2) addresses this systematically — each component gets an async handler that calls the API, wraps in loading state, and catches errors. Work through the 9 files in order.

### Issue 2: Animation Playback is Broken
The play/pause button toggles `isPlaying` in the store, but nothing reads that state to advance frames. `playbackSpeed` is set by the slider but consumed by nothing.

**Strategy:** Add a `useEffect` in `AnimationPlayer.tsx` with `setInterval`. Use `useStore.getState().currentFrame` inside the interval callback to avoid stale closure. Clean up on `isPlaying` change or unmount. This is ~15 lines.

### Issue 3: Gallery Never Populates
`setGalleryItems()` exists but is never called. The gallery always shows "No sprites saved" even if the backend has files.

**Strategy:** Add a `useEffect` in `GalleryPanel.tsx` that calls `api.getGallery()` on mount and whenever the filter changes. Also refresh after any save/import/clear operation — the simplest way is to call `api.getGallery()` at the end of every mutating action in the header and controls.

### Issue 4: No Error Feedback
The store has `error: string | null` and `setError()` but no component reads the error state. If the backend is down or returns an error, the user sees nothing.

**Strategy:** Add a thin error bar in `App.tsx` between the header and main content. Conditionally render when `error` is non-null, click to dismiss. Style it like the MapEditor's inline feedback (red-tinted background, border, small text). This is ~8 lines of JSX.

---

## 7. Architecture & Features

### Current Architecture
```
PokemonGreen.SpriteGen/
├── backend/                    # Fastify + TypeScript
│   └── src/
│       ├── index.ts            # Server entry, registers routes + CORS
│       ├── routes/
│       │   ├── generate.ts     # GET /api/generators, POST /api/generate
│       │   ├── gallery.ts      # GET /api/gallery, GET/DELETE /api/sprites/:file, POST /api/save, POST /api/clear
│       │   └── import.ts       # POST /api/import
│       ├── services/
│       │   ├── registry.ts     # Generator lookup map
│       │   ├── grassGenerator.ts
│       │   ├── flowerGenerator.ts
│       │   ├── treeGenerator.ts
│       │   └── bushGenerator.ts
│       ├── types/index.ts      # Shared types (GeneratorType, SpriteFrame, API contracts)
│       └── utils/
│           ├── seededRandom.ts  # Mulberry32 PRNG
│           ├── colorPalettes.ts # Game Boy-era color palettes
│           └── fileUtils.ts     # Sprite file CRUD
├── frontend/                   # React 19 + Vite 6 + Tailwind 4
│   └── src/
│       ├── App.tsx             # Root layout: Header + Sidebar + MainContent + GalleryPanel
│       ├── store/index.ts      # Zustand: generator, preview, import, gallery, UI state
│       ├── services/apiClient.ts
│       ├── types/generator.ts
│       └── components/
│           ├── layout/         # Header (MenuBar), Sidebar, MainContent
│           ├── controls/       # GeneratorControls (type, variant, seed, frames, speed, buttons)
│           ├── preview/        # PreviewCanvas, AnimationPlayer, FrameStrip
│           ├── gallery/        # GalleryPanel (search, grid, collapse)
│           ├── import/         # ImportDropZone (drop area, file list, naming, save)
│           └── common/         # SectionHeader
└── package.json                # Root: concurrently runs both servers
```

### Quick Wins After Wiring
1. **Gallery auto-refresh after save** — call `api.getGallery()` at the end of save/import handlers (~2 lines each)
2. **Keyboard shortcuts** — `useEffect` in App.tsx listening for Ctrl+G (generate), Ctrl+S (save), Ctrl+N (new) (~15 lines)
3. **Gallery item delete** — right-click context menu or hover X button on gallery items, calls `api.deleteSprite()` (~10 lines)
4. **Seed from gallery** — parse seed from filename when clicking a gallery item, populate it back into the seed input for reproducibility

### Future Architecture Considerations
- **Shared types package** — backend `types/index.ts` and frontend `types/generator.ts` are partially duplicated. A shared package or code generation step would prevent drift.
- **WebSocket for live preview** — currently generate is request/response. A WebSocket could stream frames as they generate for large frame counts.
- **Plugin generators** — the registry pattern supports it. New generators just implement `GeneratorService` and call `register()`. A plugin folder could auto-discover generators.

---

## File Inventory (30 source files)

### Backend (13 files)
- `backend/src/index.ts`
- `backend/src/routes/generate.ts`
- `backend/src/routes/gallery.ts`
- `backend/src/routes/import.ts`
- `backend/src/services/registry.ts`
- `backend/src/services/grassGenerator.ts`
- `backend/src/services/flowerGenerator.ts`
- `backend/src/services/treeGenerator.ts`
- `backend/src/services/bushGenerator.ts`
- `backend/src/types/index.ts`
- `backend/src/utils/seededRandom.ts`
- `backend/src/utils/colorPalettes.ts`
- `backend/src/utils/fileUtils.ts`

### Frontend (15 files)
- `frontend/src/App.tsx`
- `frontend/src/main.tsx`
- `frontend/src/index.css`
- `frontend/src/vite-env.d.ts`
- `frontend/src/store/index.ts`
- `frontend/src/services/apiClient.ts`
- `frontend/src/types/generator.ts`
- `frontend/src/components/layout/Header.tsx`
- `frontend/src/components/layout/Sidebar.tsx`
- `frontend/src/components/layout/MainContent.tsx`
- `frontend/src/components/controls/GeneratorControls.tsx`
- `frontend/src/components/preview/PreviewCanvas.tsx`
- `frontend/src/components/preview/AnimationPlayer.tsx`
- `frontend/src/components/preview/FrameStrip.tsx`
- `frontend/src/components/gallery/GalleryPanel.tsx`
- `frontend/src/components/import/ImportDropZone.tsx`
- `frontend/src/components/common/SectionHeader.tsx`

### Config (5 files)
- `package.json` (root — concurrently)
- `backend/package.json`
- `backend/tsconfig.json`
- `frontend/package.json`
- `frontend/vite.config.ts`
- `frontend/tsconfig.json`
- `frontend/tsconfig.node.json`
