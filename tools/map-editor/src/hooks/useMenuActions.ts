import { useMemo, useState } from 'react'
import { DEFAULT_CELL_SIZE } from '../data/tiles'
import {
  buildVersionedJsonExportPayload,
  downloadTextFile,
  openTextFile,
  parseMapJsonPayload,
} from '../services/mapIoService'
import { useEditorStore } from '../store/editorStore'
import { selectMenuActions, selectMenuState } from '../store/selectors/menuSelectors'
import { useShallow } from 'zustand/react/shallow'

const DEFAULT_MAP_FILE_NAME = 'map.map.json'
const MIN_CELL_SIZE = 8
const MAX_CELL_SIZE = 48
const ZOOM_STEP = 2

const mapNameToFileBase = (value: string): string => {
  const normalized = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')

  return normalized || 'map'
}

const ensureJsonFileName = (fileName: string): string => {
  const trimmed = fileName.trim()
  if (!trimmed) {
    return DEFAULT_MAP_FILE_NAME
  }

  const lowerTrimmed = trimmed.toLowerCase()
  if (lowerTrimmed.endsWith('.map.json')) {
    return trimmed
  }

  if (lowerTrimmed.endsWith('.json')) {
    return `${trimmed.slice(0, -5)}.map.json`
  }

  return `${trimmed}.map.json`
}

export const useMenuActions = () => {
  const { mapName, mapWidth, mapHeight, grid, cellSize, canUndo, canRedo } = useEditorStore(
    useShallow(selectMenuState),
  )

  const { setCellSize, resetToDefaults, clearGrid, loadMapData, undo, redo } = useEditorStore(
    useShallow(selectMenuActions),
  )

  const [activeFileName, setActiveFileName] = useState(DEFAULT_MAP_FILE_NAME)

  const currentMapData = useMemo(
    () => ({
      width: mapWidth,
      height: mapHeight,
      tiles: grid,
    }),
    [grid, mapHeight, mapWidth],
  )

  const exportJsonWithName = (fileName: string) => {
    const payload = buildVersionedJsonExportPayload(currentMapData, fileName, mapName)
    downloadTextFile(fileName, JSON.stringify(payload, null, 2), 'application/json')
  }

  const getPreferredFileName = (): string => {
    if (activeFileName === DEFAULT_MAP_FILE_NAME) {
      return ensureJsonFileName(`${mapNameToFileBase(mapName)}.map.json`)
    }

    return ensureJsonFileName(activeFileName)
  }

  const importFromDisk = async () => {
    const selectedFile = await openTextFile()
    if (!selectedFile) {
      return
    }

    const parseResult = parseMapJsonPayload(selectedFile.content)
    if (!parseResult.ok) {
      window.alert(parseResult.error)
      return
    }

    loadMapData(parseResult.data)
    setActiveFileName(ensureJsonFileName(selectedFile.fileName))
  }

  const handleNewMap = () => {
    if (!window.confirm('Create a new map? Unsaved changes will be lost.')) {
      return
    }

    resetToDefaults()
    setActiveFileName(DEFAULT_MAP_FILE_NAME)
  }

  const handleOpenMap = async () => {
    await importFromDisk()
  }

  const handleImportJson = async () => {
    await importFromDisk()
  }

  const handleExportJson = () => {
    const normalizedFileName = getPreferredFileName()
    setActiveFileName(normalizedFileName)
    exportJsonWithName(normalizedFileName)
  }

  const handleSave = () => {
    const normalizedFileName = getPreferredFileName()
    setActiveFileName(normalizedFileName)
    exportJsonWithName(normalizedFileName)
  }

  const handleSaveAs = () => {
    const nextFileName = window.prompt('Save map as:', getPreferredFileName())
    if (nextFileName === null) {
      return
    }

    const normalizedFileName = ensureJsonFileName(nextFileName)
    setActiveFileName(normalizedFileName)
    exportJsonWithName(normalizedFileName)
  }

  const handleClear = () => {
    clearGrid()
  }

  const handleUndo = () => {
    undo()
  }

  const handleRedo = () => {
    redo()
  }

  const handleZoomIn = () => {
    setCellSize(cellSize + ZOOM_STEP)
  }

  const handleZoomOut = () => {
    setCellSize(cellSize - ZOOM_STEP)
  }

  const handleResetZoom = () => {
    setCellSize(DEFAULT_CELL_SIZE)
  }

  return {
    actions: {
      handleNewMap,
      handleOpenMap,
      handleImportJson,
      handleExportJson,
      handleSave,
      handleSaveAs,
      handleClear,
      handleUndo,
      handleRedo,
      handleZoomIn,
      handleZoomOut,
      handleResetZoom,
    },
    state: {
      canUndo,
      canRedo,
      canZoomIn: cellSize < MAX_CELL_SIZE,
      canZoomOut: cellSize > MIN_CELL_SIZE,
      activeFileName,
    },
  }
}
