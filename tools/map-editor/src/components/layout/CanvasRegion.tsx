import { GRASS_TILE_ID, TILES, TILES_BY_ID } from '../../data/tiles'
import { useCanvasInteraction } from '../../hooks/useCanvasInteraction'
import { createDistinctTileColorMap, getDisplayTileColor } from '../../services/tileColorService'
import { useEditorStore } from '../../store/editorStore'
import { useUiStore } from '../../store/uiStore'

const DISTINCT_TILE_COLOR_MAP = createDistinctTileColorMap(TILES)

function CanvasRegion() {
  const directionIcons = {
    up: '▲',
    down: '▼',
    left: '◀',
    right: '▶',
  } as const

  const mapWidth = useEditorStore((state) => state.mapWidth)
  const mapHeight = useEditorStore((state) => state.mapHeight)
  const cellSize = useEditorStore((state) => state.cellSize)
  const grid = useEditorStore((state) => state.grid)
  const useDistinctColors = useUiStore((state) => state.useDistinctColors)
  const { onCellMouseDown, onCellMouseEnter, onGridMouseLeave, previewCells } = useCanvasInteraction()

  return (
    <section className="flex min-w-0 flex-1 overflow-auto p-3 md:p-4">
      <div
        className="relative inline-grid h-fit rounded border border-[#0f0f23] bg-[#0f0f23] p-px"
        style={{
          gridTemplateColumns: `repeat(${mapWidth}, ${cellSize}px)`,
          gridTemplateRows: `repeat(${mapHeight}, ${cellSize}px)`,
          gap: '1px',
        }}
        onContextMenu={(event) => event.preventDefault()}
        onMouseLeave={onGridMouseLeave}
      >
        {grid.map((row, y) =>
          row.map((tileId, x) => {
            const tile = TILES_BY_ID[tileId] ?? TILES_BY_ID[GRASS_TILE_ID]
            const tileColor = getDisplayTileColor(tile, DISTINCT_TILE_COLOR_MAP, useDistinctColors)
            const symbol = tile.category === 'trainer' ? (tile.direction ? directionIcons[tile.direction] : '▲') : tile.encounter ? '!' : null

            return (
              <button
                key={`${x}-${y}`}
                type="button"
                className="grid place-items-center p-0 transition-opacity hover:opacity-80"
                style={{
                  width: cellSize,
                  height: cellSize,
                  backgroundColor: tileColor,
                  cursor: 'crosshair',
                }}
                title={`${tile.name} (${x}, ${y})`}
                onMouseDown={(event) => onCellMouseDown(event, x, y)}
                onMouseEnter={() => onCellMouseEnter(x, y)}
                onContextMenu={(event) => event.preventDefault()}
              >
                {symbol ? <span className="pointer-events-none text-[10px] text-[#f2f2f2]">{symbol}</span> : null}
              </button>
            )
          }),
        )}
        {previewCells.map((cell, index) => {
          const previewTile = TILES_BY_ID[cell.tileId] ?? TILES_BY_ID[GRASS_TILE_ID]
          const previewColor = getDisplayTileColor(previewTile, DISTINCT_TILE_COLOR_MAP, useDistinctColors)

          return (
            <div
              key={`${cell.x}-${cell.y}-${index}`}
              className="pointer-events-none absolute border-2 border-dashed border-white/85"
              style={{
                width: cellSize,
                height: cellSize,
                left: cell.x * (cellSize + 1) + 1,
                top: cell.y * (cellSize + 1) + 1,
                backgroundColor: previewColor,
                opacity: 0.6,
                zIndex: 10,
              }}
            />
          )
        })}
      </div>
    </section>
  )
}

export default CanvasRegion
