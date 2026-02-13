const LEGACY_STORAGE_KEY = 'pokemonGreenMap'
const GRID_STORAGE_KEY = 'map-editor-grid'
const UI_STORAGE_KEY = 'map-editor-ui'

export const clearPersistedEditorData = (): void => {
  localStorage.removeItem(LEGACY_STORAGE_KEY)
  localStorage.removeItem(GRID_STORAGE_KEY)
  localStorage.removeItem(UI_STORAGE_KEY)
}
