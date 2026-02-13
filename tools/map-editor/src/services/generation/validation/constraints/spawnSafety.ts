import { TILES_BY_ID } from '../../../../data/tiles'
import { isInBounds } from '../../shared/geometry'
import { findSpawnCell } from './shared'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const spawnSafetyValidator = (context: GenerationContext): ValidationIssue[] => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const spawnCell = findSpawnCell(context)
  if (!spawnCell) {
    return [
      {
        id: 'spawnSafety',
        severity: 'error',
        message: 'Spawn cell cannot be resolved without a primary path.',
      },
    ]
  }

  if (!isInBounds(spawnCell.x, spawnCell.y, width, height)) {
    return [
      {
        id: 'spawnSafety',
        severity: 'error',
        message: 'Spawn cell is out of bounds.',
        cells: [spawnCell],
      },
    ]
  }

  const spawnTile = context.grid[spawnCell.y]?.[spawnCell.x]
  if (spawnTile === undefined || !TILES_BY_ID[spawnTile]?.walkable) {
    return [
      {
        id: 'spawnSafety',
        severity: 'error',
        message: 'Spawn cell is not walkable.',
        cells: [spawnCell],
      },
    ]
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

    const tileId = context.grid[ny]?.[nx]
    return tileId !== undefined && Boolean(TILES_BY_ID[tileId]?.walkable)
  })

  if (!hasWalkableNeighbor) {
    return [
      {
        id: 'spawnSafety',
        severity: 'error',
        message: 'Spawn cell has no walkable escape neighbor.',
        cells: [spawnCell],
      },
    ]
  }

  return []
}
