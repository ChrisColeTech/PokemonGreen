import { loadDefaultRegistry, resolveFallbackTileId } from '../../services/registryService'
import type { EditorTileRegistry } from '../../types/registry'
import type { EditorStoreSlice } from '../editorStore.types'

type RegistrySlice = Pick<import('../editorStore.types').EditorStoreState, 'activeRegistry' | 'setActiveRegistry'>

const defaultRegistry = loadDefaultRegistry()

const resolveFallbackCategory = (
  selectedCategory: string,
  registry: EditorTileRegistry,
): string => {
  if (selectedCategory === 'buildings') {
    return registry.buildings.length > 0
      ? selectedCategory
      : (registry.categories.find((category) => category.showInPalette)?.id
        ?? registry.categories[0]?.id
        ?? 'buildings')
  }

  const hasVisibleCategory = registry.categories.some(
    (category) => category.showInPalette && category.id === selectedCategory,
  )
  if (hasVisibleCategory) {
    return selectedCategory
  }

  return registry.categories.find((category) => category.showInPalette)?.id
    ?? registry.categories[0]?.id
    ?? selectedCategory
}

export const createRegistrySlice: EditorStoreSlice<RegistrySlice> = (set) => ({
  activeRegistry: defaultRegistry,
  setActiveRegistry: (registry) =>
    set((state) => {
      const tileIds = new Set<number>(registry.tiles.map((tile) => tile.id))
      const buildingIds = new Set<string>(registry.buildings.map((building) => building.id))

      const selectedCategory = resolveFallbackCategory(state.selectedCategory, registry)
      const selectedTileId = tileIds.has(state.selectedTileId)
        ? state.selectedTileId
        : resolveFallbackTileId(registry)

      const selectedBuildingId = state.selectedBuildingId && buildingIds.has(state.selectedBuildingId)
        ? state.selectedBuildingId
        : null

      return {
        activeRegistry: registry,
        selectedCategory,
        selectedTileId,
        selectedBuildingId,
      }
    }),
})
