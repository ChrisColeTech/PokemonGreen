import { getBuildingsById, getTilesById, getVisiblePaletteCategories } from '../../services/registryService'
import type { EditorStoreState } from '../editorStore.types'

export const categoriesForPalette = (state: EditorStoreState) =>
  getVisiblePaletteCategories(state.activeRegistry)

export const tilesById = (state: EditorStoreState) => getTilesById(state.activeRegistry)

export const tilesForSelectedCategory = (state: EditorStoreState) =>
  state.selectedCategory === 'buildings'
    ? []
    : state.activeRegistry.tiles.filter((tile) => tile.category === state.selectedCategory)

export const buildings = (state: EditorStoreState) => state.activeRegistry.buildings

export const buildingsById = (state: EditorStoreState) => getBuildingsById(state.activeRegistry)
