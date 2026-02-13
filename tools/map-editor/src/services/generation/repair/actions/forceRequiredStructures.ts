import { placeBuildingDeterministically, buildOccupiedCells } from './shared'
import type { BuildingId } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const forceRequiredStructures = (context: GenerationContext): number => {
  const occupied = buildOccupiedCells(context)
  let forcedCount = 0

  for (const [buildingId, target] of Object.entries(context.archetype.buildingTargets) as Array<[
    BuildingId,
    { min: number; max: number },
  ]>) {
    if (target.min <= 0) {
      continue
    }

    let placedCount = context.buildingPlacements.filter((placement) => placement.buildingId === buildingId).length
    while (placedCount < target.min) {
      const placement = placeBuildingDeterministically(context, buildingId, occupied)
      if (!placement) {
        break
      }

      context.buildingPlacements.push(placement)
      placedCount += 1
      forcedCount += 1
    }
  }

  return forcedCount
}
