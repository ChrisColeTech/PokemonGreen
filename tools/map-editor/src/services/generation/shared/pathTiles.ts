import type { GridPoint } from '../../../types/editor'
import type { GenerationContext } from '../../../types/generation'
import { getPrimaryPathTileId } from './tileRoles'

export const collectPathCells = (context: GenerationContext): GridPoint[] => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const pathTileId = getPrimaryPathTileId(context)
  const cells: GridPoint[] = []

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (context.grid[y]?.[x] === pathTileId) {
        cells.push({ x, y })
      }
    }
  }

  return cells
}
