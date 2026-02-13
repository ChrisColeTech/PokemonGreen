import { TILES_BY_ID } from '../../../../data/tiles'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const knownTileIdsOnlyValidator = (context: GenerationContext): ValidationIssue[] => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const invalidCells: GridPoint[] = []

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const tileId = context.grid[y]?.[x]
      if (tileId === undefined || TILES_BY_ID[tileId] === undefined) {
        invalidCells.push({ x, y })
      }
    }
  }

  if (invalidCells.length === 0) {
    return []
  }

  return [
    {
      id: 'knownTileIdsOnly',
      severity: 'error',
      message: `Found ${invalidCells.length} cells with unknown tile ids.`,
      cells: invalidCells,
    },
  ]
}
