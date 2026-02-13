import { clearFootprintFromGrid, getPlacementFootprintCells, placeBuildingDeterministically } from './shared'
import type { GeneratedBuildingPlacement, GenerationContext } from '../../../../types/generation'

export const repairBuildingFootprints = (context: GenerationContext): number => {
  const nextPlacements: GeneratedBuildingPlacement[] = []
  const occupied = new Set<string>()
  let changed = 0

  for (const placement of context.buildingPlacements) {
    const cells = getPlacementFootprintCells(context, placement)
    const inBounds = cells.every((cell) => cell.inBounds)

    if (inBounds) {
      nextPlacements.push(placement)
      for (const cell of cells) {
        if (cell.inBounds) {
          occupied.add(`${cell.x},${cell.y}`)
        }
      }
      continue
    }

    clearFootprintFromGrid(context, placement)
    const relocated = placeBuildingDeterministically(context, placement.buildingId, occupied)
    if (relocated) {
      nextPlacements.push(relocated)
    }
    changed += 1
  }

  context.buildingPlacements = nextPlacements
  return changed
}
