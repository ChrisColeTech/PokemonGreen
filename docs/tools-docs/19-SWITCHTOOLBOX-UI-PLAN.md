# 19 - SwitchToolbox UI Implementation Plan

## Goal

Build a lightweight Electron + React UI for the SwitchToolbox CLI that lets users select an archive, browse its folder contents, choose which models to export, configure output settings, and monitor export progress in real-time. Modelled after the [ExtractionPage](file:///D:/Projects/PokemonGreen/src/PokemonGreen.BgEditor/frontend/src/pages/ExtractionPage.tsx) in BgEditor.

---

## Principles

- **Thin UI shell.** The CLI does the heavy lifting — the UI is just I/O selection, process control, and log streaming.
- **Process isolation.** Each model export runs as a separate child process (crash isolation for native Rust panics in tegra_swizzle).
- **Resume-safe.** Skip already-exported models, just like `export_all.ps1`.
- **Match existing design system.** Dark theme, card-based layout, same color palette as BgEditor extraction page.

---

## Architecture

### Data Flow

```
React UI  ──HTTP/SSE──▶  Fastify API  ──child_process──▶  CLI EXE
   ▲                        │                                │
   │                        │ SSE stream                     │ stdout/exit code
   └────────────────────────┘                                │
                                                             ▼
Electron (thin shell)                                   model.dae + textures/
   └─ BrowserWindow loads React dev server or build
   └─ IPC: browseFolder only (native dialog)
```

### Project Structure

```
SwitchToolboxCli.Api/          ← Fastify backend (TypeScript)
  src/
    index.ts                   ← Server bootstrap (port 3100)
    routes/
      archive.ts               ← GET /api/archive/scan — list models
      export.ts                ← POST /api/export/start, DELETE /api/export/cancel
      progress.ts              ← GET /api/export/progress — SSE stream
      filesystem.ts            ← GET /api/fs/browse — folder dialog fallback
    services/
      archiveScanner.ts        ← Spawn CLI --list, parse + group output
      exportOrchestrator.ts    ← Parallel child process pool, progress tracking
      processPool.ts           ← Manages N concurrent CLI processes
    types/
      index.ts                 ← Shared types (FolderGroup, ExportConfig, etc.)
  package.json
  tsconfig.json

SwitchToolboxCli.Electron/     ← Electron shell (minimal)
  main.js                      ← BrowserWindow + browseFolder IPC only
  preload.js                   ← contextBridge for browseFolder
  package.json

SwitchToolboxCli.React/        ← React frontend (Vite + TypeScript)
  src/
    pages/
      ExportPage.tsx           ← Main export UI (single page app)
    components/
      ArchiveSelector.tsx      ← Archive path input + Browse button
      FolderFilter.tsx         ← Multi-select checklist for folder categories
      OutputConfig.tsx         ← Output dir, export mode, parallelism slider
      ExportControls.tsx       ← Start/Stop/Reset buttons
      ProgressPanel.tsx        ← Progress bar, stats grid, phase label
      LogViewer.tsx            ← Scrolling terminal-style log
      ResultsTable.tsx         ← Collapsible per-model status rows
    store/
      exportStore.ts           ← Zustand store for all export state
    services/
      apiClient.ts             ← Typed fetch wrappers for Fastify endpoints
    types/
      export.ts                ← Frontend-specific types
  package.json
  vite.config.ts
  tsconfig.json
```

---

## Archive Folder Browsing (Without Extraction)

> **Answer to "Is there a way to analyse the file without extracting?"**

**Yes.** The CLI already has `--list` which reads the archive descriptor (273K files, ~30s) and lists every `.trmdl` path without extracting anything. We parse that output to populate the folder dropdown.

### How It Works

1. User sets archive path → React calls `GET /api/archive/scan?path=<arcPath>`
2. Fastify spawns: `SwitchToolboxCli.App.exe --arc <path> --list`
3. Parse stdout lines matching `^\s+(.+\.trmdl)$`
4. Group by top-level folder prefix:
   ```
   pokemon/data/pm0025/...  →  "pokemon" (2,100 models)
   field_graphic/...         →  "field_graphic" (1,800 models)
   chara/...                 →  "chara" (400 models)
   trainer/...               →  "trainer" (300 models)
   ```
5. Populate multi-select dropdown with categories + model counts
6. User selects which categories to export (or "All")

### Folder Grouping Strategy

```typescript
interface FolderGroup {
  prefix: string        // e.g. "pokemon/data"
  label: string         // e.g. "Pokemon (2,100 models)"
  modelPaths: string[]  // full .trmdl paths
  selected: boolean
}

function groupModels(paths: string[]): FolderGroup[] {
  const groups = new Map<string, string[]>()
  for (const p of paths) {
    // Group by first two path segments: "pokemon/data", "field_graphic/terrain_model", etc.
    const parts = p.split('/')
    const prefix = parts.length >= 2 ? `${parts[0]}/${parts[1]}` : parts[0]
    if (!groups.has(prefix)) groups.set(prefix, [])
    groups.get(prefix)!.push(p)
  }
  return [...groups.entries()].map(([prefix, paths]) => ({
    prefix,
    label: `${prefix} (${paths.length} models)`,
    modelPaths: paths,
    selected: false,
  }))
}
```

---

## Fastify API Endpoints

### Archive Scanning

```
GET /api/archive/scan?path=<arcDir>
  → Spawns CLI --list, returns grouped folder categories
  Response: {
    totalModels: number
    groups: FolderGroup[]
    scanTimeMs: number
  }
```

### Export Control

```
POST /api/export/start
  Body: ExportConfig { arcPath, outputDir, modelPaths[], parallelJobs }
  → Starts batch export, returns job ID
  Response: { jobId: string, totalModels: number }

DELETE /api/export/cancel
  → Kills all active child processes for current job
  Response: { cancelled: number }

GET /api/export/status
  → Current job status snapshot
  Response: { phase, success, failed, skipped, total, elapsed }
```

### Progress Streaming (SSE)

```
GET /api/export/progress
  → Server-Sent Events stream
  Events:
    data: { type: 'started', total: 2500 }
    data: { type: 'model-done', model: 'pm0025_00_00', success: true, index: 1 }
    data: { type: 'model-done', model: 'a_t01_i02_night', success: false, index: 2 }
    data: { type: 'complete', success: 2450, failed: 50, elapsed: 742.3 }
```

### Service Architecture

```typescript
// services/archiveScanner.ts
interface ArchiveScanner {
  scan(arcPath: string, cliExe: string): Promise<ScanResult>
}

// services/exportOrchestrator.ts
interface ExportOrchestrator {
  start(config: ExportConfig): string  // returns jobId
  cancel(jobId: string): void
  getStatus(jobId: string): ExportStatus
  onProgress(jobId: string, cb: (event: ExportEvent) => void): void
}

// services/processPool.ts
interface ProcessPool {
  run(tasks: ProcessTask[], concurrency: number): AsyncIterable<ProcessResult>
  killAll(): void
}
```

---

## Implementation Phases

### Phase 0 — Foundation Setup

**Scope:** Initialize all three projects, establish connectivity.

**Deliverables:**
- `SwitchToolboxCli.Api/` — Fastify server on port 3100 with health endpoint
- `SwitchToolboxCli.Electron/` — Electron shell with BrowserWindow + browseFolder IPC
- `SwitchToolboxCli.React/` — Vite + React + TypeScript project
- Root `package.json` with `dev` script to run all three concurrently

**Acceptance Criteria:**
- `npm run dev` starts Fastify (3100), Vite (5173), and Electron
- `GET /api/health` returns `{ status: 'ok' }`
- React app renders in Electron window
- No TypeScript errors

---

### Phase 1 — Archive Scanning (API + UI)

**Scope:** Implement archive scanning endpoint and folder category UI.

**API Work:**
- `archiveScanner.ts` — spawn CLI `--list`, parse `.trmdl` paths, group by folder prefix
- `GET /api/archive/scan` route with request validation

**UI Components:**
- `ArchiveSelector` — path input + Browse button + scan status
- `FolderFilter` — multi-select checklist of folder categories with model counts
- `apiClient.ts` — typed `scanArchive()` fetch call
- Shows "Scanning..." spinner during `--list` execution (~30s)

**Acceptance Criteria:**
- User selects archive dir → dropdown populates with folder categories
- Categories show model counts
- User can select/deselect categories
- "Select All" / "Deselect All" buttons

---

### Phase 2 — Export Engine (API + UI)

**Scope:** Implement parallel export orchestrator and progress streaming.

**API Work:**
- `exportOrchestrator.ts` — manages export job lifecycle
- `processPool.ts` — spawns N concurrent CLI processes, tracks completion
- `POST /api/export/start` — accepts config, returns job ID
- `DELETE /api/export/cancel` — kills active child processes
- `GET /api/export/progress` — SSE stream of real-time events

**UI Components:**
- `ExportControls` — Start/Stop buttons, parallel jobs slider
- `ProgressPanel` — animated progress bar, stats grid (success/failed/skipped)
- `LogViewer` — scrolling terminal-style log fed by SSE events
- `ResultsTable` — per-model status rows (OK / FAIL / SKIPPED)

**Acceptance Criteria:**
- Export runs selected models in parallel child processes
- Progress bar updates in real-time via SSE
- Stop button kills active processes
- Resume-safe: re-running skips already-exported models

---

### Phase 3 — Polish + Settings

**Scope:** Quality of life, persistence, error details.

**Features:**
- Remember last archive path + output dir (localStorage)
- Export mode selector (split DAE vs combined)
- Failed model retry button
- Open output folder in Explorer
- Error details expandable per failed model

---

## UI Mockup (Single Page Layout)

```
┌─────────────────────────────────────────────────────┐
│  SwitchToolbox Exporter                             │
├─────────────────────────────────────────────────────┤
│  ┌─ Archive Settings ─────────────────────────────┐ │
│  │  Archive Path: [________________________] [📁]  │ │
│  │                                                 │ │
│  │  Folder Categories:     Scanning... ●           │ │
│  │  ☑ pokemon/data       (2,100 models)            │ │
│  │  ☑ chara/data         (400 models)              │ │
│  │  ☐ field_graphic      (1,800 models)            │ │
│  │  ☐ trainer/data       (300 models)              │ │
│  │  [Select All] [Deselect All]  Total: 2,500      │ │
│  │                                                 │ │
│  │  Output Dir:  [________________________] [📁]   │ │
│  │  Parallel Jobs: [====●=====] 8                  │ │
│  │                                                 │ │
│  │  [▶ Start Export]  [■ Stop]                     │ │
│  └─────────────────────────────────────────────────┘ │
│                                                      │
│  ┌─ Progress ─────────────────────────────────────┐ │
│  │  Exporting...  1,247 / 2,500          49.9%     │ │
│  │  [████████████████░░░░░░░░░░░░░░░░░░]           │ │
│  │                                                 │ │
│  │  ✅ Success: 1,200  ❌ Failed: 47  ⏭ Skip: 0   │ │
│  │  ⏱ Elapsed: 12.3 min                           │ │
│  │                                                 │ │
│  │  > pm0025_00_00 ............... OK              │ │
│  │  > a_t01_i02_night ........... FAIL (crash)     │ │
│  │  > pm0418_00_00 .............. OK              │ │
│  │  > pm1038_12_00 .............. OK              │ │
│  └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Process isolation | One CLI process per model | Rust panic in tegra_swizzle kills the process; can't catch from C# |
| Folder discovery | `--list` flag (no extraction) | Archive descriptor is read-only, fast (~30s for 273K files) |
| Parallelism | Configurable (default 8) | Balances throughput vs I/O contention on archive files |
| Skip logic | Check for `model.dae` | Same as `export_all.ps1` — resume-safe batch |
| Backend framework | Fastify + TypeScript | Consistent with sprite-generator; typed routes, fast |
| Progress streaming | Server-Sent Events (SSE) | Simpler than WebSocket for one-way progress, no extra deps |
| Frontend framework | Vite + React + TypeScript | Consistent with BgEditor; fast dev server |
| State management | Zustand | Lightweight, matches ExtractionPage pattern |
| Styling | Inline + CSS variables | Matches BgEditor dark theme conventions |

---

## Immediate Next Work Items

1. Create `SwitchToolboxCli.Api/` — Fastify + TypeScript, health endpoint
2. Create `SwitchToolboxCli.React/` — Vite + React + TypeScript scaffold
3. Create `SwitchToolboxCli.Electron/` — minimal shell with `browseFolder` IPC
4. Implement `archiveScanner.ts` — spawn CLI `--list`, parse + group
5. Build `ExportPage.tsx` with archive selector + folder dropdown
6. Implement `exportOrchestrator.ts` — parallel process pool + SSE streaming
7. Build progress panel + log viewer fed by SSE
8. Wire `processPool.ts` for crash-isolated parallel export
