import type { StateCreator } from 'zustand'
import type { ValidMapData } from '../services/mapIoService'
import type { BuildingId, BuildingRotation, DrawMode, PaletteCategory, TileGrid } from '../types/editor'
import type { ArchetypeId, GenerationDiagnostics, GenerationTemplateId } from '../types/generation'
import type { EditorTileRegistry } from '../types/registry'

export interface EditorStoreState {
  activeRegistry: EditorTileRegistry
  mapName: string
  mapWidth: number
  mapHeight: number
  widthInput: number
  heightInput: number
  cellSize: number
  selectedCategory: PaletteCategory
  selectedTileId: number
  selectedBuildingId: BuildingId | null
  buildingRotation: BuildingRotation
  drawMode: DrawMode
  grid: TileGrid
  pastGrids: TileGrid[]
  futureGrids: TileGrid[]
  drawStartGrid: TileGrid | null
  generationSeedInput: string
  generationArchetypeId: ArchetypeId
  generationTemplateId: GenerationTemplateId | null
  generationWidthInput: number
  generationHeightInput: number
  generationUseCurrentDimensions: boolean
  generationMaxRepairAttempts: number
  generationEnforceSpawnSafety: boolean
  generationEnforceDoorConnectivity: boolean
  lastGeneratedSeed: string | null
  lastGenerationDiagnostics: GenerationDiagnostics | null
  setWidthInput: (value: number) => void
  setHeightInput: (value: number) => void
  setMapName: (value: string) => void
  setCellSize: (value: number) => void
  setSelectedCategory: (category: PaletteCategory) => void
  setSelectedTileId: (tileId: number) => void
  toggleSelectedBuilding: (buildingId: BuildingId) => void
  rotateSelectedBuilding: (direction: -1 | 1) => void
  resizeGridFromInputs: () => void
  clearGrid: () => void
  resetToDefaults: () => void
  beginDraw: (mode: Exclude<DrawMode, null>) => void
  endDraw: () => void
  paintCell: (x: number, y: number, mode: Exclude<DrawMode, null>) => void
  placeBuilding: (x: number, y: number) => void
  loadMapData: (mapData: ValidMapData) => void
  setGenerationSeedInput: (value: string) => void
  setGenerationArchetypeId: (value: ArchetypeId) => void
  setGenerationTemplateId: (value: GenerationTemplateId | null) => void
  setGenerationWidthInput: (value: number) => void
  setGenerationHeightInput: (value: number) => void
  setGenerationUseCurrentDimensions: (value: boolean) => void
  setGenerationMaxRepairAttempts: (value: number) => void
  setGenerationEnforceSpawnSafety: (value: boolean) => void
  setGenerationEnforceDoorConnectivity: (value: boolean) => void
  setActiveRegistry: (registry: EditorTileRegistry) => void
  generateRandomMapFromControls: (seedOverride?: string) => void
  regenerateRandomMap: () => void
  undo: () => void
  redo: () => void
}

export type EditorStoreSlice<TSlice> = StateCreator<EditorStoreState, [], [], TSlice>

export type EditorStorePersistedState = Pick<
  EditorStoreState,
  | 'mapWidth'
  | 'mapHeight'
  | 'mapName'
  | 'widthInput'
  | 'heightInput'
  | 'cellSize'
  | 'selectedCategory'
  | 'selectedTileId'
  | 'selectedBuildingId'
  | 'buildingRotation'
  | 'grid'
  | 'generationSeedInput'
  | 'generationArchetypeId'
  | 'generationTemplateId'
  | 'generationWidthInput'
  | 'generationHeightInput'
  | 'generationUseCurrentDimensions'
  | 'generationMaxRepairAttempts'
  | 'generationEnforceSpawnSafety'
  | 'generationEnforceDoorConnectivity'
  | 'lastGeneratedSeed'
>
