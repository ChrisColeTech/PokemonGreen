import { GRASS_TILE_ID } from '../../../data/tiles'
import { pointKey } from '../shared/geometry'
import { clamp } from '../shared/math'
import {
  TILE_ID_FLOWER,
} from '../shared/tileConstants'
import { getEncounterTileOptions, getPrimaryPathTileId } from '../shared/tileRoles'
import { buildOccupiedBuildingCells, getTownDistrict, hasDistrictCell } from './passHelpers'
import type { GridPoint } from '../../../types/editor'
import type { GenerationPass } from '../../../types/generation'

export const placeEncountersPass: GenerationPass = {
  id: 'placeEncounters',
  run: (context) => {
    const rng = context.rng.fork('placeEncounters')
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const pathTileId = getPrimaryPathTileId(context)
    const town = getTownDistrict(context)
    const buildingCells = buildOccupiedBuildingCells(context)
    const encounterTileOptions = getEncounterTileOptions(context)

    const newAnchors: GridPoint[] = []
    const encounterZone = context.state.templateHints?.encounterZone ?? 'balanced'
    const zoneFilteredPathCells = context.state.primaryPathCells.filter((cell) => {
      if (encounterZone === 'east') {
        return cell.x >= Math.floor(width * 0.45)
      }

      if (encounterZone === 'west') {
        return cell.x <= Math.ceil(width * 0.55)
      }

      return true
    })
    const pathCells = (zoneFilteredPathCells.length > 0 ? zoneFilteredPathCells : context.state.primaryPathCells)
      .filter((cell) => (town ? !hasDistrictCell(town, cell.x, cell.y) : true))
      .sort((left, right) => left.x - right.x)

    for (let index = 0; index < pathCells.length; index += 1) {
      if (index % 3 !== 0 || !rng.chance(0.4)) {
        continue
      }

      const anchor = pathCells[index]
      const patchWidth = rng.int(2, 4)
      const patchHeight = rng.int(2, 3)
      const side = rng.pick([-1, 1])
      const startX = clamp(anchor.x + rng.int(-1, 1), 1, width - patchWidth - 1)
      const startY = clamp(anchor.y + side * rng.int(2, 4), 1, height - patchHeight - 1)
      const encounterTileId =
        encounterTileOptions.length > 1 && rng.chance(0.18)
          ? rng.pick(encounterTileOptions.slice(1))
          : encounterTileOptions[0]

      let paintedAny = false
      for (let y = startY; y < startY + patchHeight; y += 1) {
        for (let x = startX; x < startX + patchWidth; x += 1) {
          if (context.grid[y][x] === pathTileId || buildingCells.has(pointKey(x, y))) {
            continue
          }

          if (context.grid[y][x] === GRASS_TILE_ID || context.grid[y][x] === TILE_ID_FLOWER) {
            context.grid[y][x] = encounterTileId
            paintedAny = true
          }
        }
      }

      if (paintedAny) {
        newAnchors.push(anchor)
      }
    }

    context.state.encounterAnchorCells = newAnchors
    return context
  },
}
