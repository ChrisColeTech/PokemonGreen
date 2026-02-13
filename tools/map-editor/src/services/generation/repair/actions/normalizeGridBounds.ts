import { getBaseTerrainTileId } from '../../shared/tileRoles'
import type { TileGrid } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const normalizeGridBounds = (context: GenerationContext): boolean => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const baseTileId = getBaseTerrainTileId(context)

  let changed = context.grid.length !== height
  for (let y = 0; y < Math.min(context.grid.length, height); y += 1) {
    if ((context.grid[y]?.length ?? 0) !== width) {
      changed = true
      break
    }
  }

  if (!changed) {
    return false
  }

  const nextGrid: TileGrid = Array.from({ length: height }, () => Array.from({ length: width }, () => baseTileId))
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const tileId = context.grid[y]?.[x]
      if (tileId !== undefined) {
        nextGrid[y][x] = tileId
      }
    }
  }

  context.grid = nextGrid
  return true
}
