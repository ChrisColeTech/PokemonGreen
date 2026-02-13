import { boundedMapValidator } from './boundedMap'
import { buildingDoorConnectivityValidator } from './buildingDoorConnectivity'
import { buildingFootprintsInBoundsValidator } from './buildingFootprintsInBounds'
import { knownTileIdsOnlyValidator } from './knownTileIdsOnly'
import { minRequiredStructuresValidator } from './minRequiredStructures'
import { reachableCriticalPathValidator } from './reachableCriticalPath'
import { spawnSafetyValidator } from './spawnSafety'
import type { GenerationContext, HardConstraintId, ValidationIssue } from '../../../../types/generation'

export type HardConstraintValidator = (context: GenerationContext) => ValidationIssue[]

export const HARD_CONSTRAINT_VALIDATOR_REGISTRY: Record<HardConstraintId, HardConstraintValidator> = {
  boundedMap: boundedMapValidator,
  knownTileIdsOnly: knownTileIdsOnlyValidator,
  reachableCriticalPath: reachableCriticalPathValidator,
  buildingFootprintsInBounds: buildingFootprintsInBoundsValidator,
  buildingDoorConnectivity: buildingDoorConnectivityValidator,
  minRequiredStructures: minRequiredStructuresValidator,
  spawnSafety: spawnSafetyValidator,
}
