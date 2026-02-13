import { useMemo } from 'react'
import { BUILDINGS, BUILDINGS_BY_ID } from '../data/buildings'
import { TILES } from '../data/tiles'
import { rotateBuildingFootprint } from '../services/buildingService'
import { clearPersistedEditorData } from '../services/storageService'
import { useEditorStore } from '../store/editorStore'
import { selectSidebarActions, selectSidebarState } from '../store/selectors/sidebarSelectors'
import type { BuildingId, PaletteCategory } from '../types/editor'
import { useShallow } from 'zustand/react/shallow'

const parseNumericValue = (value: string, fallback: number) => {
  const parsed = Number.parseInt(value, 10)
  return Number.isNaN(parsed) ? fallback : parsed
}

export const useSidebarControls = () => {
  const {
    selectedCategory,
    selectedTileId,
    selectedBuildingId,
    buildingRotation,
    mapName,
    widthInput,
    heightInput,
    cellSize,
  } = useEditorStore(useShallow(selectSidebarState))

  const {
    setSelectedCategory,
    setSelectedTileId,
    toggleSelectedBuilding,
    rotateSelectedBuilding,
    setMapName,
    setWidthInput,
    setHeightInput,
    setCellSize,
    resizeGridFromInputs,
    clearGrid,
    resetToDefaults,
  } = useEditorStore(useShallow(selectSidebarActions))

  const visibleTiles = useMemo(
    () => (selectedCategory === 'buildings' ? [] : TILES.filter((tile) => tile.category === selectedCategory)),
    [selectedCategory],
  )

  const selectedBuilding = selectedBuildingId ? BUILDINGS_BY_ID[selectedBuildingId] : null
  const rotatedBuilding = selectedBuilding ? rotateBuildingFootprint(selectedBuilding, buildingRotation) : null

  const handleSelectCategory = (category: PaletteCategory) => {
    setSelectedCategory(category)
  }

  const handleSelectTile = (tileId: number) => {
    setSelectedTileId(tileId)
  }

  const handleToggleBuilding = (buildingId: BuildingId) => {
    toggleSelectedBuilding(buildingId)
  }

  const handleRotateBuilding = (direction: -1 | 1) => {
    rotateSelectedBuilding(direction)
  }

  const handleResetSaved = () => {
    if (!window.confirm('Clear saved data?')) {
      return
    }

    clearPersistedEditorData()
    resetToDefaults()
  }

  return {
    selectedCategory,
    selectedTileId,
    selectedBuildingId,
    buildingRotation,
    mapName,
    widthInput,
    heightInput,
    cellSize,
    visibleTiles,
    buildings: BUILDINGS,
    selectedBuilding,
    rotatedBuilding,
    setMapName,
    setWidthInput: (value: string) => setWidthInput(parseNumericValue(value, widthInput)),
    setHeightInput: (value: string) => setHeightInput(parseNumericValue(value, heightInput)),
    setCellSize: (value: string) => setCellSize(parseNumericValue(value, cellSize)),
    resizeGridFromInputs,
    clearGrid,
    handleSelectCategory,
    handleSelectTile,
    handleToggleBuilding,
    handleRotateBuilding,
    handleResetSaved,
  }
}
