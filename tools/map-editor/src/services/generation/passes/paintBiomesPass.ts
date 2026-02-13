import { isInBounds, pointKey } from '../shared/geometry'
import {
  TILE_ID_FLOWER,
  TILE_ID_TREE,
} from '../shared/tileConstants'
import { getBaseTerrainTileId } from '../shared/tileRoles'
import type { GenerationPass } from '../../../types/generation'

export const paintBiomesPass: GenerationPass = {
  id: 'paintBiomes',
  run: (context) => {
    const rng = context.rng.fork('paintBiomes')
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const baseTileId = getBaseTerrainTileId(context)
    const pathSet = new Set(context.state.primaryPathCells.map((cell) => pointKey(cell.x, cell.y)))

    for (let y = 0; y < height; y += 1) {
      for (let x = 0; x < width; x += 1) {
        if (!pathSet.has(pointKey(x, y)) && context.grid[y][x] === baseTileId && rng.chance(0.045)) {
          context.grid[y][x] = TILE_ID_FLOWER
        }
      }
    }

    const treeClusters = Math.max(2, Math.floor((width * height) / 180))
    for (let cluster = 0; cluster < treeClusters; cluster += 1) {
      const centerX = rng.int(1, width - 2)
      const centerY = rng.int(1, height - 2)
      const radiusX = rng.int(2, 4)
      const radiusY = rng.int(2, 3)

      for (let y = centerY - radiusY; y <= centerY + radiusY; y += 1) {
        for (let x = centerX - radiusX; x <= centerX + radiusX; x += 1) {
          if (!isInBounds(x, y, width, height) || pathSet.has(pointKey(x, y))) {
            continue
          }

          const normalizedX = Math.abs((x - centerX) / radiusX)
          const normalizedY = Math.abs((y - centerY) / radiusY)
          if (normalizedX + normalizedY <= 1.25 && rng.chance(0.65)) {
            context.grid[y][x] = TILE_ID_TREE
          }
        }
      }
    }

    return context
  },
}
