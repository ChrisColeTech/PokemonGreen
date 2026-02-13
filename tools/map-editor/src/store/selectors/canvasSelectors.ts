import type { EditorStoreState } from '../editorStore.types'

export const selectCanvasState = (state: EditorStoreState) => ({
  mapWidth: state.mapWidth,
  mapHeight: state.mapHeight,
  drawMode: state.drawMode,
  selectedBuildingId: state.selectedBuildingId,
  buildingRotation: state.buildingRotation,
})

export const selectCanvasActions = (state: EditorStoreState) => ({
  beginDraw: state.beginDraw,
  endDraw: state.endDraw,
  paintCell: state.paintCell,
  placeBuilding: state.placeBuilding,
})
