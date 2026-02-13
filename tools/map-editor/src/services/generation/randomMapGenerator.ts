import {
  DEFAULT_GENERATION_MAX_REPAIR_ATTEMPTS,
  DEFAULT_GENERATION_PASSES,
  DEFAULT_HARD_CONSTRAINT_POLICY,
  RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID,
} from '../../data/generation/archetypes'
import { GRASS_TILE_ID } from '../../data/tiles'
import { createGrid } from '../gridService'
import { createSeededRng } from './seededRng'
import { balancePass } from './passes/balancePass'
import { carvePrimaryPathsPass } from './passes/carvePrimaryPathsPass'
import { finalizePass } from './passes/finalizePass'
import { initializePass } from './passes/initializePass'
import { placeBuildingsPass } from './passes/placeBuildingsPass'
import { placeEncountersPass } from './passes/placeEncountersPass'
import { placeInteractivesAndEntitiesPass } from './passes/placeInteractivesAndEntitiesPass'
import { paintBiomesPass } from './passes/paintBiomesPass'
import { repairPass } from './passes/repairPass'
import { reserveDistrictsPass } from './passes/reserveDistrictsPass'
import { validatePass } from './passes/validatePass'
import type {
  ArchetypeId,
  GenerationContext,
  GenerationDiagnostics,
  GenerationPass,
  GenerationPassId,
  HardConstraintId,
  RandomGenerationConfig,
  RandomGenerationResult,
} from '../../types/generation'

const DEFAULT_ARCHETYPE_ID: ArchetypeId = 'town_route_basic'

const nowMs = (): number => (typeof performance !== 'undefined' ? performance.now() : Date.now())

const createEmptyDiagnostics = (): GenerationDiagnostics => ({
  warnings: [],
  passDurationsMs: {},
  hardConstraintIssues: [],
  softGoalScores: [],
  appliedRepairs: [],
})

const PASS_REGISTRY: Record<GenerationPassId, GenerationPass> = {
  initialize: initializePass,
  carvePrimaryPaths: carvePrimaryPathsPass,
  paintBiomes: paintBiomesPass,
  reserveDistricts: reserveDistrictsPass,
  placeBuildings: placeBuildingsPass,
  placeEncounters: placeEncountersPass,
  placeInteractivesAndEntities: placeInteractivesAndEntitiesPass,
  balance: balancePass,
  validate: validatePass,
  repair: repairPass,
  finalize: finalizePass,
}

const resolvePassOrder = (config: RandomGenerationConfig): GenerationPassId[] => {
  if (config.pipeline.passOrderOverride && config.pipeline.passOrderOverride.length > 0) {
    return [...config.pipeline.passOrderOverride]
  }

  const archetype = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[config.archetypeId]
  return archetype ? [...archetype.passOrder] : [...DEFAULT_GENERATION_PASSES]
}

const runPassPipeline = (context: GenerationContext, passOrder: GenerationPassId[]): GenerationContext => {
  let nextContext = context

  for (const passId of passOrder) {
    const pass = PASS_REGISTRY[passId]
    const start = nowMs()
    nextContext = pass.run(nextContext)
    nextContext.diagnostics.passDurationsMs[passId] = nowMs() - start
  }

  return nextContext
}

const applyHardConstraintPolicy = (context: GenerationContext): GenerationContext => {
  const disabledConstraints = Object.keys(context.config.hardConstraintPolicy).filter((constraintId) => {
    const typedConstraintId = constraintId as HardConstraintId
    return !context.config.hardConstraintPolicy[typedConstraintId]
  })

  if (disabledConstraints.length > 0) {
    context.diagnostics.warnings.push(
      `Hard constraints disabled by config: ${disabledConstraints.join(', ')}`,
    )
  }

  return context
}

const buildInitialContext = (config: RandomGenerationConfig): GenerationContext => {
  const archetype = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[config.archetypeId]
  const width = config.dimensions.width
  const height = config.dimensions.height
  const rng = createSeededRng(config.seed)

  return {
    config,
    archetype,
    rng,
    grid: createGrid(width, height, config.baseFillTileId),
    buildingPlacements: [],
    state: {
      primaryPathCells: [],
      reservedDistricts: [],
      encounterAnchorCells: [],
      activeTemplateId: null,
      templateHints: null,
    },
    diagnostics: createEmptyDiagnostics(),
  }
}

export const createDefaultRandomGenerationConfig = (
  overrides: Partial<RandomGenerationConfig> = {},
): RandomGenerationConfig => {
  const selectedArchetype = overrides.archetypeId ?? DEFAULT_ARCHETYPE_ID
  const preset = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[selectedArchetype]

  return {
    seed: overrides.seed ?? `${Date.now()}`,
    dimensions: overrides.dimensions ?? {
      width: preset.recommendedDimensions.width,
      height: preset.recommendedDimensions.height,
    },
    archetypeId: selectedArchetype,
    templateId: overrides.templateId ?? null,
    baseFillTileId: overrides.baseFillTileId ?? preset.tileRoles.baseTerrainTileId ?? GRASS_TILE_ID,
    hardConstraintPolicy: overrides.hardConstraintPolicy ?? { ...DEFAULT_HARD_CONSTRAINT_POLICY },
    softGoalWeightsOverride: overrides.softGoalWeightsOverride,
    pipeline: {
      passOrderOverride: overrides.pipeline?.passOrderOverride,
      maxRepairAttempts: overrides.pipeline?.maxRepairAttempts ?? DEFAULT_GENERATION_MAX_REPAIR_ATTEMPTS,
    },
  }
}

export const generateRandomMap = (inputConfig: RandomGenerationConfig): RandomGenerationResult => {
  let context = buildInitialContext(inputConfig)
  context = applyHardConstraintPolicy(context)

  const passOrder = resolvePassOrder(inputConfig)
  context = runPassPipeline(context, passOrder)

  return {
    grid: context.grid,
    width: inputConfig.dimensions.width,
    height: inputConfig.dimensions.height,
    displayName: `Generated ${context.archetype.label}`,
    archetypeId: inputConfig.archetypeId,
    seed: inputConfig.seed,
    diagnostics: context.diagnostics,
  }
}
