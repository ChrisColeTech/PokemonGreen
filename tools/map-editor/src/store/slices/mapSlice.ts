import { clamp, clearGrid, MAX_DIMENSION, MIN_DIMENSION, resizeGridPreserve } from '../../services/gridService'
import type { EditorStoreSlice } from '../editorStore.types'
import {
  areGridsEqual,
  createDefaultEditorState,
  DEFAULT_CELL_SIZE,
  DEFAULT_MAP_HEIGHT,
  DEFAULT_MAP_WIDTH,
  GRASS_TILE_ID,
  MAX_CELL_SIZE,
  MIN_CELL_SIZE,
  pushPastGrid,
  initialGrid,
} from './shared'

type MapSlice = Pick<
  import('../editorStore.types').EditorStoreState,
  | 'mapWidth'
  | 'mapHeight'
  | 'mapName'
  | 'widthInput'
  | 'heightInput'
  | 'cellSize'
  | 'grid'
  | 'setWidthInput'
  | 'setHeightInput'
  | 'setMapName'
  | 'setCellSize'
  | 'resizeGridFromInputs'
  | 'clearGrid'
  | 'resetToDefaults'
>

export const createMapSlice: EditorStoreSlice<MapSlice> = (set) => ({
  mapWidth: DEFAULT_MAP_WIDTH,
  mapHeight: DEFAULT_MAP_HEIGHT,
  mapName: 'Map',
  widthInput: DEFAULT_MAP_WIDTH,
  heightInput: DEFAULT_MAP_HEIGHT,
  cellSize: DEFAULT_CELL_SIZE,
  grid: initialGrid,
  setWidthInput: (value) =>
    set({
      widthInput: Number.isFinite(value) ? value : DEFAULT_MAP_WIDTH,
    }),
  setHeightInput: (value) =>
    set({
      heightInput: Number.isFinite(value) ? value : DEFAULT_MAP_HEIGHT,
    }),
  setMapName: (value) =>
    set({
      mapName: value.trim() || 'Map',
    }),
  setCellSize: (value) =>
    set({
      cellSize: clamp(Number.isFinite(value) ? value : DEFAULT_CELL_SIZE, MIN_CELL_SIZE, MAX_CELL_SIZE),
    }),
  resizeGridFromInputs: () =>
    set((state) => {
      const nextWidth = clamp(state.widthInput, MIN_DIMENSION, MAX_DIMENSION)
      const nextHeight = clamp(state.heightInput, MIN_DIMENSION, MAX_DIMENSION)
      const nextGrid = resizeGridPreserve(state.grid, nextWidth, nextHeight, GRASS_TILE_ID)
      if (
        nextWidth === state.mapWidth
        && nextHeight === state.mapHeight
        && areGridsEqual(nextGrid, state.grid)
      ) {
        return state
      }

      return {
        mapWidth: nextWidth,
        mapHeight: nextHeight,
        widthInput: nextWidth,
        heightInput: nextHeight,
        grid: nextGrid,
        pastGrids: pushPastGrid(state.pastGrids, state.grid),
        futureGrids: [],
        drawMode: null,
        drawStartGrid: null,
      }
    }),
  clearGrid: () =>
    set((state) => {
      const nextGrid = clearGrid(state.mapWidth, state.mapHeight, GRASS_TILE_ID)
      if (areGridsEqual(nextGrid, state.grid)) {
        return state
      }

      return {
        grid: nextGrid,
        pastGrids: pushPastGrid(state.pastGrids, state.grid),
        futureGrids: [],
        drawMode: null,
        drawStartGrid: null,
      }
    }),
  resetToDefaults: () =>
    set(createDefaultEditorState()),
})
