import { TILES_BY_ID } from '../../../../data/tiles'
import { isInBounds } from '../../shared/geometry'
import { getPrimaryPathTileId } from '../../shared/tileRoles'
import { resolveSpawnCell } from '../../validation/hardConstraintValidationService'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const enforceSpawnSafety = (context: GenerationContext): boolean => {
  const spawnCell = resolveSpawnCell(context)
  if (!spawnCell) {
    return false
  }

  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const pathTileId = getPrimaryPathTileId(context)
  let changed = false

  if (isInBounds(spawnCell.x, spawnCell.y, width, height)) {
    const tileId = context.grid[spawnCell.y][spawnCell.x]
    if (!TILES_BY_ID[tileId]?.walkable) {
      context.grid[spawnCell.y][spawnCell.x] = pathTileId
      changed = true
    }
  }

  const neighbors: GridPoint[] = [
    { x: 1, y: 0 },
    { x: -1, y: 0 },
    { x: 0, y: 1 },
    { x: 0, y: -1 },
  ]
  const hasWalkableNeighbor = neighbors.some((offset) => {
    const nx = spawnCell.x + offset.x
    const ny = spawnCell.y + offset.y
    if (!isInBounds(nx, ny, width, height)) {
      return false
    }

    return Boolean(TILES_BY_ID[context.grid[ny][nx]]?.walkable)
  })

  if (!hasWalkableNeighbor) {
    const neighbor = neighbors
      .map((offset) => ({ x: spawnCell.x + offset.x, y: spawnCell.y + offset.y }))
      .find((cell) => isInBounds(cell.x, cell.y, width, height))
    if (neighbor) {
      context.grid[neighbor.y][neighbor.x] = pathTileId
      changed = true
    }
  }

  return changed
}
