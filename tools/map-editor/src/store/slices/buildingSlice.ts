import { placeBuildingFootprint } from '../../services/buildingService'
import type { BuildingRotation } from '../../types/editor'
import type { EditorStoreSlice } from '../editorStore.types'
import { GRASS_TILE_ID, pushPastGrid } from './shared'

type BuildingSlice = Pick<
  import('../editorStore.types').EditorStoreState,
  | 'selectedCategory'
  | 'selectedTileId'
  | 'selectedBuildingId'
  | 'buildingRotation'
  | 'setSelectedCategory'
  | 'setSelectedTileId'
  | 'toggleSelectedBuilding'
  | 'rotateSelectedBuilding'
  | 'placeBuilding'
>

export const createBuildingSlice: EditorStoreSlice<BuildingSlice> = (set) => ({
  selectedCategory: 'terrain',
  selectedTileId: GRASS_TILE_ID,
  selectedBuildingId: null,
  buildingRotation: 0,
  setSelectedCategory: (category) => set({ selectedCategory: category }),
  setSelectedTileId: (tileId) =>
    set((state) => {
      const tile = state.activeRegistry.tiles.find((entry) => entry.id === tileId)
      if (!tile) {
        return state
      }

      return {
        selectedTileId: tileId,
        selectedCategory: tile.category,
        selectedBuildingId: null,
        buildingRotation: 0,
      }
    }),
  toggleSelectedBuilding: (buildingId) =>
    set((state) => {
      if (!state.activeRegistry.buildings.some((building) => building.id === buildingId)) {
        return state
      }

      if (state.selectedBuildingId === buildingId) {
        return {
          selectedBuildingId: null,
          buildingRotation: 0,
        }
      }

      return {
        selectedCategory: 'buildings',
        selectedBuildingId: buildingId,
        buildingRotation: 0,
      }
    }),
  rotateSelectedBuilding: (direction) =>
    set((state) => {
      if (!state.selectedBuildingId) {
        return state
      }

      const nextRotation = ((state.buildingRotation + direction + 4) % 4) as BuildingRotation
      return {
        buildingRotation: nextRotation,
      }
    }),
  placeBuilding: (x, y) =>
    set((state) => {
      if (!state.selectedBuildingId) {
        return state
      }

      if (!state.activeRegistry.buildings.some((building) => building.id === state.selectedBuildingId)) {
        return state
      }

      const selectedBuilding = state.activeRegistry.buildings.find((building) => building.id === state.selectedBuildingId)
      if (!selectedBuilding) {
        return state
      }

      const nextGrid = placeBuildingFootprint(
        state.grid,
        selectedBuilding,
        state.buildingRotation,
        x,
        y,
        state.mapWidth,
        state.mapHeight,
      )

      if (nextGrid === state.grid) {
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
})
