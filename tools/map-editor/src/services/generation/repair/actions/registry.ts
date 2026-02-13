import { connectBuildingDoors } from './connectBuildingDoors'
import { enforceSpawnSafety } from './enforceSpawnSafety'
import { forceRequiredStructures } from './forceRequiredStructures'
import { normalizeGridBounds } from './normalizeGridBounds'
import { reconnectPathComponents } from './reconnectPathComponents'
import { repairBuildingFootprints } from './repairBuildingFootprints'
import { replaceUnknownTileIds } from './replaceUnknownTileIds'
import type { GenerationContext, HardConstraintId } from '../../../../types/generation'

export type RepairActionDescriptor = {
  issueId: HardConstraintId
  idPrefix: string
  run: (context: GenerationContext) => unknown
  describe: (result: unknown) => string
  isApplied: (result: unknown) => boolean
  defaultResult: unknown
}

export const HARD_CONSTRAINT_REPAIR_ACTION_REGISTRY: RepairActionDescriptor[] = [
  {
    issueId: 'boundedMap',
    idPrefix: 'repair-bounded-map',
    run: normalizeGridBounds,
    describe: () => 'Normalize grid bounds to configured dimensions.',
    isApplied: (result) => Boolean(result),
    defaultResult: false,
  },
  {
    issueId: 'knownTileIdsOnly',
    idPrefix: 'repair-known-tile-ids',
    run: replaceUnknownTileIds,
    describe: (result) => `Replace unknown tile ids with base terrain (${result as number} cells).`,
    isApplied: (result) => (result as number) > 0,
    defaultResult: 0,
  },
  {
    issueId: 'reachableCriticalPath',
    idPrefix: 'repair-reconnect-critical-path',
    run: reconnectPathComponents,
    describe: () => 'Reconnect disconnected primary path segments.',
    isApplied: (result) => Boolean(result),
    defaultResult: false,
  },
  {
    issueId: 'buildingDoorConnectivity',
    idPrefix: 'repair-building-door-connectivity',
    run: connectBuildingDoors,
    describe: () => 'Connect building doors to nearby path cells.',
    isApplied: (result) => Boolean(result),
    defaultResult: false,
  },
  {
    issueId: 'buildingFootprintsInBounds',
    idPrefix: 'repair-building-footprints',
    run: repairBuildingFootprints,
    describe: (result) => `Relocate or remove out-of-bounds buildings (${result as number} placements).`,
    isApplied: (result) => (result as number) > 0,
    defaultResult: 0,
  },
  {
    issueId: 'minRequiredStructures',
    idPrefix: 'repair-force-required-structures',
    run: forceRequiredStructures,
    describe: (result) => `Force minimum required buildings when placement is possible (${result as number} added).`,
    isApplied: (result) => (result as number) > 0,
    defaultResult: 0,
  },
  {
    issueId: 'spawnSafety',
    idPrefix: 'repair-spawn-safety',
    run: enforceSpawnSafety,
    describe: () => 'Enforce walkable and escapable spawn cell.',
    isApplied: (result) => Boolean(result),
    defaultResult: false,
  },
]
