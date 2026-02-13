import { DEFAULT_HARD_CONSTRAINT_POLICY, RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID } from '../../data/generation/archetypes'
import { DEFAULT_CELL_SIZE, DEFAULT_MAP_HEIGHT, DEFAULT_MAP_WIDTH, GRASS_TILE_ID } from '../../data/tiles'
import { createGrid } from '../../services/gridService'
import type { TileGrid } from '../../types/editor'
import type { EditorStorePersistedState, EditorStoreState } from '../editorStore.types'

export const STORE_NAME = 'map-editor-grid'
export const MIN_CELL_SIZE = 8
export const MAX_CELL_SIZE = 48
export const HISTORY_LIMIT = 50
export const DEFAULT_GENERATION_ARCHETYPE_ID = 'town_route_basic' as const
export const MAX_GENERATION_REPAIR_ATTEMPTS = 8

export const initialGrid = createGrid(DEFAULT_MAP_WIDTH, DEFAULT_MAP_HEIGHT, GRASS_TILE_ID)
export const defaultArchetype = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[DEFAULT_GENERATION_ARCHETYPE_ID]

export const cloneGrid = (grid: TileGrid): TileGrid => grid.map((row) => [...row])

export const areGridsEqual = (leftGrid: TileGrid, rightGrid: TileGrid): boolean => {
  if (leftGrid.length !== rightGrid.length) {
    return false
  }

  for (let y = 0; y < leftGrid.length; y += 1) {
    const leftRow = leftGrid[y]
    const rightRow = rightGrid[y]
    if (leftRow.length !== rightRow.length) {
      return false
    }

    for (let x = 0; x < leftRow.length; x += 1) {
      if (leftRow[x] !== rightRow[x]) {
        return false
      }
    }
  }

  return true
}

export const pushPastGrid = (pastGrids: TileGrid[], grid: TileGrid): TileGrid[] => {
  const nextPast = [...pastGrids, cloneGrid(grid)]
  if (nextPast.length <= HISTORY_LIMIT) {
    return nextPast
  }

  return nextPast.slice(nextPast.length - HISTORY_LIMIT)
}

export const resolveGenerationSeed = (seedInput: string): string => {
  const normalizedSeed = seedInput.trim()
  return normalizedSeed || `${Date.now()}`
}

export const createDefaultEditorState = (): Pick<
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
  | 'drawMode'
  | 'grid'
  | 'pastGrids'
  | 'futureGrids'
  | 'drawStartGrid'
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
  | 'lastGenerationDiagnostics'
> => ({
  mapWidth: DEFAULT_MAP_WIDTH,
  mapHeight: DEFAULT_MAP_HEIGHT,
  mapName: 'Map',
  widthInput: DEFAULT_MAP_WIDTH,
  heightInput: DEFAULT_MAP_HEIGHT,
  cellSize: DEFAULT_CELL_SIZE,
  selectedCategory: 'terrain',
  selectedTileId: GRASS_TILE_ID,
  selectedBuildingId: null,
  buildingRotation: 0,
  drawMode: null,
  grid: createGrid(DEFAULT_MAP_WIDTH, DEFAULT_MAP_HEIGHT, GRASS_TILE_ID),
  pastGrids: [],
  futureGrids: [],
  drawStartGrid: null,
  generationSeedInput: '',
  generationArchetypeId: DEFAULT_GENERATION_ARCHETYPE_ID,
  generationTemplateId: null,
  generationWidthInput: defaultArchetype.recommendedDimensions.width,
  generationHeightInput: defaultArchetype.recommendedDimensions.height,
  generationUseCurrentDimensions: true,
  generationMaxRepairAttempts: 3,
  generationEnforceSpawnSafety: true,
  generationEnforceDoorConnectivity: true,
  lastGeneratedSeed: null,
  lastGenerationDiagnostics: null,
})

export const partializeEditorStore = (state: EditorStoreState): EditorStorePersistedState => ({
  mapWidth: state.mapWidth,
  mapHeight: state.mapHeight,
  mapName: state.mapName,
  widthInput: state.widthInput,
  heightInput: state.heightInput,
  cellSize: state.cellSize,
  selectedCategory: state.selectedCategory,
  selectedTileId: state.selectedTileId,
  selectedBuildingId: state.selectedBuildingId,
  buildingRotation: state.buildingRotation,
  grid: state.grid,
  generationSeedInput: state.generationSeedInput,
  generationArchetypeId: state.generationArchetypeId,
  generationTemplateId: state.generationTemplateId,
  generationWidthInput: state.generationWidthInput,
  generationHeightInput: state.generationHeightInput,
  generationUseCurrentDimensions: state.generationUseCurrentDimensions,
  generationMaxRepairAttempts: state.generationMaxRepairAttempts,
  generationEnforceSpawnSafety: state.generationEnforceSpawnSafety,
  generationEnforceDoorConnectivity: state.generationEnforceDoorConnectivity,
  lastGeneratedSeed: state.lastGeneratedSeed,
})

export { DEFAULT_CELL_SIZE, DEFAULT_HARD_CONSTRAINT_POLICY, DEFAULT_MAP_HEIGHT, DEFAULT_MAP_WIDTH, GRASS_TILE_ID }
