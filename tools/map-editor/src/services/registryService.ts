import defaultRegistryRaw from '../data/registries/default.json?raw'
import type {
  EditorRegistryBuildingDefinition,
  EditorRegistryCategoryDefinition,
  EditorRegistryMetadata,
  EditorRegistryTileDefinition,
  EditorTileRegistry,
} from '../types/registry'

interface ParseRegistrySuccess {
  ok: true
  data: EditorTileRegistry
}

interface ParseRegistryFailure {
  ok: false
  error: string
}

export type ParseRegistryResult = ParseRegistrySuccess | ParseRegistryFailure
type ParseSubResult<TData> = { ok: true, data: TData } | ParseRegistryFailure

const isObjectRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const isNonEmptyString = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0

const isHexColor = (value: string): boolean => /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(value)

const parseMetadata = (value: unknown): ParseSubResult<EditorRegistryMetadata> => {
  if (!isObjectRecord(value)) {
    return { ok: false, error: 'registry.metadata must be an object.' }
  }

  const { id, name, version } = value

  if (!isNonEmptyString(id)) {
    return { ok: false, error: 'registry.metadata.id must be a non-empty string.' }
  }

  if (!isNonEmptyString(name)) {
    return { ok: false, error: 'registry.metadata.name must be a non-empty string.' }
  }

  if (!isNonEmptyString(version)) {
    return { ok: false, error: 'registry.metadata.version must be a non-empty string.' }
  }

  return {
    ok: true,
    data: {
      id: id.trim(),
      name: name.trim(),
      version: version.trim(),
    },
  }
}

const parseCategories = (value: unknown): ParseSubResult<EditorRegistryCategoryDefinition[]> => {
  if (!Array.isArray(value)) {
    return { ok: false, error: 'registry.categories must be an array.' }
  }

  const categories: EditorRegistryCategoryDefinition[] = []
  const categoryIds = new Set<string>()

  for (let index = 0; index < value.length; index += 1) {
    const entry = value[index]
    if (!isObjectRecord(entry)) {
      return { ok: false, error: `registry.categories[${index}] must be an object.` }
    }

    const { id, label, showInPalette } = entry
    if (!isNonEmptyString(id)) {
      return { ok: false, error: `registry.categories[${index}].id must be a non-empty string.` }
    }
    if (!isNonEmptyString(label)) {
      return { ok: false, error: `registry.categories[${index}].label must be a non-empty string.` }
    }
    if (typeof showInPalette !== 'boolean') {
      return { ok: false, error: `registry.categories[${index}].showInPalette must be a boolean.` }
    }

    const normalizedId = id.trim()
    if (categoryIds.has(normalizedId)) {
      return { ok: false, error: `registry.categories[${index}].id '${normalizedId}' is duplicated.` }
    }

    categoryIds.add(normalizedId)
    categories.push({
      id: normalizedId,
      label: label.trim(),
      showInPalette,
    })
  }

  return { ok: true, data: categories }
}

const isValidDirection = (value: unknown): value is 'up' | 'down' | 'left' | 'right' =>
  value === 'up' || value === 'down' || value === 'left' || value === 'right'

const parseTiles = (
  value: unknown,
  categoryIds: Set<string>,
): ParseSubResult<EditorRegistryTileDefinition[]> => {
  if (!Array.isArray(value) || value.length === 0) {
    return { ok: false, error: 'registry.tiles must be a non-empty array.' }
  }

  const tiles: EditorRegistryTileDefinition[] = []
  const tileIds = new Set<number>()

  for (let index = 0; index < value.length; index += 1) {
    const entry = value[index]
    if (!isObjectRecord(entry)) {
      return { ok: false, error: `registry.tiles[${index}] must be an object.` }
    }

    const { id, name, color, walkable, category, encounter, direction, isOverlay } = entry

    if (typeof id !== 'number' || !Number.isInteger(id) || id < 0) {
      return { ok: false, error: `registry.tiles[${index}].id must be a non-negative integer.` }
    }
    const tileId = id

    if (tileIds.has(tileId)) {
      return { ok: false, error: `registry.tiles[${index}].id '${tileId}' is duplicated.` }
    }
    if (!isNonEmptyString(name)) {
      return { ok: false, error: `registry.tiles[${index}].name must be a non-empty string.` }
    }
    if (!isNonEmptyString(color) || !isHexColor(color.trim())) {
      return { ok: false, error: `registry.tiles[${index}].color must be a valid hex color string.` }
    }
    if (typeof walkable !== 'boolean') {
      return { ok: false, error: `registry.tiles[${index}].walkable must be a boolean.` }
    }
    if (!isNonEmptyString(category)) {
      return { ok: false, error: `registry.tiles[${index}].category must be a non-empty string.` }
    }

    const normalizedCategory = category.trim()
    if (!categoryIds.has(normalizedCategory)) {
      return {
        ok: false,
        error: `registry.tiles[${index}].category '${normalizedCategory}' is not defined in registry.categories.`,
      }
    }

    if (encounter !== undefined && !isNonEmptyString(encounter)) {
      return { ok: false, error: `registry.tiles[${index}].encounter must be a non-empty string when provided.` }
    }

    if (direction !== undefined && !isValidDirection(direction)) {
      return {
        ok: false,
        error: `registry.tiles[${index}].direction must be one of: up, down, left, right.`,
      }
    }

    if (isOverlay !== undefined && typeof isOverlay !== 'boolean') {
      return { ok: false, error: `registry.tiles[${index}].isOverlay must be a boolean when provided.` }
    }

    tileIds.add(tileId)
    tiles.push({
      id: tileId,
      name: name.trim(),
      color: color.trim(),
      walkable,
      category: normalizedCategory,
      encounter: encounter?.trim(),
      direction,
      isOverlay,
    })
  }

  return { ok: true, data: tiles }
}

const parseBuildings = (
  value: unknown,
  tileIds: Set<number>,
): ParseSubResult<EditorRegistryBuildingDefinition[]> => {
  if (!Array.isArray(value)) {
    return { ok: false, error: 'registry.buildings must be an array.' }
  }

  const buildings: EditorRegistryBuildingDefinition[] = []
  const buildingIds = new Set<string>()

  for (let index = 0; index < value.length; index += 1) {
    const entry = value[index]
    if (!isObjectRecord(entry)) {
      return { ok: false, error: `registry.buildings[${index}] must be an object.` }
    }

    const { id, name, tiles } = entry
    if (!isNonEmptyString(id)) {
      return { ok: false, error: `registry.buildings[${index}].id must be a non-empty string.` }
    }
    if (!isNonEmptyString(name)) {
      return { ok: false, error: `registry.buildings[${index}].name must be a non-empty string.` }
    }
    if (!Array.isArray(tiles) || tiles.length === 0) {
      return { ok: false, error: `registry.buildings[${index}].tiles must be a non-empty matrix.` }
    }

    const normalizedId = id.trim()
    if (buildingIds.has(normalizedId)) {
      return { ok: false, error: `registry.buildings[${index}].id '${normalizedId}' is duplicated.` }
    }
    buildingIds.add(normalizedId)

    const matrix: (number | null)[][] = []
    let expectedWidth: number | null = null

    for (let y = 0; y < tiles.length; y += 1) {
      const row = tiles[y]
      if (!Array.isArray(row) || row.length === 0) {
        return { ok: false, error: `registry.buildings[${index}].tiles[${y}] must be a non-empty array.` }
      }

      if (expectedWidth === null) {
        expectedWidth = row.length
      } else if (row.length !== expectedWidth) {
        return {
          ok: false,
          error: `registry.buildings[${index}].tiles rows must all have ${expectedWidth} columns.`,
        }
      }

      const parsedRow: (number | null)[] = []
      for (let x = 0; x < row.length; x += 1) {
        const tileId = row[x]
        if (tileId !== null && !Number.isInteger(tileId)) {
          return {
            ok: false,
            error: `registry.buildings[${index}].tiles[${y}][${x}] must be null or an integer tile id.`,
          }
        }

        if (Number.isInteger(tileId) && !tileIds.has(tileId)) {
          return {
            ok: false,
            error: `registry.buildings[${index}].tiles[${y}][${x}] references unknown tile id ${tileId}.`,
          }
        }

        parsedRow.push(tileId)
      }

      matrix.push(parsedRow)
    }

    buildings.push({
      id: normalizedId,
      name: name.trim(),
      tiles: matrix,
    })
  }

  return { ok: true, data: buildings }
}

export const parseRegistryJson = (rawValue: string, source = 'registry'): ParseRegistryResult => {
  let parsedValue: unknown

  try {
    parsedValue = JSON.parse(rawValue)
  } catch {
    return {
      ok: false,
      error: `${source}: invalid JSON format.`,
    }
  }

  if (!isObjectRecord(parsedValue)) {
    return {
      ok: false,
      error: `${source}: root value must be an object.`,
    }
  }

  const metadataResult = parseMetadata(parsedValue.metadata)
  if (!metadataResult.ok) {
    return { ok: false, error: `${source}: ${metadataResult.error}` }
  }

  const categoriesResult = parseCategories(parsedValue.categories)
  if (!categoriesResult.ok) {
    return { ok: false, error: `${source}: ${categoriesResult.error}` }
  }
  const categoryIds = new Set(categoriesResult.data.map((category) => category.id))

  const tilesResult = parseTiles(parsedValue.tiles, categoryIds)
  if (!tilesResult.ok) {
    return { ok: false, error: `${source}: ${tilesResult.error}` }
  }
  const tileIds = new Set(tilesResult.data.map((tile) => tile.id))

  const buildingsResult = parseBuildings(parsedValue.buildings, tileIds)
  if (!buildingsResult.ok) {
    return { ok: false, error: `${source}: ${buildingsResult.error}` }
  }

  return {
    ok: true,
    data: {
      metadata: metadataResult.data,
      categories: categoriesResult.data,
      tiles: tilesResult.data,
      buildings: buildingsResult.data,
    },
  }
}

export const loadDefaultRegistry = (): EditorTileRegistry => {
  const parsedRegistry = parseRegistryJson(defaultRegistryRaw, 'default registry')
  if (!parsedRegistry.ok) {
    throw new Error(`Failed to load default registry: ${parsedRegistry.error}`)
  }

  return parsedRegistry.data
}

export const getVisiblePaletteCategories = (registry: EditorTileRegistry): EditorRegistryCategoryDefinition[] =>
  registry.categories.filter((category) => category.showInPalette)

export const getTilesById = (registry: EditorTileRegistry): Record<number, EditorRegistryTileDefinition> =>
  registry.tiles.reduce<Record<number, EditorRegistryTileDefinition>>((result, tile) => {
    result[tile.id] = tile
    return result
  }, {})

export const getBuildingsById = (registry: EditorTileRegistry): Record<string, EditorRegistryBuildingDefinition> =>
  registry.buildings.reduce<Record<string, EditorRegistryBuildingDefinition>>((result, building) => {
    result[building.id] = building
    return result
  }, {})

export const resolveFallbackTileId = (registry: EditorTileRegistry): number => {
  const terrainLikeWalkable = registry.tiles.find((tile) => {
    const normalizedCategory = tile.category.toLowerCase()
    return tile.walkable && (normalizedCategory === 'terrain' || normalizedCategory.includes('terrain'))
  })

  if (terrainLikeWalkable) {
    return terrainLikeWalkable.id
  }

  const firstWalkable = registry.tiles.find((tile) => tile.walkable)
  if (firstWalkable) {
    return firstWalkable.id
  }

  return registry.tiles[0].id
}
