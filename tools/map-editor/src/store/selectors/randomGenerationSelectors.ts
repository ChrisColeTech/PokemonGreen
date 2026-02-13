import type { EditorStoreState } from '../editorStore.types'

export const selectRandomGenerationState = (state: EditorStoreState) => ({
  mapWidth: state.mapWidth,
  mapHeight: state.mapHeight,
  generationSeedInput: state.generationSeedInput,
  generationArchetypeId: state.generationArchetypeId,
  generationTemplateId: state.generationTemplateId,
  generationWidthInput: state.generationWidthInput,
  generationHeightInput: state.generationHeightInput,
  generationUseCurrentDimensions: state.generationUseCurrentDimensions,
  generationMaxRepairAttempts: state.generationMaxRepairAttempts,
  generationEnforceSpawnSafety: state.generationEnforceSpawnSafety,
  generationEnforceDoorConnectivity: state.generationEnforceDoorConnectivity,
  lastGeneratedSeed: state.lastGeneratedSeed,
  lastGenerationDiagnostics: state.lastGenerationDiagnostics,
})

export const selectRandomGenerationActions = (state: EditorStoreState) => ({
  setGenerationSeedInput: state.setGenerationSeedInput,
  setGenerationArchetypeId: state.setGenerationArchetypeId,
  setGenerationTemplateId: state.setGenerationTemplateId,
  setGenerationWidthInput: state.setGenerationWidthInput,
  setGenerationHeightInput: state.setGenerationHeightInput,
  setGenerationUseCurrentDimensions: state.setGenerationUseCurrentDimensions,
  setGenerationMaxRepairAttempts: state.setGenerationMaxRepairAttempts,
  setGenerationEnforceSpawnSafety: state.setGenerationEnforceSpawnSafety,
  setGenerationEnforceDoorConnectivity: state.setGenerationEnforceDoorConnectivity,
  generateRandomMapFromControls: state.generateRandomMapFromControls,
  regenerateRandomMap: state.regenerateRandomMap,
})
