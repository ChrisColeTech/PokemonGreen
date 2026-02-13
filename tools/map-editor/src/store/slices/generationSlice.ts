import { RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID } from '../../data/generation/archetypes'
import { GENERATION_STARTER_TEMPLATES_BY_ID } from '../../data/generation/templates'
import { createDefaultRandomGenerationConfig, generateRandomMap } from '../../services/generation/randomMapGenerator'
import { clamp, MAX_DIMENSION, MIN_DIMENSION } from '../../services/gridService'
import type { EditorStoreSlice } from '../editorStore.types'
import {
  cloneGrid,
  defaultArchetype,
  DEFAULT_GENERATION_ARCHETYPE_ID,
  DEFAULT_HARD_CONSTRAINT_POLICY,
  MAX_GENERATION_REPAIR_ATTEMPTS,
  pushPastGrid,
  resolveGenerationSeed,
} from './shared'

type GenerationSlice = Pick<
  import('../editorStore.types').EditorStoreState,
  | 'generationSeedInput'
  | 'generationArchetypeId'
  | 'generationTemplateId'
  | 'generationWidthInput'
  | 'generationHeightInput'
  | 'generationUseCurrentDimensions'
  | 'generationMaxRepairAttempts'
  | 'generationEnforceSpawnSafety'
  | 'generationEnforceDoorConnectivity'
  | 'lastGeneratedSeed'
  | 'lastGenerationDiagnostics'
  | 'setGenerationSeedInput'
  | 'setGenerationArchetypeId'
  | 'setGenerationTemplateId'
  | 'setGenerationWidthInput'
  | 'setGenerationHeightInput'
  | 'setGenerationUseCurrentDimensions'
  | 'setGenerationMaxRepairAttempts'
  | 'setGenerationEnforceSpawnSafety'
  | 'setGenerationEnforceDoorConnectivity'
  | 'generateRandomMapFromControls'
  | 'regenerateRandomMap'
>

export const createGenerationSlice: EditorStoreSlice<GenerationSlice> = (set, get) => ({
  generationSeedInput: '',
  generationArchetypeId: DEFAULT_GENERATION_ARCHETYPE_ID,
  generationTemplateId: null,
  generationWidthInput: defaultArchetype.recommendedDimensions.width,
  generationHeightInput: defaultArchetype.recommendedDimensions.height,
  generationUseCurrentDimensions: true,
  generationMaxRepairAttempts: 3,
  generationEnforceSpawnSafety: true,
  generationEnforceDoorConnectivity: true,
  lastGeneratedSeed: null,
  lastGenerationDiagnostics: null,
  setGenerationSeedInput: (value) =>
    set({
      generationSeedInput: value,
    }),
  setGenerationArchetypeId: (value) =>
    set((state) => {
      const archetype = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[value]
      if (!archetype) {
        return state
      }

      if (state.generationUseCurrentDimensions) {
        return {
          generationArchetypeId: value,
        }
      }

      return {
        generationArchetypeId: value,
        generationWidthInput: archetype.recommendedDimensions.width,
        generationHeightInput: archetype.recommendedDimensions.height,
      }
    }),
  setGenerationTemplateId: (value) =>
    set((state) => {
      if (value === null) {
        return {
          generationTemplateId: null,
        }
      }

      if (!GENERATION_STARTER_TEMPLATES_BY_ID[value]) {
        return state
      }

      return {
        generationTemplateId: value,
      }
    }),
  setGenerationWidthInput: (value) =>
    set({
      generationWidthInput: Number.isFinite(value) ? value : defaultArchetype.recommendedDimensions.width,
    }),
  setGenerationHeightInput: (value) =>
    set({
      generationHeightInput: Number.isFinite(value) ? value : defaultArchetype.recommendedDimensions.height,
    }),
  setGenerationUseCurrentDimensions: (value) =>
    set({
      generationUseCurrentDimensions: value,
    }),
  setGenerationMaxRepairAttempts: (value) =>
    set({
      generationMaxRepairAttempts: clamp(
        Number.isFinite(value) ? value : 0,
        0,
        MAX_GENERATION_REPAIR_ATTEMPTS,
      ),
    }),
  setGenerationEnforceSpawnSafety: (value) =>
    set({
      generationEnforceSpawnSafety: value,
    }),
  setGenerationEnforceDoorConnectivity: (value) =>
    set({
      generationEnforceDoorConnectivity: value,
    }),
  generateRandomMapFromControls: (seedOverride) => {
    const state = get()
    const seed = resolveGenerationSeed(seedOverride ?? state.generationSeedInput)
    const dimensions = state.generationUseCurrentDimensions
      ? {
          width: state.mapWidth,
          height: state.mapHeight,
        }
      : {
          width: clamp(state.generationWidthInput, MIN_DIMENSION, MAX_DIMENSION),
          height: clamp(state.generationHeightInput, MIN_DIMENSION, MAX_DIMENSION),
        }
    const config = createDefaultRandomGenerationConfig({
      seed,
      archetypeId: state.generationArchetypeId,
      templateId: state.generationTemplateId,
      dimensions,
      hardConstraintPolicy: {
        ...DEFAULT_HARD_CONSTRAINT_POLICY,
        spawnSafety: state.generationEnforceSpawnSafety,
        buildingDoorConnectivity: state.generationEnforceDoorConnectivity,
      },
      pipeline: {
        maxRepairAttempts: state.generationMaxRepairAttempts,
      },
    })
    const result = generateRandomMap(config)

    set((currentState) => ({
      mapName: result.displayName,
      mapWidth: result.width,
      mapHeight: result.height,
      widthInput: result.width,
      heightInput: result.height,
      drawMode: null,
      grid: cloneGrid(result.grid),
      pastGrids: pushPastGrid(currentState.pastGrids, currentState.grid),
      futureGrids: [],
      drawStartGrid: null,
      generationSeedInput: seed,
      generationWidthInput: result.width,
      generationHeightInput: result.height,
      lastGeneratedSeed: seed,
      lastGenerationDiagnostics: result.diagnostics,
    }))
  },
  regenerateRandomMap: () => {
    const state = get()
    const seed = state.lastGeneratedSeed?.trim() || state.generationSeedInput.trim()
    if (!seed) {
      return
    }

    state.generateRandomMapFromControls(seed)
  },
})
