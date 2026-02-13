import type { EditorStoreState } from '../editorStore.types'

export const selectMenuState = (state: EditorStoreState) => ({
  mapName: state.mapName,
  mapWidth: state.mapWidth,
  mapHeight: state.mapHeight,
  grid: state.grid,
  cellSize: state.cellSize,
  canUndo: state.pastGrids.length > 0,
  canRedo: state.futureGrids.length > 0,
})

export const selectMenuActions = (state: EditorStoreState) => ({
  setCellSize: state.setCellSize,
  resetToDefaults: state.resetToDefaults,
  clearGrid: state.clearGrid,
  loadMapData: state.loadMapData,
  undo: state.undo,
  redo: state.redo,
})
