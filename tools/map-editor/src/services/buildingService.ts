import { BUILDING_PREFAB_STAMPS_BY_ID } from '../data/buildings'
import type { BuildingDefinition, BuildingPlacementCell, BuildingRotation, BuildingStampMatrix, TileGrid } from '../types/editor'

interface BuildingFootprint {
  width: number
  height: number
  tiles: BuildingStampMatrix
}

export const rotateBuildingFootprint = (
  building: BuildingDefinition,
  rotation: BuildingRotation,
): BuildingFootprint => {
  const prefabStamp = BUILDING_PREFAB_STAMPS_BY_ID[building.id]?.[rotation]
  if (!prefabStamp) {
    return {
      width: building.width,
      height: building.height,
      tiles: building.tiles.map((row) => [...row]),
    }
  }

  return {
    width: prefabStamp.width,
    height: prefabStamp.height,
    tiles: prefabStamp.tiles.map((row) => [...row]),
  }
}

export const getPlacementCells = (
  building: BuildingDefinition,
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
  building: BuildingDefinition,
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
