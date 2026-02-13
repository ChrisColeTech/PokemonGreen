import type { BuildingId, BuildingRotation, GridPoint, TileGrid } from './editor'

export interface SeededRng {
  next: () => number
  int: (minInclusive: number, maxInclusive: number) => number
  chance: (probability: number) => boolean
  pick: <T>(items: readonly T[]) => T
  shuffle: <T>(items: readonly T[]) => T[]
  fork: (stream: string) => SeededRng
}

export interface ReservedDistrict {
  id: string
  x: number
  y: number
  width: number
  height: number
}

export interface GenerationState {
  primaryPathCells: GridPoint[]
  reservedDistricts: ReservedDistrict[]
  encounterAnchorCells: GridPoint[]
  activeTemplateId: GenerationTemplateId | null
  templateHints: GenerationTemplateHints | null
}

export type GenerationPassId =
  | 'initialize'
  | 'carvePrimaryPaths'
  | 'paintBiomes'
  | 'reserveDistricts'
  | 'placeBuildings'
  | 'placeEncounters'
  | 'placeInteractivesAndEntities'
  | 'balance'
  | 'validate'
  | 'repair'
  | 'finalize'

export type HardConstraintId =
  | 'boundedMap'
  | 'knownTileIdsOnly'
  | 'reachableCriticalPath'
  | 'buildingFootprintsInBounds'
  | 'buildingDoorConnectivity'
  | 'minRequiredStructures'
  | 'spawnSafety'

export type SoftGoalId =
  | 'routeReadability'
  | 'biomeVariety'
  | 'townCoherence'
  | 'encounterPacing'
  | 'landmarkVisibility'

export type ArchetypeId =
  | 'town_route_basic'
  | 'coastal_town_route'
  | 'forest_town_route'
  | 'mountain_pass_town_route'
  | 'riverlands_town_route'
  | 'lakeside_hamlet_route'
  | 'canyon_corridor_route'
  | 'meadow_outskirts_route'
  | 'deep_cave'
  | 'surfing_route'
  | 'underwater_dive'
  | 'volcano_crater'
  | 'seaside_boardwalk'
  | 'dense_forest'
  | 'gym_arena'
  | 'battle_tower'
  | 'mountain_peak'
  | 'moon_colony'
  | 'ship_deck'
  | 'safari_zone'

export type GenerationTemplateId =
  | 'compact_town_spine'
  | 'northern_crossing'
  | 'southern_wilds'
  | 'central_switchbacks'
  | 'riverbend_market'
  | 'cliffside_detour'
  | 'eastward_promontory'
  | 'westwood_weave'
  | 'twin_meadow_lane'
  | 'lowland_bypass'
  | 'highland_sweep'
  | 'winding_passages'
  | 'crystal_depths'
  | 'ocean_crossing'
  | 'archipelago_drift'
  | 'abyssal_trench'
  | 'coral_reef'
  | 'lava_flows'
  | 'crater_rim'
  | 'beachfront_row'
  | 'pier_plaza'
  | 'forest_maze'
  | 'canopy_trail'
  | 'gym_leader_hall'
  | 'trainer_gauntlet'
  | 'tower_arena'
  | 'tower_basement'
  | 'summit_path'
  | 'cliff_edge'
  | 'lunar_surface'
  | 'crater_base'
  | 'cruise_deck'
  | 'cargo_hold'
  | 'zone_north'
  | 'zone_south'

export type EncounterZoneBias = 'balanced' | 'west' | 'east'

export interface PrimaryPathTemplateHint {
  startYRatio: number
  minYRatio: number
  maxYRatio: number
  meanderChance: number
}

export interface TownTemplateHint {
  anchorXRatio: number
  anchorYRatio: number
  widthRatio: number
  heightRatio: number
}

export interface GenerationTemplateHints {
  primaryPath?: PrimaryPathTemplateHint
  town?: TownTemplateHint
  encounterZone?: EncounterZoneBias
}

export interface GenerationTemplate {
  id: GenerationTemplateId
  label: string
  description: string
  hints: GenerationTemplateHints
}

export interface GenerationDimensions {
  width: number
  height: number
}

export interface BuildingTargetRange {
  min: number
  max: number
}

export interface TileRoleSet {
  baseTerrainTileId: number
  primaryPathTileId: number
  optionalWaterTileIds: number[]
  optionalEncounterTileIds: number[]
}

export interface RandomMapArchetype {
  id: ArchetypeId
  label: string
  description: string
  recommendedDimensions: GenerationDimensions
  passOrder: GenerationPassId[]
  requiredHardConstraints: HardConstraintId[]
  softGoalWeights: Record<SoftGoalId, number>
  buildingTargets: Partial<Record<BuildingId, BuildingTargetRange>>
  tileRoles: TileRoleSet
}

export interface GenerationPipelineOptions {
  passOrderOverride?: GenerationPassId[]
  maxRepairAttempts: number
}

export interface RandomGenerationConfig {
  seed: string
  dimensions: GenerationDimensions
  archetypeId: ArchetypeId
  templateId: GenerationTemplateId | null
  baseFillTileId: number
  hardConstraintPolicy: Record<HardConstraintId, boolean>
  softGoalWeightsOverride?: Partial<Record<SoftGoalId, number>>
  pipeline: GenerationPipelineOptions
}

export interface GeneratedBuildingPlacement {
  buildingId: BuildingId
  rotation: BuildingRotation
  anchor: GridPoint
}

export interface ValidationIssue {
  id: HardConstraintId
  severity: 'error' | 'warning'
  message: string
  cells?: GridPoint[]
}

export interface SoftGoalScore {
  id: SoftGoalId
  score: number
  weight: number
  weightedScore: number
}

export interface GenerationRepairAction {
  id: string
  description: string
  applied: boolean
}

export interface GenerationDiagnostics {
  warnings: string[]
  passDurationsMs: Partial<Record<GenerationPassId, number>>
  hardConstraintIssues: ValidationIssue[]
  softGoalScores: SoftGoalScore[]
  appliedRepairs: GenerationRepairAction[]
}

export interface GenerationContext {
  config: RandomGenerationConfig
  archetype: RandomMapArchetype
  rng: SeededRng
  grid: TileGrid
  buildingPlacements: GeneratedBuildingPlacement[]
  state: GenerationState
  diagnostics: GenerationDiagnostics
}

export interface RandomGenerationResult {
  grid: TileGrid
  width: number
  height: number
  displayName: string
  archetypeId: ArchetypeId
  seed: string
  diagnostics: GenerationDiagnostics
}

export interface GenerationPass {
  id: GenerationPassId
  run: (context: GenerationContext) => GenerationContext
}
