import { TILES_BY_ID } from '../../../../data/tiles'
import { getBaseTerrainTileId } from '../../shared/tileRoles'
import type { GenerationContext } from '../../../../types/generation'

export const replaceUnknownTileIds = (context: GenerationContext): number => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const baseTileId = getBaseTerrainTileId(context)
  let replaced = 0

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const tileId = context.grid[y]?.[x]
      if (tileId === undefined || TILES_BY_ID[tileId] === undefined) {
        context.grid[y][x] = baseTileId
        replaced += 1
      }
    }
  }

  return replaced
}
