import type { EditorStoreState } from '../editorStore.types'

export const selectSidebarState = (state: EditorStoreState) => ({
  selectedCategory: state.selectedCategory,
  selectedTileId: state.selectedTileId,
  selectedBuildingId: state.selectedBuildingId,
  buildingRotation: state.buildingRotation,
  mapName: state.mapName,
  widthInput: state.widthInput,
  heightInput: state.heightInput,
  cellSize: state.cellSize,
})

export const selectSidebarActions = (state: EditorStoreState) => ({
  setSelectedCategory: state.setSelectedCategory,
  setSelectedTileId: state.setSelectedTileId,
  toggleSelectedBuilding: state.toggleSelectedBuilding,
  rotateSelectedBuilding: state.rotateSelectedBuilding,
  setMapName: state.setMapName,
  setWidthInput: state.setWidthInput,
  setHeightInput: state.setHeightInput,
  setCellSize: state.setCellSize,
  resizeGridFromInputs: state.resizeGridFromInputs,
  clearGrid: state.clearGrid,
  resetToDefaults: state.resetToDefaults,
})
