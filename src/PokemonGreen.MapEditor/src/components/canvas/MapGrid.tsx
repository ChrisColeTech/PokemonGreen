import { useCallback, useRef } from 'react'
import { useEditorStore } from '../../store/editorStore'

const ARROWS: Record<string, string> = {
  up: '\u25B2',
  down: '\u25BC',
  left: '\u25C0',
  right: '\u25B6',
}

export function MapGrid() {
  const mapData = useEditorStore(s => s.mapData)
  const cellSize = useEditorStore(s => s.cellSize)
  const selectedBuilding = useEditorStore(s => s.selectedBuilding)
  const paint = useEditorStore(s => s.paint)
  const placeBuilding = useEditorStore(s => s.placeBuilding)
  const getTile = useEditorStore(s => s.getTile)
  const isDrawing = useRef(false)

  const handleMouseDown = useCallback((x: number, y: number, e: React.MouseEvent) => {
    e.preventDefault()
    if (e.button === 2) {
      paint(x, y, 1)
    } else if (selectedBuilding !== null) {
      placeBuilding(x, y)
    } else {
      isDrawing.current = true
      paint(x, y)
    }
  }, [paint, placeBuilding, selectedBuilding])

  const handleMouseEnter = useCallback((x: number, y: number) => {
    if (isDrawing.current && selectedBuilding === null) {
      paint(x, y)
    }
  }, [paint, selectedBuilding])

  const handleMouseUp = useCallback(() => {
    isDrawing.current = false
  }, [])

  const width = mapData[0].length
  const gridPx = width * cellSize + (width + 1)

  return (
    <div
      className="select-none"
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseUp}
      onContextMenu={(e) => e.preventDefault()}
    >
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: `repeat(${width}, ${cellSize}px)`,
          gap: '1px',
          background: '#0f0f23',
          padding: '1px',
          width: `${gridPx}px`,
          position: 'relative',
        }}
      >
        {mapData.map((row, y) =>
          row.map((tileId, x) => {
            const tile = getTile(tileId)
            return (
              <div
                key={`${x}-${y}`}
                style={{
                  width: cellSize,
                  height: cellSize,
                  background: tile.color,
                  cursor: 'crosshair',
                  borderRadius: 2,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 10,
                  color: '#e0e0e0',
                }}
                onMouseDown={(e) => handleMouseDown(x, y, e)}
                onMouseEnter={(e) => { e.currentTarget.style.opacity = '0.7'; handleMouseEnter(x, y) }}
                onMouseLeave={(e) => { e.currentTarget.style.opacity = '1' }}
              >
                {tile.encounter && '!'}
                {tile.category === 'trainer' && tile.direction && ARROWS[tile.direction]}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}
