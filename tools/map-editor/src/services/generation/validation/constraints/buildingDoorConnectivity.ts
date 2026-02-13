import { isInBounds, pointKey } from '../../shared/geometry'
import { TILE_ID_DOOR } from '../../shared/tileConstants'
import { getPrimaryPathTileId } from '../../shared/tileRoles'
import { getFootprintCells } from './shared'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const buildingDoorConnectivityValidator = (context: GenerationContext): ValidationIssue[] => {
  const pathTileId = getPrimaryPathTileId(context)
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const invalidDoors: GridPoint[] = []
  const neighbors: GridPoint[] = [
    { x: 1, y: 0 },
    { x: -1, y: 0 },
    { x: 0, y: 1 },
    { x: 0, y: -1 },
  ]

  for (let index = 0; index < context.buildingPlacements.length; index += 1) {
    const cells = getFootprintCells(context, index).filter((cell) => cell.inBounds)
    const buildingSet = new Set(cells.map((cell) => pointKey(cell.x, cell.y)))
    const doorCells = cells.filter((cell) => cell.tileId === TILE_ID_DOOR)

    for (const door of doorCells) {
      let connected = false
      for (const offset of neighbors) {
        const nx = door.x + offset.x
        const ny = door.y + offset.y
        if (!isInBounds(nx, ny, width, height) || buildingSet.has(pointKey(nx, ny))) {
          continue
        }

        if (context.grid[ny]?.[nx] === pathTileId) {
          connected = true
          break
        }
      }

      if (!connected) {
        invalidDoors.push({ x: door.x, y: door.y })
      }
    }
  }

  if (invalidDoors.length === 0) {
    return []
  }

  return [
    {
      id: 'buildingDoorConnectivity',
      severity: 'error',
      message: `Found ${invalidDoors.length} building doors without path adjacency.`,
      cells: invalidDoors,
    },
  ]
}
