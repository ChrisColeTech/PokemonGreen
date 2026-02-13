import type { DrawMode, TileGrid } from '../types/editor'

export const MIN_DIMENSION = 5
export const MAX_DIMENSION = 100

export const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value))

export const createGrid = (width: number, height: number, fillTileId: number): TileGrid =>
  Array.from({ length: height }, () => Array.from({ length: width }, () => fillTileId))

export const resizeGridPreserve = (grid: TileGrid, width: number, height: number, fillTileId: number): TileGrid => {
  const nextGrid = createGrid(width, height, fillTileId)

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      nextGrid[y][x] = grid[y]?.[x] ?? fillTileId
    }
  }

  return nextGrid
}

export const clearGrid = (width: number, height: number, fillTileId: number): TileGrid => createGrid(width, height, fillTileId)

const isInBounds = (grid: TileGrid, x: number, y: number) => y >= 0 && y < grid.length && x >= 0 && x < (grid[0]?.length ?? 0)

const withTile = (grid: TileGrid, x: number, y: number, tileId: number): TileGrid => {
  if (!isInBounds(grid, x, y)) {
    return grid
  }

  if (grid[y][x] === tileId) {
    return grid
  }

  const nextGrid = grid.map((row) => [...row])
  nextGrid[y][x] = tileId
  return nextGrid
}

export const paintCell = (grid: TileGrid, x: number, y: number, tileId: number): TileGrid => withTile(grid, x, y, tileId)

export const eraseCell = (grid: TileGrid, x: number, y: number, eraseTileId: number): TileGrid => withTile(grid, x, y, eraseTileId)

export const toggleCell = (grid: TileGrid, x: number, y: number, tileId: number, fallbackTileId: number): TileGrid => {
  if (!isInBounds(grid, x, y)) {
    return grid
  }

  const nextTileId = grid[y][x] === tileId ? fallbackTileId : tileId
  return withTile(grid, x, y, nextTileId)
}

export const applyPaintOperation = (
  grid: TileGrid,
  x: number,
  y: number,
  mode: Exclude<DrawMode, null>,
  selectedTileId: number,
  eraseTileId: number,
): TileGrid => {
  if (mode === 'erase') {
    return eraseCell(grid, x, y, eraseTileId)
  }

  return toggleCell(grid, x, y, selectedTileId, eraseTileId)
}
