import { useMemo } from 'react'
import { useCanvasInteraction } from '../../hooks/useCanvasInteraction'
import { createDistinctTileColorMap, getDisplayTileColor } from '../../services/tileColorService'
import { useEditorStore } from '../../store/editorStore'
import { useUiStore } from '../../store/uiStore'
import type { TileDirection } from '../../types/editor'

const UNKNOWN_TILE_COLOR = '#3f4752'

function CanvasRegion() {
  const directionIcons: Record<TileDirection, string> = {
    up: '▲',
    down: '▼',
    left: '◀',
    right: '▶',
    north: '▲',
    south: '▼',
    west: '◀',
    east: '▶',
    northwest: '↖',
    northeast: '↗',
    southwest: '↙',
    southeast: '↘',
  }

  const mapWidth = useEditorStore((state) => state.mapWidth)
  const mapHeight = useEditorStore((state) => state.mapHeight)
  const cellSize = useEditorStore((state) => state.cellSize)
  const grid = useEditorStore((state) => state.grid)
  const registryTiles = useEditorStore((state) => state.activeRegistry.tiles)
  const useDistinctColors = useUiStore((state) => state.useDistinctColors)
  const { onCellMouseDown, onCellMouseEnter, onGridMouseLeave, previewCells } = useCanvasInteraction()
  const tilesById = useMemo(
    () => registryTiles.reduce<Record<number, (typeof registryTiles)[number]>>((result, tile) => {
      result[tile.id] = tile
      return result
    }, {}),
    [registryTiles],
  )
  const distinctTileColorMap = useMemo(() => createDistinctTileColorMap(registryTiles), [registryTiles])
  const fallbackTile = registryTiles[0]

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
            const tile = tilesById[tileId] ?? fallbackTile
            const tileColor = tile
              ? getDisplayTileColor(tile, distinctTileColorMap, useDistinctColors)
              : UNKNOWN_TILE_COLOR
            const symbol = tile
              ? (tile.category === 'trainer' ? (tile.direction ? directionIcons[tile.direction] : '▲') : tile.encounter ? '!' : null)
              : null

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
                title={`${tile?.name ?? 'Unknown Tile'} (${x}, ${y})`}
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
          const previewTile = tilesById[cell.tileId] ?? fallbackTile
          const previewColor = previewTile
            ? getDisplayTileColor(previewTile, distinctTileColorMap, useDistinctColors)
            : UNKNOWN_TILE_COLOR

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
