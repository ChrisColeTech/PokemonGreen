import { TILES_BY_ID } from '../../data/tiles'
import { MAX_DIMENSION, MIN_DIMENSION } from '../gridService'
import {
  isObjectRecord,
  mergeLayersToTiles,
  type ParseMapPayloadResult,
} from './mapSchema'

export const parseMapJsonPayload = (rawValue: string): ParseMapPayloadResult => {
  let parsedValue: unknown

  try {
    parsedValue = JSON.parse(rawValue)
  } catch {
    return {
      ok: false,
      error: 'Invalid JSON format.',
    }
  }

  if (!isObjectRecord(parsedValue)) {
    return {
      ok: false,
      error: 'Map data must be an object.',
    }
  }

  const widthValue = parsedValue.width
  const heightValue = parsedValue.height
  const schemaVersionValue = parsedValue.schemaVersion
  const tileTypesValue = parsedValue.tileTypes
  const displayNameValue = parsedValue.displayName

  if (schemaVersionValue !== undefined && schemaVersionValue !== 1 && schemaVersionValue !== 2) {
    return {
      ok: false,
      error: `Unsupported schemaVersion ${String(schemaVersionValue)}. Expected 1 or 2.`,
    }
  }

  const schemaVersion = schemaVersionValue ?? 1

  if (typeof widthValue !== 'number' || typeof heightValue !== 'number' || !Number.isInteger(widthValue) || !Number.isInteger(heightValue)) {
    return {
      ok: false,
      error: 'Map width and height must be integers.',
    }
  }

  const width: number = widthValue
  const height: number = heightValue

  if (width < MIN_DIMENSION || width > MAX_DIMENSION || height < MIN_DIMENSION || height > MAX_DIMENSION) {
    return {
      ok: false,
      error: `Map dimensions must be between ${MIN_DIMENSION} and ${MAX_DIMENSION}.`,
    }
  }

  const knownTileIds = new Set<number>()

  if (isObjectRecord(tileTypesValue)) {
    Object.keys(tileTypesValue).forEach((key) => {
      const parsedId = Number.parseInt(key, 10)
      if (Number.isInteger(parsedId)) {
        knownTileIds.add(parsedId)
      }
    })

    if (knownTileIds.size === 0) {
      return {
        ok: false,
        error: 'tileTypes must define at least one tile id.',
      }
    }
  }

  let mergedTiles: number[][]

  if (schemaVersion === 2) {
    const baseTilesValue = parsedValue.baseTiles
    const overlayTilesValue = parsedValue.overlayTiles

    if (!Array.isArray(baseTilesValue) || baseTilesValue.length !== height) {
      return {
        ok: false,
        error: 'baseTiles must be an array matching map height.',
      }
    }

    if (!Array.isArray(overlayTilesValue) || overlayTilesValue.length !== height) {
      return {
        ok: false,
        error: 'overlayTiles must be an array matching map height.',
      }
    }

    const baseTiles: number[][] = []
    const overlayTiles: (number | null)[][] = []

    for (let y = 0; y < height; y++) {
      const baseRow = baseTilesValue[y]
      const overlayRow = overlayTilesValue[y]

      if (!Array.isArray(baseRow) || baseRow.length !== width) {
        return {
          ok: false,
          error: `baseTiles row ${y + 1} must contain exactly ${width} columns.`,
        }
      }

      if (!Array.isArray(overlayRow) || overlayRow.length !== width) {
        return {
          ok: false,
          error: `overlayTiles row ${y + 1} must contain exactly ${width} columns.`,
        }
      }

      const parsedBaseRow: number[] = []
      const parsedOverlayRow: (number | null)[] = []

      for (let x = 0; x < width; x++) {
        const baseTileId = baseRow[x]
        if (!Number.isInteger(baseTileId)) {
          return {
            ok: false,
            error: `baseTiles at (${x}, ${y}) must be an integer tile id.`,
          }
        }
        if (knownTileIds.size > 0 && !knownTileIds.has(baseTileId)) {
          return {
            ok: false,
            error: `baseTiles at (${x}, ${y}) references unknown tile id ${baseTileId}.`,
          }
        }
        parsedBaseRow.push(baseTileId)

        const overlayTileId = overlayRow[x]
        if (overlayTileId === null) {
          parsedOverlayRow.push(null)
        } else if (!Number.isInteger(overlayTileId)) {
          return {
            ok: false,
            error: `overlayTiles at (${x}, ${y}) must be null or an integer tile id.`,
          }
        } else {
          if (knownTileIds.size > 0 && !knownTileIds.has(overlayTileId)) {
            return {
              ok: false,
              error: `overlayTiles at (${x}, ${y}) references unknown tile id ${overlayTileId}.`,
            }
          }
          parsedOverlayRow.push(overlayTileId)
        }
      }

      baseTiles.push(parsedBaseRow)
      overlayTiles.push(parsedOverlayRow)
    }

    mergedTiles = mergeLayersToTiles(baseTiles, overlayTiles)
  } else {
    const tilesValue = parsedValue.tiles

    if (!Array.isArray(tilesValue) || tilesValue.length !== height) {
      return {
        ok: false,
        error: 'Tile rows must be an array matching map height.',
      }
    }

    mergedTiles = []

    for (let rowIndex = 0; rowIndex < height; rowIndex++) {
      const row = tilesValue[rowIndex]

      if (!Array.isArray(row) || row.length !== width) {
        return {
          ok: false,
          error: `Tile row ${rowIndex + 1} must contain exactly ${width} columns.`,
        }
      }

      const normalizedRow: number[] = []
      for (let columnIndex = 0; columnIndex < width; columnIndex++) {
        const tileId = row[columnIndex]
        if (!Number.isInteger(tileId)) {
          return {
            ok: false,
            error: `Tile at (${columnIndex}, ${rowIndex}) must be an integer tile id.`,
          }
        }

        if (knownTileIds.size > 0 && !knownTileIds.has(tileId)) {
          return {
            ok: false,
            error: `Tile at (${columnIndex}, ${rowIndex}) references unknown tile id ${tileId} for this map schema.`,
          }
        }

        if (!TILES_BY_ID[tileId] && knownTileIds.size === 0) {
          return {
            ok: false,
            error: `Tile at (${columnIndex}, ${rowIndex}) references unknown tile id ${tileId}.`,
          }
        }

        normalizedRow.push(tileId)
      }

      mergedTiles.push(normalizedRow)
    }
  }

  return {
    ok: true,
    data: {
      displayName: typeof displayNameValue === 'string' && displayNameValue.trim().length > 0
        ? displayNameValue.trim()
        : undefined,
      width,
      height,
      tiles: mergedTiles,
    },
  }
}
