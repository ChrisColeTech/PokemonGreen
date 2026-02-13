import { getFootprintCells } from './shared'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const buildingFootprintsInBoundsValidator = (context: GenerationContext): ValidationIssue[] => {
  const outOfBoundsCells: GridPoint[] = []

  for (let index = 0; index < context.buildingPlacements.length; index += 1) {
    const cells = getFootprintCells(context, index)
    for (const cell of cells) {
      if (!cell.inBounds) {
        outOfBoundsCells.push({ x: cell.x, y: cell.y })
      }
    }
  }

  if (outOfBoundsCells.length === 0) {
    return []
  }

  return [
    {
      id: 'buildingFootprintsInBounds',
      severity: 'error',
      message: `Found ${outOfBoundsCells.length} building footprint cells outside map bounds.`,
      cells: outOfBoundsCells,
    },
  ]
}
