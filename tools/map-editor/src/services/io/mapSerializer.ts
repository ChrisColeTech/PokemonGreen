import { TILES } from '../../data/tiles'
import {
  DEFAULT_TILE_SIZE,
  MAP_SCHEMA_VERSION,
  fileNameToMapId,
  splitTilesIntoLayers,
  type MapJsonPayload,
  type MapJsonTileType,
  type ValidMapData,
} from './mapSchema'

export const buildJsonExportPayload = (mapData: ValidMapData): MapJsonPayload => {
  const { baseTiles, overlayTiles } = splitTilesIntoLayers(mapData.tiles)

  return {
    schemaVersion: MAP_SCHEMA_VERSION,
    mapId: 'map',
    displayName: 'Map',
    tileSize: DEFAULT_TILE_SIZE,
    width: mapData.width,
    height: mapData.height,
    baseTiles,
    overlayTiles,
    tileTypes: TILES.reduce<Record<number, MapJsonTileType>>((accumulator, tile) => {
      accumulator[tile.id] = {
        name: tile.name,
        walkable: tile.walkable,
        category: tile.category,
        encounter: tile.encounter,
        direction: tile.direction,
      }
      return accumulator
    }, {}),
  }
}

export const buildVersionedJsonExportPayload = (
  mapData: ValidMapData,
  fileName: string,
  mapDisplayName?: string,
): MapJsonPayload => {
  const mapId = fileNameToMapId(fileName)
  const derivedDisplayName = mapId
    .split('_')
    .filter(Boolean)
    .map((token) => token[0].toUpperCase() + token.slice(1))
    .join(' ')
  const displayName = (mapDisplayName?.trim() || derivedDisplayName || 'Map').slice(0, 80)

  return {
    ...buildJsonExportPayload(mapData),
    mapId,
    displayName,
  }
}
