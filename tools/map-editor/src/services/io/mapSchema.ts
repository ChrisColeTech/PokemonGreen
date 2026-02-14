import { isOverlayTile } from '../../data/tiles'
import type { TileDirection } from '../../types/editor'

export interface MapJsonTileType {
  name: string
  walkable: boolean
  category: string
  encounter?: string
  direction?: TileDirection
}

export interface MapJsonPayload {
  schemaVersion: number
  mapId: string
  displayName: string
  tileSize: number
  width: number
  height: number
  baseTiles: number[][]
  overlayTiles: (number | null)[][]
  tileTypes: Record<number, MapJsonTileType>
}

export interface ValidMapData {
  displayName?: string
  width: number
  height: number
  tiles: number[][]
}

interface ParseSuccess {
  ok: true
  data: ValidMapData
}

interface ParseFailure {
  ok: false
  error: string
}

export type ParseMapPayloadResult = ParseSuccess | ParseFailure

export const JSON_FILE_ACCEPT = '.json,application/json'
export const MAP_SCHEMA_VERSION = 2
export const DEFAULT_TILE_SIZE = 32
export const GRASS_TILE_ID = 1

export const isObjectRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

export const splitTilesIntoLayers = (tiles: number[][]): { baseTiles: number[][], overlayTiles: (number | null)[][] } => {
  const height = tiles.length
  const width = tiles[0]?.length ?? 0

  const baseTiles: number[][] = []
  const overlayTiles: (number | null)[][] = []

  for (let y = 0; y < height; y++) {
    const baseRow: number[] = []
    const overlayRow: (number | null)[] = []

    for (let x = 0; x < width; x++) {
      const tileId = tiles[y][x]

      if (isOverlayTile(tileId)) {
        baseRow.push(GRASS_TILE_ID)
        overlayRow.push(tileId)
      } else {
        baseRow.push(tileId)
        overlayRow.push(null)
      }
    }

    baseTiles.push(baseRow)
    overlayTiles.push(overlayRow)
  }

  return { baseTiles, overlayTiles }
}

export const mergeLayersToTiles = (baseTiles: number[][], overlayTiles: (number | null)[][]): number[][] => {
  const height = baseTiles.length
  const width = baseTiles[0]?.length ?? 0
  const merged: number[][] = []

  for (let y = 0; y < height; y++) {
    const row: number[] = []
    for (let x = 0; x < width; x++) {
      const overlay = overlayTiles[y]?.[x]
      row.push(overlay ?? baseTiles[y][x])
    }
    merged.push(row)
  }

  return merged
}

export const normalizeMapId = (value: string): string => {
  const normalized = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')

  return normalized || 'map'
}

export const fileNameToMapId = (fileName: string): string => {
  const baseName = fileName
    .trim()
    .replace(/\.map\.json$/i, '')
    .replace(/\.json$/i, '')

  return normalizeMapId(baseName)
}
