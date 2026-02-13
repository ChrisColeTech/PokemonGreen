import { GRASS_TILE_ID } from '../../../data/tiles'
import { isInBounds, pointKey } from '../shared/geometry'
import {
  TILE_ID_FLOWER,
  TILE_ID_HIDDEN_ITEM,
  TILE_ID_ITEM,
  TILE_ID_NPC,
  TILE_ID_SIGN,
  TILE_ID_TALL_GRASS,
  TRAINER_TILE_IDS,
} from '../shared/tileConstants'
import { getPrimaryPathTileId } from '../shared/tileRoles'
import { buildOccupiedBuildingCells } from './passHelpers'
import type { GridPoint } from '../../../types/editor'
import type { GenerationPass } from '../../../types/generation'

export const placeInteractivesAndEntitiesPass: GenerationPass = {
  id: 'placeInteractivesAndEntities',
  run: (context) => {
    const rng = context.rng.fork('placeInteractivesAndEntities')
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const pathTileId = getPrimaryPathTileId(context)
    const occupied = buildOccupiedBuildingCells(context)
    const pathSet = new Set(context.state.primaryPathCells.map((cell) => pointKey(cell.x, cell.y)))

    const candidates: GridPoint[] = []
    for (const pathCell of context.state.primaryPathCells) {
      const offsets: GridPoint[] = [
        { x: 0, y: -1 },
        { x: 0, y: 1 },
        { x: -1, y: 0 },
        { x: 1, y: 0 },
      ]
      for (const offset of offsets) {
        const x = pathCell.x + offset.x
        const y = pathCell.y + offset.y
        const key = pointKey(x, y)
        if (!isInBounds(x, y, width, height) || pathSet.has(key) || occupied.has(key)) {
          continue
        }

        const tileId = context.grid[y][x]
        if (tileId === GRASS_TILE_ID || tileId === TILE_ID_FLOWER || tileId === TILE_ID_TALL_GRASS) {
          candidates.push({ x, y })
        }
      }
    }

    const candidateMap = new Map<string, GridPoint>()
    for (const cell of candidates) {
      candidateMap.set(pointKey(cell.x, cell.y), cell)
    }
    const available = rng.shuffle(Array.from(candidateMap.values()))

    const placeFromAvailable = (count: number, tileResolver: (index: number) => number): void => {
      for (let index = 0; index < count && available.length > 0; index += 1) {
        const cell = available.shift()
        if (!cell) {
          return
        }

        if (context.grid[cell.y][cell.x] === pathTileId) {
          continue
        }

        context.grid[cell.y][cell.x] = tileResolver(index)
      }
    }

    placeFromAvailable(rng.int(2, 4), () => TILE_ID_SIGN)
    placeFromAvailable(rng.int(2, 4), () => TILE_ID_NPC)
    placeFromAvailable(rng.int(3, 6), () => rng.pick(TRAINER_TILE_IDS))
    placeFromAvailable(rng.int(2, 4), (index) => (index % 2 === 0 ? TILE_ID_ITEM : TILE_ID_HIDDEN_ITEM))

    return context
  },
}
