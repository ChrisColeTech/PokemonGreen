import { BUILDINGS_BY_ID } from '../../../data/buildings'
import { getPlacementCells, placeBuildingFootprint, rotateBuildingFootprint } from '../../buildingService'
import { isInBounds, pointKey } from '../shared/geometry'
import {
  TILE_ID_DOOR,
  TILE_ID_FLOWER,
} from '../shared/tileConstants'
import { getBaseTerrainTileId, getPrimaryPathTileId } from '../shared/tileRoles'
import { buildOccupiedBuildingCells, getTownDistrict } from './passHelpers'
import type { BuildingId, BuildingRotation, GridPoint } from '../../../types/editor'
import type { GeneratedBuildingPlacement, GenerationPass } from '../../../types/generation'

export const placeBuildingsPass: GenerationPass = {
  id: 'placeBuildings',
  run: (context) => {
    const rng = context.rng.fork('placeBuildings')
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const baseTileId = getBaseTerrainTileId(context)
    const pathTileId = getPrimaryPathTileId(context)
    const town = getTownDistrict(context)
    if (!town) {
      context.diagnostics.warnings.push('Town district missing before building placement.')
      return context
    }

    const occupied = buildOccupiedBuildingCells(context)
    const requiredOrder: BuildingId[] = ['pokecenter', 'pokemart', 'gym', 'lab', 'house-large', 'house-small', 'gate']
    const candidateOrder: BuildingId[] = [
      ...requiredOrder,
      'cave-entrance',
      'pond',
      'fence-h',
      'fence-v',
    ]
    const placementQueue: BuildingId[] = []

    for (const buildingId of candidateOrder) {
      const target = context.archetype.buildingTargets[buildingId]
      if (!target) {
        continue
      }

      const extra = target.max > target.min ? rng.int(0, target.max - target.min) : 0
      const count = target.min + extra
      for (let index = 0; index < count; index += 1) {
        placementQueue.push(buildingId)
      }
    }

    const shuffledTail = rng.shuffle(placementQueue.filter((id) => !requiredOrder.includes(id)))
    const ordered = [
      ...placementQueue.filter((id) => requiredOrder.includes(id)),
      ...shuffledTail,
    ]

    const countNonNullTiles = (buildingId: BuildingId, rotation: BuildingRotation): number => {
      const footprint = rotateBuildingFootprint(BUILDINGS_BY_ID[buildingId], rotation)
      let count = 0
      for (const row of footprint.tiles) {
        for (const tileId of row) {
          if (tileId !== null) {
            count += 1
          }
        }
      }
      return count
    }

    const tryPlaceBuilding = (buildingId: BuildingId): GeneratedBuildingPlacement | null => {
      const building = BUILDINGS_BY_ID[buildingId]
      for (let attempt = 0; attempt < 40; attempt += 1) {
        const rotation = rng.int(0, 3) as BuildingRotation
        const footprint = rotateBuildingFootprint(building, rotation)
        const minX = town.x + 1
        const maxX = town.x + town.width - footprint.width - 1
        const minY = town.y + 1
        const maxY = town.y + town.height - footprint.height - 1

        if (maxX < minX || maxY < minY) {
          return null
        }

        const startX = rng.int(minX, maxX)
        const startY = rng.int(minY, maxY)
        const placementCells = getPlacementCells(building, rotation, startX, startY, width, height)
        const expectedCellCount = countNonNullTiles(buildingId, rotation)
        if (placementCells.length !== expectedCellCount) {
          continue
        }

        let blocked = false
        for (const cell of placementCells) {
          const key = pointKey(cell.x, cell.y)
          if (occupied.has(key)) {
            blocked = true
            break
          }

          const currentTileId = context.grid[cell.y][cell.x]
          if (currentTileId !== baseTileId && currentTileId !== TILE_ID_FLOWER) {
            blocked = true
            break
          }
        }
        if (blocked) {
          continue
        }

        context.grid = placeBuildingFootprint(context.grid, building, rotation, startX, startY, width, height)
        for (const cell of placementCells) {
          occupied.add(pointKey(cell.x, cell.y))
        }

        const buildingCellSet = new Set(placementCells.map((cell) => pointKey(cell.x, cell.y)))
        for (const doorCell of placementCells.filter((cell) => cell.tileId === TILE_ID_DOOR)) {
          const neighborOffsets: GridPoint[] = [
            { x: 0, y: 1 },
            { x: 0, y: -1 },
            { x: 1, y: 0 },
            { x: -1, y: 0 },
          ]

          for (const offset of neighborOffsets) {
            const nx = doorCell.x + offset.x
            const ny = doorCell.y + offset.y
            if (!isInBounds(nx, ny, width, height) || buildingCellSet.has(pointKey(nx, ny))) {
              continue
            }

            if (!occupied.has(pointKey(nx, ny))) {
              context.grid[ny][nx] = pathTileId
            }
            break
          }
        }

        return {
          buildingId,
          rotation,
          anchor: { x: startX, y: startY },
        }
      }

      return null
    }

    for (const buildingId of ordered) {
      const placement = tryPlaceBuilding(buildingId)
      if (placement) {
        context.buildingPlacements.push(placement)
      }
    }

    for (const [buildingId, target] of Object.entries(context.archetype.buildingTargets) as Array<[
      BuildingId,
      { min: number; max: number },
    ]>) {
      const placedCount = context.buildingPlacements.filter((placement) => placement.buildingId === buildingId).length
      if (placedCount < target.min) {
        context.diagnostics.warnings.push(
          `Could not place required building ${buildingId} (${placedCount}/${target.min}).`,
        )
      }
    }

    return context
  },
}
