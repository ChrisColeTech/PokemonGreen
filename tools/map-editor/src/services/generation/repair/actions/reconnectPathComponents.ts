import { isInBounds, pointKey } from '../../shared/geometry'
import { collectPathCells } from '../../shared/pathTiles'
import { getPrimaryPathTileId } from '../../shared/tileRoles'
import { carveManhattanPath } from './shared'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext } from '../../../../types/generation'

export const reconnectPathComponents = (context: GenerationContext): boolean => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const pathTileId = getPrimaryPathTileId(context)
  let pathCells = collectPathCells(context)

  if (pathCells.length === 0) {
    const y = Math.floor(height / 2)
    for (let x = 0; x < width; x += 1) {
      context.grid[y][x] = pathTileId
    }
    pathCells = collectPathCells(context)
  }

  const pathSet = new Set(pathCells.map((cell) => pointKey(cell.x, cell.y)))
  const visited = new Set<string>()
  const components: GridPoint[][] = []
  const neighbors: GridPoint[] = [
    { x: 1, y: 0 },
    { x: -1, y: 0 },
    { x: 0, y: 1 },
    { x: 0, y: -1 },
  ]

  for (const cell of pathCells) {
    const key = pointKey(cell.x, cell.y)
    if (visited.has(key)) {
      continue
    }

    const component: GridPoint[] = []
    const queue: GridPoint[] = [cell]
    visited.add(key)

    while (queue.length > 0) {
      const current = queue.shift()
      if (!current) {
        break
      }

      component.push(current)
      for (const offset of neighbors) {
        const nx = current.x + offset.x
        const ny = current.y + offset.y
        const nextKey = pointKey(nx, ny)
        if (!isInBounds(nx, ny, width, height) || !pathSet.has(nextKey) || visited.has(nextKey)) {
          continue
        }

        visited.add(nextKey)
        queue.push({ x: nx, y: ny })
      }
    }

    components.push(component)
  }

  const getComponentRepresentative = (component: GridPoint[]): GridPoint => {
    const first = component[0]
    if (!first) {
      return { x: 0, y: 0 }
    }

    return component.reduce(
      (best, cell) => (cell.x < best.x || (cell.x === best.x && cell.y < best.y) ? cell : best),
      first,
    )
  }

  components.sort((left, right) => {
    const leftRep = getComponentRepresentative(left)
    const rightRep = getComponentRepresentative(right)
    if (leftRep.x !== rightRep.x) {
      return leftRep.x - rightRep.x
    }
    return leftRep.y - rightRep.y
  })

  let changed = false
  const mainRep = components[0]?.[0]
  if (!mainRep) {
    return changed
  }

  for (let index = 1; index < components.length; index += 1) {
    const rep = components[index]?.[0]
    if (!rep) {
      continue
    }

    carveManhattanPath(context.grid, mainRep, rep, pathTileId, width, height)
    changed = true
  }

  const refreshed = collectPathCells(context)
  if (refreshed.length === 0) {
    return changed
  }
  const leftMost = refreshed.reduce((best, cell) => (cell.x < best.x ? cell : best), refreshed[0])
  const rightMost = refreshed.reduce((best, cell) => (cell.x > best.x ? cell : best), refreshed[0])
  if (leftMost.x > 0) {
    carveManhattanPath(context.grid, leftMost, { x: 0, y: leftMost.y }, pathTileId, width, height)
    changed = true
  }
  if (rightMost.x < width - 1) {
    carveManhattanPath(context.grid, rightMost, { x: width - 1, y: rightMost.y }, pathTileId, width, height)
    changed = true
  }

  return changed
}
