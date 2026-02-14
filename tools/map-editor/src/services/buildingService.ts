import type { BuildingPlacementCell, BuildingRotation, BuildingStampMatrix, TileGrid } from '../types/editor'
import type { EditorRegistryBuildingDefinition } from '../types/registry'

interface BuildingFootprint {
  width: number
  height: number
  tiles: BuildingStampMatrix
}

export const rotateBuildingFootprint = (
  building: EditorRegistryBuildingDefinition,
  rotation: BuildingRotation,
): BuildingFootprint => {
  let rotatedTiles = building.tiles.map((row) => [...row])

  for (let step = 0; step < rotation; step += 1) {
    const sourceHeight = rotatedTiles.length
    const sourceWidth = rotatedTiles[0]?.length ?? 0
    const nextTiles: BuildingStampMatrix = Array.from({ length: sourceWidth }, () =>
      Array.from({ length: sourceHeight }, () => null),
    )

    for (let y = 0; y < sourceHeight; y += 1) {
      for (let x = 0; x < sourceWidth; x += 1) {
        nextTiles[x][sourceHeight - 1 - y] = rotatedTiles[y][x]
      }
    }

    rotatedTiles = nextTiles
  }

  return {
    width: rotatedTiles[0]?.length ?? 0,
    height: rotatedTiles.length,
    tiles: rotatedTiles,
  }
}

export const getPlacementCells = (
  building: EditorRegistryBuildingDefinition,
  rotation: BuildingRotation,
  startX: number,
  startY: number,
  mapWidth: number,
  mapHeight: number,
): BuildingPlacementCell[] => {
  const footprint = rotateBuildingFootprint(building, rotation)
  const cells: BuildingPlacementCell[] = []

  for (let by = 0; by < footprint.height; by += 1) {
    for (let bx = 0; bx < footprint.width; bx += 1) {
      const x = startX + bx
      const y = startY + by

      if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) {
        continue
      }

      const tileId = footprint.tiles[by]?.[bx]
      if (tileId === null || tileId === undefined) {
        continue
      }

      cells.push({ x, y, tileId })
    }
  }

  return cells
}

export const placeBuildingFootprint = (
  grid: TileGrid,
  building: EditorRegistryBuildingDefinition,
  rotation: BuildingRotation,
  startX: number,
  startY: number,
  mapWidth: number,
  mapHeight: number,
): TileGrid => {
  const placementCells = getPlacementCells(building, rotation, startX, startY, mapWidth, mapHeight)

  if (placementCells.length === 0) {
    return grid
  }

  const nextGrid = grid.map((row) => [...row])
  for (const cell of placementCells) {
    nextGrid[cell.y][cell.x] = cell.tileId
  }

  return nextGrid
}
