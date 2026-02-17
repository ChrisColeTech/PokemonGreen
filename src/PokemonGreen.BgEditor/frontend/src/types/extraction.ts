// ---------------------------------------------------------------------------
// GARC Extraction types
// ---------------------------------------------------------------------------

/** Combined archive preset key — each maps to a label + real GARC subpath */
export type ArchivePreset =
  | 'pokemon-battle-models'
  | 'battle-characters'
  | 'field-characters'
  | 'map-models'
  | 'overworld-models'
  | 'battle-tree-models'
  | 'z-move-visuals'
  | 'skybox'
  | 'cutscene-models'
  | 'npc-models'
  | 'custom'

export interface ArchivePresetOption {
  key: ArchivePreset
  label: string
  subpath: string
  entries?: string
}

export const ARCHIVE_PRESETS: ArchivePresetOption[] = [
  { key: 'pokemon-battle-models', label: 'Pokemon Battle Models', subpath: 'a/0/9/4', entries: '10,549' },
  { key: 'battle-characters',    label: 'Battle Characters (HD)', subpath: 'a/1/7/4', entries: '316' },
  { key: 'field-characters',     label: 'Field Characters (OW)',  subpath: 'a/2/0/0', entries: '604' },
  { key: 'map-models',           label: 'Map Models',             subpath: 'a/0/8/5' },
  { key: 'overworld-models',     label: 'Overworld Models',       subpath: 'a/0/8/6' },
  { key: 'battle-tree-models',   label: 'Battle Tree Models',     subpath: 'a/0/8/0' },
  { key: 'z-move-visuals',       label: 'Z-Move Visuals',         subpath: 'a/0/8/7', entries: '7,205' },
  { key: 'skybox',               label: 'Skybox Model',           subpath: 'a/0/7/2' },
  { key: 'cutscene-models',      label: 'Cutscene Models',        subpath: 'a/0/9/8' },
  { key: 'npc-models',           label: 'NPC Models',             subpath: 'a/1/7/0' },
  { key: 'custom',               label: 'Custom Path',            subpath: '' },
]

/** Export mode */
export type ExportMode = 'split' | 'individual'

/** Extraction configuration */
export interface ExtractionConfig {
  garcPath: string
  outputDir: string
  exportMode: ExportMode
  entryLimit: number | null
  deriveFolderNames: boolean
  archivePreset: ArchivePreset
}

/** Extraction phase */
export type ExtractionPhase = 'idle' | 'parsing' | 'grouping' | 'exporting' | 'done' | 'error' | 'stopped'

/** Live extraction stats */
export interface ExtractionStats {
  totalEntries: number
  processedEntries: number
  groupsFound: number
  modelsExported: number
  texturesExported: number
  clipsExported: number
  parseErrors: number
  exportErrors: number
}

/** Extraction progress state */
export interface ExtractionProgress {
  phase: ExtractionPhase
  stats: ExtractionStats
  logLines: string[]
  elapsedSeconds: number
}

/** A single extracted group result */
export interface ExtractedGroup {
  folderName: string
  modelCount: number
  textureCount: number
  clipCount: number
  files: string[]
  expanded: boolean
}

export const INITIAL_STATS: ExtractionStats = {
  totalEntries: 0,
  processedEntries: 0,
  groupsFound: 0,
  modelsExported: 0,
  texturesExported: 0,
  clipsExported: 0,
  parseErrors: 0,
  exportErrors: 0,
}

export const INITIAL_PROGRESS: ExtractionProgress = {
  phase: 'idle',
  stats: { ...INITIAL_STATS },
  logLines: [],
  elapsedSeconds: 0,
}
