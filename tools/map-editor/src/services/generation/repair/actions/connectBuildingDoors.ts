import { isInBounds, pointKey } from '../../shared/geometry'
import { collectPathCells } from '../../shared/pathTiles'
import { TILE_ID_DOOR } from '../../shared/tileConstants'
import { getPrimaryPathTileId } from '../../shared/tileRoles'
import { carveManhattanPath, getPlacementFootprintCells } from './shared'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const connectBuildingDoors = (context: GenerationContext): boolean => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const pathTileId = getPrimaryPathTileId(context)
  let changed = false

  const neighbors: GridPoint[] = [
    { x: 1, y: 0 },
    { x: -1, y: 0 },
    { x: 0, y: 1 },
    { x: 0, y: -1 },
  ]

  for (const placement of context.buildingPlacements) {
    const footprintCells = getPlacementFootprintCells(context, placement).filter((cell) => cell.inBounds)
    const buildingCellSet = new Set(footprintCells.map((cell) => pointKey(cell.x, cell.y)))
    const doorCells = footprintCells.filter((cell) => cell.tileId === TILE_ID_DOOR)

    for (const doorCell of doorCells) {
      const hasAdjacentPath = neighbors.some((offset) => {
        const nx = doorCell.x + offset.x
        const ny = doorCell.y + offset.y
        return isInBounds(nx, ny, width, height) && context.grid[ny][nx] === pathTileId
      })

      if (hasAdjacentPath) {
        continue
      }

      const pathCells = collectPathCells(context)
      let targetPathCell: GridPoint | null = null
      let bestDistance = Number.POSITIVE_INFINITY

      for (const pathCell of pathCells) {
        const distance = Math.abs(pathCell.x - doorCell.x) + Math.abs(pathCell.y - doorCell.y)
        if (distance === 0) {
          continue
        }

        if (
          distance < bestDistance ||
          (distance === bestDistance &&
            (targetPathCell === null || pathCell.x < targetPathCell.x || (pathCell.x === targetPathCell.x && pathCell.y < targetPathCell.y)))
        ) {
          bestDistance = distance
          targetPathCell = pathCell
        }
      }

      const doorExit = neighbors
        .map((offset) => ({ x: doorCell.x + offset.x, y: doorCell.y + offset.y }))
        .find((cell) => isInBounds(cell.x, cell.y, width, height) && !buildingCellSet.has(pointKey(cell.x, cell.y)))

      if (!doorExit) {
        continue
      }

      context.grid[doorExit.y][doorExit.x] = pathTileId
      changed = true

      if (targetPathCell) {
        carveManhattanPath(context.grid, doorExit, targetPathCell, pathTileId, width, height)
      }
    }
  }

  return changed
}
