import { useMemo } from 'react'
import { rotateBuildingFootprint } from '../services/buildingService'
import { getBuildingsById, getVisiblePaletteCategories } from '../services/registryService'
import { clearPersistedEditorData } from '../services/storageService'
import { useEditorStore } from '../store/editorStore'
import { selectSidebarActions, selectSidebarState } from '../store/selectors/sidebarSelectors'
import type { PaletteCategory } from '../types/editor'
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

  const activeRegistry = useEditorStore((state) => state.activeRegistry)

  const paletteCategories = useMemo(
    () => getVisiblePaletteCategories(activeRegistry),
    [activeRegistry],
  )

  const visibleTiles = useMemo(
    () =>
      selectedCategory === 'buildings'
        ? []
        : activeRegistry.tiles.filter((tile) => tile.category === selectedCategory),
    [activeRegistry.tiles, selectedCategory],
  )

  const buildings = activeRegistry.buildings
  const registryTiles = activeRegistry.tiles

  const buildingsByRegistryId = useMemo(
    () => getBuildingsById(activeRegistry),
    [activeRegistry],
  )

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

  const selectedBuilding = selectedBuildingId ? buildingsByRegistryId[selectedBuildingId] : null
  const rotatedBuilding = selectedBuilding ? rotateBuildingFootprint(selectedBuilding, buildingRotation) : null
  const buildingsWithDimensions = useMemo(
    () => buildings.map((building) => ({
      ...building,
      width: building.tiles[0]?.length ?? 0,
      height: building.tiles.length,
    })),
    [buildings],
  )

  const handleSelectCategory = (category: PaletteCategory) => {
    setSelectedCategory(category)
  }

  const handleSelectTile = (tileId: number) => {
    setSelectedTileId(tileId)
  }

  const handleToggleBuilding = (buildingId: string) => {
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
    paletteCategories,
    registryTiles,
    visibleTiles,
    buildings: buildingsWithDimensions,
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
