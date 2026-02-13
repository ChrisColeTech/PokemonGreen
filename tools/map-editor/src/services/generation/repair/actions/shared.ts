import { BUILDINGS_BY_ID } from '../../../../data/buildings'
import { getPlacementCells, placeBuildingFootprint, rotateBuildingFootprint } from '../../../buildingService'
import { isInBounds, pointKey } from '../../shared/geometry'
import { collectPathCells } from '../../shared/pathTiles'
import { TILE_ID_FLOWER } from '../../shared/tileConstants'
import { getBaseTerrainTileId, getPrimaryPathTileId } from '../../shared/tileRoles'
import type { BuildingId, BuildingPlacementCell, BuildingRotation, GridPoint, TileGrid } from '../../../../types/editor'
import type { GeneratedBuildingPlacement, GenerationContext } from '../../../../types/generation'

export const carveManhattanPath = (
  grid: TileGrid,
  start: GridPoint,
  end: GridPoint,
  tileId: number,
  width: number,
  height: number,
): void => {
  let x = start.x
  let y = start.y
  if (isInBounds(x, y, width, height)) {
    grid[y][x] = tileId
  }

  while (x !== end.x) {
    x += end.x > x ? 1 : -1
    if (isInBounds(x, y, width, height)) {
      grid[y][x] = tileId
    }
  }

  while (y !== end.y) {
    y += end.y > y ? 1 : -1
    if (isInBounds(x, y, width, height)) {
      grid[y][x] = tileId
    }
  }
}

export const getPlacementFootprintCells = (
  context: GenerationContext,
  placement: GeneratedBuildingPlacement,
): Array<BuildingPlacementCell & { inBounds: boolean }> => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const footprint = rotateBuildingFootprint(BUILDINGS_BY_ID[placement.buildingId], placement.rotation)
  const cells: Array<BuildingPlacementCell & { inBounds: boolean }> = []

  for (let localY = 0; localY < footprint.height; localY += 1) {
    for (let localX = 0; localX < footprint.width; localX += 1) {
      const tileId = footprint.tiles[localY]?.[localX]
      if (tileId === null || tileId === undefined) {
        continue
      }

      const x = placement.anchor.x + localX
      const y = placement.anchor.y + localY
      cells.push({
        x,
        y,
        tileId,
        inBounds: isInBounds(x, y, width, height),
      })
    }
  }

  return cells
}

export const refreshPrimaryPathState = (context: GenerationContext): void => {
  context.state.primaryPathCells = collectPathCells(context)
}

export const clearFootprintFromGrid = (context: GenerationContext, placement: GeneratedBuildingPlacement): void => {
  const baseTileId = getBaseTerrainTileId(context)
  const cells = getPlacementCells(
    BUILDINGS_BY_ID[placement.buildingId],
    placement.rotation,
    placement.anchor.x,
    placement.anchor.y,
    context.config.dimensions.width,
    context.config.dimensions.height,
  )

  for (const cell of cells) {
    context.grid[cell.y][cell.x] = baseTileId
  }
}

export const buildOccupiedCells = (context: GenerationContext): Set<string> => {
  const occupied = new Set<string>()

  for (const placement of context.buildingPlacements) {
    const cells = getPlacementCells(
      BUILDINGS_BY_ID[placement.buildingId],
      placement.rotation,
      placement.anchor.x,
      placement.anchor.y,
      context.config.dimensions.width,
      context.config.dimensions.height,
    )

    for (const cell of cells) {
      occupied.add(pointKey(cell.x, cell.y))
    }
  }

  return occupied
}

export const canPlaceAt = (
  context: GenerationContext,
  buildingId: BuildingId,
  rotation: BuildingRotation,
  x: number,
  y: number,
  occupied: Set<string>,
): boolean => {
  const baseTileId = getBaseTerrainTileId(context)
  const placementCells = getPlacementCells(
    BUILDINGS_BY_ID[buildingId],
    rotation,
    x,
    y,
    context.config.dimensions.width,
    context.config.dimensions.height,
  )

  const footprint = rotateBuildingFootprint(BUILDINGS_BY_ID[buildingId], rotation)
  let expectedCellCount = 0
  for (const row of footprint.tiles) {
    for (const tileId of row) {
      if (tileId !== null) {
        expectedCellCount += 1
      }
    }
  }

  if (placementCells.length !== expectedCellCount) {
    return false
  }

  for (const cell of placementCells) {
    const key = pointKey(cell.x, cell.y)
    if (occupied.has(key)) {
      return false
    }

    const currentTile = context.grid[cell.y][cell.x]
    if (currentTile !== baseTileId && currentTile !== TILE_ID_FLOWER && currentTile !== getPrimaryPathTileId(context)) {
      return false
    }
  }

  return true
}

export const placeBuildingDeterministically = (
  context: GenerationContext,
  buildingId: BuildingId,
  occupied: Set<string>,
): GeneratedBuildingPlacement | null => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height

  for (const rotation of [0, 1, 2, 3] as const) {
    const footprint = rotateBuildingFootprint(BUILDINGS_BY_ID[buildingId], rotation)
    const maxX = width - footprint.width
    const maxY = height - footprint.height

    for (let y = 0; y <= maxY; y += 1) {
      for (let x = 0; x <= maxX; x += 1) {
        if (!canPlaceAt(context, buildingId, rotation, x, y, occupied)) {
          continue
        }

        context.grid = placeBuildingFootprint(
          context.grid,
          BUILDINGS_BY_ID[buildingId],
          rotation,
          x,
          y,
          width,
          height,
        )

        const cells = getPlacementCells(BUILDINGS_BY_ID[buildingId], rotation, x, y, width, height)
        for (const cell of cells) {
          occupied.add(pointKey(cell.x, cell.y))
        }

        return {
          buildingId,
          rotation,
          anchor: { x, y },
        }
      }
    }
  }

  return null
}
