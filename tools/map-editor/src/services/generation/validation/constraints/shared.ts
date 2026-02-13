import { BUILDINGS_BY_ID } from '../../../../data/buildings'
import { rotateBuildingFootprint } from '../../../buildingService'
import { isInBounds } from '../../shared/geometry'
import { collectPathCells } from '../../shared/pathTiles'
import type { BuildingPlacementCell, GridPoint } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const getFootprintCells = (
  context: GenerationContext,
  placementIndex: number,
): Array<BuildingPlacementCell & { inBounds: boolean }> => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const placement = context.buildingPlacements[placementIndex]
  const building = BUILDINGS_BY_ID[placement.buildingId]
  const footprint = rotateBuildingFootprint(building, placement.rotation)
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

export const findSpawnCell = (context: GenerationContext): GridPoint | null => {
  const sortedPrimary = [...context.state.primaryPathCells].sort((left, right) => {
    if (left.x !== right.x) {
      return left.x - right.x
    }
    return left.y - right.y
  })

  if (sortedPrimary.length > 0) {
    return sortedPrimary[0]
  }

  const fromGrid = collectPathCells(context)
  fromGrid.sort((left, right) => {
    if (left.x !== right.x) {
      return left.x - right.x
    }
    return left.y - right.y
  })

  return fromGrid[0] ?? null
}
