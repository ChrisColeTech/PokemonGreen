import { useMemo } from 'react'
import { RANDOM_MAP_ARCHETYPE_PRESETS } from '../data/generation/archetypes'
import { GENERATION_STARTER_TEMPLATES } from '../data/generation/templates'
import { summarizeGenerationDiagnostics } from '../services/generation/generationDiagnosticsService'
import { useEditorStore } from '../store/editorStore'
import {
  selectRandomGenerationActions,
  selectRandomGenerationState,
} from '../store/selectors/randomGenerationSelectors'
import { useShallow } from 'zustand/react/shallow'

const parseNumericValue = (value: string, fallback: number) => {
  const parsed = Number.parseInt(value, 10)
  return Number.isNaN(parsed) ? fallback : parsed
}

export const useRandomGenerationControls = () => {
  const {
    mapWidth,
    mapHeight,
    generationSeedInput,
    generationArchetypeId,
    generationTemplateId,
    generationWidthInput,
    generationHeightInput,
    generationUseCurrentDimensions,
    generationMaxRepairAttempts,
    generationEnforceSpawnSafety,
    generationEnforceDoorConnectivity,
    lastGeneratedSeed,
    lastGenerationDiagnostics,
  } = useEditorStore(useShallow(selectRandomGenerationState))

  const {
    setGenerationSeedInput,
    setGenerationArchetypeId,
    setGenerationTemplateId,
    setGenerationWidthInput,
    setGenerationHeightInput,
    setGenerationUseCurrentDimensions,
    setGenerationMaxRepairAttempts,
    setGenerationEnforceSpawnSafety,
    setGenerationEnforceDoorConnectivity,
    generateRandomMapFromControls,
    regenerateRandomMap,
  } = useEditorStore(useShallow(selectRandomGenerationActions))

  const diagnosticsSummary = useMemo(
    () => summarizeGenerationDiagnostics(lastGenerationDiagnostics),
    [lastGenerationDiagnostics],
  )

  return {
    archetypeOptions: RANDOM_MAP_ARCHETYPE_PRESETS,
    templateOptions: GENERATION_STARTER_TEMPLATES,
    currentMapWidth: mapWidth,
    currentMapHeight: mapHeight,
    generationSeedInput,
    generationArchetypeId,
    generationTemplateId,
    generationWidthInput,
    generationHeightInput,
    generationUseCurrentDimensions,
    generationMaxRepairAttempts,
    generationEnforceSpawnSafety,
    generationEnforceDoorConnectivity,
    lastGeneratedSeed,
    lastGenerationDiagnostics,
    diagnosticsSummary,
    canRegenerate: Boolean((lastGeneratedSeed ?? generationSeedInput).trim()),
    setGenerationSeedInput,
    setGenerationArchetypeId,
    setGenerationTemplateId,
    setGenerationWidthInput: (value: string) =>
      setGenerationWidthInput(parseNumericValue(value, generationWidthInput)),
    setGenerationHeightInput: (value: string) =>
      setGenerationHeightInput(parseNumericValue(value, generationHeightInput)),
    setGenerationUseCurrentDimensions,
    setGenerationMaxRepairAttempts: (value: string) =>
      setGenerationMaxRepairAttempts(parseNumericValue(value, generationMaxRepairAttempts)),
    setGenerationEnforceSpawnSafety,
    setGenerationEnforceDoorConnectivity,
    generateRandomMapFromControls,
    regenerateRandomMap,
  }
}
