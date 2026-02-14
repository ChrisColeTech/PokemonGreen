import type { EditorTileRegistry, EditorTileDefinition, EditorCategoryDefinition, EditorBuildingDefinition } from '../types/editor'
import defaultRegistry from '../data/registries/default.json'

// --- Loader ---

export function parseRegistryJson(json: string): EditorTileRegistry {
  const data = JSON.parse(json)

  if (!data.id || !data.name || !data.version) {
    throw new Error('Registry must have id, name, and version fields')
  }
  if (!Array.isArray(data.categories) || data.categories.length === 0) {
    throw new Error('Registry must have at least one category')
  }
  if (!Array.isArray(data.tiles) || data.tiles.length === 0) {
    throw new Error('Registry must have at least one tile')
  }

  // Validate no duplicate tile IDs
  const tileIds = new Set<number>()
  for (const tile of data.tiles) {
    if (tileIds.has(tile.id)) {
      throw new Error(`Duplicate tile id: ${tile.id}`)
    }
    tileIds.add(tile.id)
  }

  // Validate tile category references
  const catIds = new Set(data.categories.map((c: EditorCategoryDefinition) => c.id))
  for (const tile of data.tiles) {
    if (!catIds.has(tile.category)) {
      throw new Error(`Tile "${tile.name}" references unknown category "${tile.category}"`)
    }
  }

  // Validate building tile references
  if (data.buildings) {
    for (const building of data.buildings) {
      for (const row of building.tiles) {
        for (const tileId of row) {
          if (tileId !== null && !tileIds.has(tileId)) {
            throw new Error(`Building "${building.name}" references unknown tile id ${tileId}`)
          }
        }
      }
    }
  }

  return {
    id: data.id,
    name: data.name,
    version: data.version,
    categories: data.categories,
    tiles: data.tiles,
    buildings: data.buildings || [],
  }
}

export function loadDefaultRegistry(): EditorTileRegistry {
  return defaultRegistry as EditorTileRegistry
}

// --- Derived lookups ---

export function buildTilesById(registry: EditorTileRegistry): Map<number, EditorTileDefinition> {
  const map = new Map<number, EditorTileDefinition>()
  for (const tile of registry.tiles) {
    map.set(tile.id, tile)
  }
  return map
}

export function tilesForCategory(registry: EditorTileRegistry, categoryId: string): EditorTileDefinition[] {
  return registry.tiles.filter(t => t.category === categoryId)
}

export function paletteCategories(registry: EditorTileRegistry): EditorCategoryDefinition[] {
  return registry.categories.filter(c => c.showInPalette)
}

export function buildingWidth(building: EditorBuildingDefinition): number {
  return building.tiles[0]?.length ?? 0
}

export function buildingHeight(building: EditorBuildingDefinition): number {
  return building.tiles.length
}

// Fallback tile for when the selected tile is missing after a registry switch
export function fallbackTileId(registry: EditorTileRegistry): number {
  // Prefer first walkable terrain tile
  const grass = registry.tiles.find(t => t.category === 'terrain' && t.walkable)
  if (grass) return grass.id
  return registry.tiles[0]?.id ?? 0
}

// Fallback category when selected category is missing
export function fallbackCategoryId(registry: EditorTileRegistry): string {
  const visible = paletteCategories(registry)
  return visible[0]?.id ?? registry.categories[0]?.id ?? ''
}

// Unknown tile placeholder (for IDs not in registry)
export const UNKNOWN_TILE: EditorTileDefinition = {
  id: -1,
  name: 'Unknown',
  color: '#ff00ff',
  walkable: false,
  category: 'terrain',
}
