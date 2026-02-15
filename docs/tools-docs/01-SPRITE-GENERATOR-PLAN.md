# 01 - Sprite Generator Implementation Plan

## Goal

Build a stable, maintainable sprite generator tool in `tools/sprite-generator` using React + Vite + TypeScript + Tailwind for the frontend and Fastify for the backend API.

We will execute in two major stages:

1. Build the **core web UI** (layout, controls, preview canvas, gallery).
2. Implement the **backend generation API** with proper service architecture.

---

## Principles

- Keep UI and generation logic separated from day one.
- Prefer typed models and explicit state transitions.
- All sprite generation should be deterministic via seeded random.
- Support both SVG preview and PNG export.
- Keep code testable and debuggable (pure functions in services, minimal component logic).

---

## Target Architecture

### App Layers

- **UI Layer (components):** presentational React components for layout, controls, preview canvas, gallery grid, dialogs.
- **State Layer (store):** global app state (selected generator, parameters, preview frames, gallery items, export status).
- **Hook Layer (hooks):** reusable interaction logic (preview generation, file operations, keyboard shortcuts).
- **API Layer (client):** typed API client for Fastify backend communication.
- **Backend Layer (Fastify):** REST API endpoints for generation, conversion, and file operations.
- **Service Layer (services):** pure generation logic (grass, flowers, trees, etc.) and image conversion.
- **Type Layer (types):** shared interfaces for sprite schemas, generator configs, API requests/responses.

### Suggested Folder Structure

```text
tools/sprite-generator/
  src/
    app/
      AppShell.tsx
      routes.tsx
    components/
      layout/
        Header.tsx
        Sidebar.tsx
        MainContent.tsx
      controls/
        GeneratorSelect.tsx
        VariantSelect.tsx
        ParameterControls.tsx
        SeedInput.tsx
        ActionButtons.tsx
      preview/
        PreviewCanvas.tsx
        FrameStrip.tsx
        AnimationPlayer.tsx
      gallery/
        GalleryGrid.tsx
        GalleryItem.tsx
        GalleryFilters.tsx
      common/
        Button.tsx
        Card.tsx
        Modal.tsx
        Spinner.tsx
        Toast.tsx
    hooks/
      useGenerator.ts
      usePreview.ts
      useGallery.ts
      useExport.ts
      useKeyboardShortcuts.ts
    services/
      apiClient.ts
    store/
      index.ts
      generatorSlice.ts
      gallerySlice.ts
      uiSlice.ts
    types/
      generator.ts
      sprite.ts
      api.ts
    utils/
      colorUtils.ts
      svgUtils.ts
  server/
    index.ts
    routes/
      generate.ts
      gallery.ts
      convert.ts
      export.ts
    services/
      grassGenerator.ts
      flowerGenerator.ts
      treeGenerator.ts
      bushGenerator.ts
      seededRandom.ts
      svgRenderer.ts
      pngConverter.ts
    types/
      index.ts
    utils/
      fileUtils.ts
      colorPalettes.ts
  package.json
  vite.config.ts
  tailwind.config.js
  tsconfig.json
```

---

## Feature Baseline (from current `sprite-ui.js`)

This is the source-of-truth feature inventory for the new implementation.

### Current Feature Set to Preserve

- **Generator types:** grass, flower (5 color variants), tree-green, tree-autumn.
- **Preview system:** live preview of all animation frames.
- **Parameters:** type selection, variant selection (for flowers), seed input.
- **Save functionality:** save generated frames to disk as SVG files.
- **Gallery view:** browse all existing sprites in the Assets/Sprites directory.
- **File operations:** click to download individual sprites, clear all sprites.
- **Animation:** frame-by-frame preview with labeled frame numbers.

### Issues Identified in Legacy

- Single-file monolith with inline HTML/CSS/JS as strings.
- Duplicated generator logic between `generate_sprites.js`, `batch_generate.js`, and `sprite-ui.js`.
- No separation between UI, API, and generation logic.
- Hard-coded port and configuration.
- No proper error handling or validation.
- No TypeScript type safety.

---

## Implementation Phases

## Phase 0 - Foundation Setup

### Scope

- Initialize Vite + React + TypeScript project.
- Set up Tailwind CSS pipeline.
- Initialize Fastify backend.
- Establish folder structure and TypeScript strictness.

### Deliverables

- Working Vite dev server for frontend.
- Working Fastify dev server for backend.
- Shared TypeScript types between frontend and backend.
- ESLint and Prettier configuration.

### Acceptance Criteria

- `npm run dev` starts both frontend and backend.
- No TypeScript errors in baseline scaffolding.
- Hot reload works for both frontend and backend.

---

## Phase 1 - Backend API (Services-first)

### Scope

Build the generation and file management API before the UI.

### API Endpoints

```
POST /api/generate
  - Body: { type: string, variant?: string, seed: number, frames: number }
  - Response: { frames: string[], metadata: { frameCount: number, type: string } }

GET /api/gallery
  - Query: { filter?: string, limit?: number, offset?: number }
  - Response: { items: GalleryItem[], total: number }

POST /api/save
  - Body: { type: string, variant?: string, seed: number, baseName?: string }
  - Response: { saved: string[], count: number }

GET /api/sprites/:filename
  - Response: SVG file content

DELETE /api/sprites/:filename
  - Response: { success: boolean }

POST /api/convert
  - Body: { filename: string, format: 'png', scale?: number }
  - Response: { outputFilename: string, downloadUrl: string }

POST /api/clear
  - Response: { deleted: number }
```

### Service Architecture

```typescript
interface GeneratorService {
  type: string;
  variants?: string[];
  defaultFrames: number;
  generate(frame: number, totalFrames: number, variant?: string, seed?: number): string;
}

interface GalleryService {
  list(filter?: string): Promise<GalleryItem[]>;
  get(filename: string): Promise<string>;
  save(filename: string, content: string): Promise<void>;
  delete(filename: string): Promise<void>;
  clear(): Promise<number>;
}

interface ConversionService {
  svgToPng(svgContent: string, scale: number): Promise<Buffer>;
}
```

### Deliverables

- Fastify server with all endpoints implemented.
- Generator services for grass, flower, tree, bush.
- Seeded random utility with deterministic output.
- Gallery service with file system operations.
- PNG conversion service (using sharp or similar).

### Acceptance Criteria

- All endpoints return correct responses.
- Generation is deterministic (same seed = same output).
- File operations work correctly.
- Error responses are properly formatted.

---

## Phase 2 - Core Web UI (UX-first)

### Scope

Build a user-friendly interface before connecting to the API.

### Core Screens / Regions

- **Header:** tool title, theme toggle, help/docs link.
- **Left panel:** generator controls (type, variant, parameters, seed, action buttons).
- **Main area:** preview canvas with animation playback.
- **Right panel:** gallery with filter/search, bulk actions.

### UX Requirements

- Clean, modern dark theme matching map editor aesthetic.
- Responsive layout (desktop first).
- Keyboard shortcuts for common actions.
- Clear feedback for loading, success, error states.
- Smooth animations for frame preview.

### UI Components

```
GeneratorSelect      - Dropdown to choose sprite type
VariantSelect        - Dropdown for color variants (conditional)
ParameterControls    - Sliders/inputs for generator parameters
SeedInput            - Number input with randomize button
ActionButtons        - Preview, Save, Clear buttons

PreviewCanvas        - Large canvas showing current frame
FrameStrip           - Horizontal strip of all frames
AnimationPlayer      - Play/pause controls, speed slider

GalleryGrid          - Grid of existing sprites
GalleryItem          - Individual sprite with preview, name, actions
GalleryFilters       - Search, type filter, sort options

Modal                - Confirmation dialogs
Toast                - Success/error notifications
```

### Deliverables

- Implemented layout components with mock data.
- Shared component library matching design system.
- Theme and spacing system applied consistently.

### Acceptance Criteria

- UI is usable with mock data.
- Navigation and controls are intuitive.
- Responsive at target breakpoints.

---

## Phase 3 - Frontend-Backend Integration

### Scope

Connect React UI to Fastify API with proper state management.

### State Areas

- **Generator state:** selected type, variant, parameters, seed.
- **Preview state:** generated frames, current frame index, isPlaying, playbackSpeed.
- **Gallery state:** items list, filter, sort, selected items.
- **UI state:** loading flags, error messages, toast notifications.
- **Export state:** export progress, download links.

### Data Flow

```
User Action -> Hook -> API Client -> Fastify Endpoint -> Service -> Response
                              |
                              v
                         Zustand Store -> UI Update
```

### Hook Candidates

- `useGenerator` - manages generator type/variant/seed selection
- `usePreview` - handles preview generation and animation state
- `useGallery` - fetches and manages gallery items
- `useExport` - handles save and conversion operations
- `useKeyboardShortcuts` - binds keyboard actions

### Deliverables

- Connected UI with real data.
- Proper loading and error states.
- Optimistic updates where appropriate.

### Acceptance Criteria

- Preview updates when parameters change.
- Gallery reflects file system state.
- Save operations persist files correctly.
- Error states show actionable messages.

---

## Phase 4 - Advanced Features

### Scope

Add power-user features and quality-of-life improvements.

### Features

- **Batch generation:** generate multiple types/variants at once.
- **Custom palettes:** user-defined color palettes for generators.
- **Export options:** PNG at multiple scales, sprite sheets, animated GIF.
- **History:** remember recent generation parameters.
- **Presets:** save/load parameter configurations.
- **Comparison:** side-by-side comparison of different seeds/variants.

### Deliverables

- Batch generation modal with progress indicator.
- Custom palette editor.
- Export options panel with format selection.
- History sidebar with quick restore.
- Preset save/load functionality.

### Acceptance Criteria

- Batch generation completes successfully.
- Custom palettes apply correctly to generators.
- Export produces valid output files.
- History restores exact parameters.

---

## Phase 5 - Hardening and Polish

### Scope

- Error handling and edge cases.
- Performance optimization.
- Documentation and developer experience.

### Deliverables

- Comprehensive error boundaries.
- Loading skeletons and optimistic UI.
- API documentation (OpenAPI/Swagger).
- User guide for the tool.
- Performance optimization (lazy loading, memoization).

### Acceptance Criteria

- No unhandled errors in normal usage.
- Smooth performance with large galleries.
- API is self-documenting.
- New users can learn tool quickly.

---

## Milestone Plan (Execution Order)

1. Initialize project structure and tooling.
2. Implement backend services and API endpoints.
3. Build frontend layout and component library.
4. Connect frontend to backend with state management.
5. Add animation preview and gallery features.
6. Implement export and conversion features.
7. Add batch generation and presets.
8. Polish, test, and document.

---

## Definition of Done

- Frontend is modular (components/hooks/store cleanly separated).
- Backend follows service architecture with pure generation logic.
- All legacy features are implemented with improved UX.
- Generated sprites are deterministic and reproducible.
- Codebase is testable and maintainable.
- Tool integrates seamlessly with existing workflow.

---

## Risks and Mitigations

- **Risk:** SVG-to-PNG conversion complexity.
  - **Mitigation:** Use proven library (sharp) and provide fallback to CLI conversion.
- **Risk:** Generation logic divergence from existing scripts.
  - **Mitigation:** Port logic incrementally with parity tests against known outputs.
- **Risk:** Gallery performance with many sprites.
  - **Mitigation:** Implement virtualization, lazy loading, and thumbnail caching.
- **Risk:** Browser compatibility for canvas/export features.
  - **Mitigation:** Target modern browsers, provide graceful degradation.

---

## Immediate Next Work Items

1. Create `tools/sprite-generator` directory structure.
2. Initialize Vite + React + TypeScript project with Tailwind.
3. Initialize Fastify server with basic routing.
4. Port seeded random utility from existing scripts.
5. Port grass generator as first service with tests.
6. Implement `/api/generate` endpoint for grass type.
7. Build minimal UI to preview grass generation.
8. Validate end-to-end flow before expanding.

---

## API Contract (TypeScript Types)

```typescript
// types/generator.ts
interface GeneratorConfig {
  type: GeneratorType;
  variant?: string;
  seed: number;
  frames: number;
}

type GeneratorType = 'grass' | 'grass-single' | 'flower' | 'tree-green' | 'tree-autumn' | 'bush';

interface GeneratorInfo {
  type: GeneratorType;
  label: string;
  frames: number;
  variants?: { value: string; label: string }[];
  parameters?: ParameterDefinition[];
}

interface ParameterDefinition {
  name: string;
  type: 'number' | 'color' | 'select';
  default: any;
  min?: number;
  max?: number;
  options?: { value: string; label: string }[];
}

// types/api.ts
interface GenerateRequest {
  type: GeneratorType;
  variant?: string;
  seed: number;
  frames?: number;
}

interface GenerateResponse {
  frames: string[];  // SVG content
  metadata: {
    type: GeneratorType;
    variant?: string;
    seed: number;
    frameCount: number;
  };
}

interface GalleryItem {
  filename: string;
  type: string;
  variant?: string;
  frameIndex?: number;
  thumbnail?: string;
  createdAt: Date;
}

interface SaveRequest {
  type: GeneratorType;
  variant?: string;
  seed: number;
  baseName?: string;
}

interface SaveResponse {
  saved: string[];
  count: number;
}

interface ConvertRequest {
  filename: string;
  format: 'png';
  scale?: number;
}

interface ConvertResponse {
  outputFilename: string;
  downloadUrl: string;
}
```

---

## Generator Service Interface

```typescript
// services/types.ts
interface SpriteFrame {
  svg: string;
  width: number;
  height: number;
}

interface GenerationContext {
  seed: number;
  frameIndex: number;
  totalFrames: number;
  variant?: string;
  parameters?: Record<string, any>;
}

interface GeneratorService {
  readonly type: GeneratorType;
  readonly label: string;
  readonly defaultFrames: number;
  readonly variants?: string[];
  readonly parameters?: ParameterDefinition[];
  
  generate(context: GenerationContext): SpriteFrame;
  generateAll(seed: number, frames?: number, variant?: string): SpriteFrame[];
}
```
