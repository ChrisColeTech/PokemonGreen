import { BUILDINGS_BY_ID } from '../../../data/buildings'
import { getPlacementCells } from '../../buildingService'
import { pointKey } from '../shared/geometry'
import type { GridPoint, TileGrid } from '../../../types/editor'
import type { GenerationContext, ReservedDistrict } from '../../../types/generation'

export const findPrimaryPathY = (cells: GridPoint[], fallbackY: number, x: number): number => {
  for (const cell of cells) {
    if (cell.x === x) {
      return cell.y
    }
  }

  return fallbackY
}

export const hasDistrictCell = (district: ReservedDistrict, x: number, y: number): boolean =>
  x >= district.x && y >= district.y && x < district.x + district.width && y < district.y + district.height

export const getTownDistrict = (context: GenerationContext): ReservedDistrict | null =>
  context.state.reservedDistricts.find((district) => district.id === 'town') ?? null

export const buildOccupiedBuildingCells = (context: GenerationContext): Set<string> => {
  const occupied = new Set<string>()
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height

  for (const placement of context.buildingPlacements) {
    const building = BUILDINGS_BY_ID[placement.buildingId]
    const cells = getPlacementCells(
      building,
      placement.rotation,
      placement.anchor.x,
      placement.anchor.y,
      width,
      height,
    )

    for (const cell of cells) {
      occupied.add(pointKey(cell.x, cell.y))
    }
  }

  return occupied
}

export const fillRect = (grid: TileGrid, x: number, y: number, width: number, height: number, tileId: number): void => {
  for (let iy = y; iy < y + height; iy += 1) {
    for (let ix = x; ix < x + width; ix += 1) {
      if (grid[iy]?.[ix] !== undefined) {
        grid[iy][ix] = tileId
      }
    }
  }
}
