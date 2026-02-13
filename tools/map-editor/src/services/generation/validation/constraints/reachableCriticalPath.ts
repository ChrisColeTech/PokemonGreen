import { isInBounds, pointKey } from '../../shared/geometry'
import { collectPathCells } from '../../shared/pathTiles'
import type { GridPoint } from '../../../../types/editor'
import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const reachableCriticalPathValidator = (context: GenerationContext): ValidationIssue[] => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const pathCells = collectPathCells(context)

  if (pathCells.length === 0) {
    return [
      {
        id: 'reachableCriticalPath',
        severity: 'error',
        message: 'Primary path tiles are missing.',
      },
    ]
  }

  const pathSet = new Set(pathCells.map((cell) => pointKey(cell.x, cell.y)))
  const visited = new Set<string>()
  const queue: GridPoint[] = [pathCells[0]]
  visited.add(pointKey(pathCells[0].x, pathCells[0].y))
  const neighbors: GridPoint[] = [
    { x: 1, y: 0 },
    { x: -1, y: 0 },
    { x: 0, y: 1 },
    { x: 0, y: -1 },
  ]

  while (queue.length > 0) {
    const current = queue.shift()
    if (!current) {
      break
    }

    for (const offset of neighbors) {
      const nx = current.x + offset.x
      const ny = current.y + offset.y
      const key = pointKey(nx, ny)
      if (!isInBounds(nx, ny, width, height) || !pathSet.has(key) || visited.has(key)) {
        continue
      }

      visited.add(key)
      queue.push({ x: nx, y: ny })
    }
  }

  if (visited.size !== pathCells.length) {
    const unreachable = pathCells.filter((cell) => !visited.has(pointKey(cell.x, cell.y)))
    return [
      {
        id: 'reachableCriticalPath',
        severity: 'error',
        message: `Primary path is disconnected (${unreachable.length} unreachable cells).`,
        cells: unreachable,
      },
    ]
  }

  const touchesLeft = pathCells.some((cell) => cell.x === 0)
  const touchesRight = pathCells.some((cell) => cell.x === width - 1)
  if (!touchesLeft || !touchesRight) {
    return [
      {
        id: 'reachableCriticalPath',
        severity: 'error',
        message: 'Primary path must reach both map edges.',
      },
    ]
  }

  return []
}
