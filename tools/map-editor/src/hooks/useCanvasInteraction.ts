import { type MouseEvent, useEffect, useMemo, useState } from 'react'
import { BUILDINGS_BY_ID } from '../data/buildings'
import { getPlacementCells } from '../services/buildingService'
import { useEditorStore } from '../store/editorStore'
import { selectCanvasActions, selectCanvasState } from '../store/selectors/canvasSelectors'
import type { GridPoint } from '../types/editor'
import { useShallow } from 'zustand/react/shallow'

export const useCanvasInteraction = () => {
  const { mapWidth, mapHeight, drawMode, selectedBuildingId, buildingRotation } = useEditorStore(
    useShallow(selectCanvasState),
  )

  const { beginDraw, endDraw, paintCell, placeBuilding } = useEditorStore(useShallow(selectCanvasActions))

  const [hoverCell, setHoverCell] = useState<GridPoint | null>(null)

  useEffect(() => {
    const handleMouseUp = () => {
      endDraw()
    }

    window.addEventListener('mouseup', handleMouseUp)
    return () => {
      window.removeEventListener('mouseup', handleMouseUp)
    }
  }, [endDraw])

  const onCellMouseDown = (event: MouseEvent<HTMLButtonElement>, x: number, y: number) => {
    event.preventDefault()

    if (event.button === 2) {
      beginDraw('erase')
      paintCell(x, y, 'erase')
      return
    }

    if (event.button !== 0) {
      return
    }

    if (selectedBuildingId) {
      placeBuilding(x, y)
      return
    }

    beginDraw('paint')
    paintCell(x, y, 'paint')
  }

  const onCellMouseEnter = (x: number, y: number) => {
    if (selectedBuildingId) {
      setHoverCell({ x, y })
      return
    }

    if (!drawMode) {
      return
    }

    paintCell(x, y, drawMode)
  }

  const onGridMouseLeave = () => {
    setHoverCell(null)
  }

  const previewCells = useMemo(() => {
    if (!selectedBuildingId || !hoverCell) {
      return []
    }

    const building = BUILDINGS_BY_ID[selectedBuildingId]
    if (!building) {
      return []
    }

    return getPlacementCells(building, buildingRotation, hoverCell.x, hoverCell.y, mapWidth, mapHeight)
  }, [buildingRotation, hoverCell, mapHeight, mapWidth, selectedBuildingId])

  return {
    onCellMouseDown,
    onCellMouseEnter,
    onGridMouseLeave,
    previewCells,
  }
}
