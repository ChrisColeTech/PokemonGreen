import { BUILDINGS_BY_ID } from '../../../data/buildings'
import { getPlacementCells } from '../../buildingService'
import { isInBounds, pointKey } from '../shared/geometry'
import { clamp } from '../shared/math'
import {
  TILE_ID_FLOWER,
  TILE_ID_TREE,
  TOWN_PATH_CLUTTER_TILE_IDS,
  TRAINER_TILE_IDS,
} from '../shared/tileConstants'
import { getBaseTerrainTileId, getEncounterTileOptions } from '../shared/tileRoles'
import type { GridPoint } from '../../../types/editor'
import type { GenerationContext } from '../../../types/generation'

const collectCellsByTileSet = (
  context: GenerationContext,
  tileIds: Set<number>,
): GridPoint[] => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const cells: GridPoint[] = []

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (tileIds.has(context.grid[y][x])) {
        cells.push({ x, y })
      }
    }
  }

  return cells
}

const buildOccupiedBuildingCellSet = (context: GenerationContext): Set<string> => {
  const occupied = new Set<string>()
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height

  for (const placement of context.buildingPlacements) {
    const cells = getPlacementCells(
      BUILDINGS_BY_ID[placement.buildingId],
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

const collectTownPathBufferCells = (context: GenerationContext): Set<string> => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const town = context.state.reservedDistricts.find((district) => district.id === 'town')
  if (!town) {
    return new Set<string>()
  }

  const townPathCells = context.state.primaryPathCells.filter((cell) => {
    return (
      cell.x >= town.x &&
      cell.y >= town.y &&
      cell.x < town.x + town.width &&
      cell.y < town.y + town.height
    )
  })

  const bufferedCells = new Set<string>()
  for (const pathCell of townPathCells) {
    for (let dy = -1; dy <= 1; dy += 1) {
      for (let dx = -1; dx <= 1; dx += 1) {
        if (Math.abs(dx) + Math.abs(dy) > 1) {
          continue
        }

        const x = pathCell.x + dx
        const y = pathCell.y + dy
        if (isInBounds(x, y, width, height)) {
          bufferedCells.add(pointKey(x, y))
        }
      }
    }
  }

  return bufferedCells
}

const minPathDistance = (cell: GridPoint, pathCells: GridPoint[]): number => {
  let distance = Number.POSITIVE_INFINITY
  for (const pathCell of pathCells) {
    const nextDistance = Math.abs(pathCell.x - cell.x) + Math.abs(pathCell.y - cell.y)
    if (nextDistance < distance) {
      distance = nextDistance
    }
  }

  return Number.isFinite(distance) ? distance : Number.MAX_SAFE_INTEGER
}

const clearTownPathClutter = (context: GenerationContext, occupiedBuildingCells: Set<string>): number => {
  const baseTileId = getBaseTerrainTileId(context)
  const townPathBuffer = collectTownPathBufferCells(context)
  let cleared = 0

  for (const key of townPathBuffer) {
    if (occupiedBuildingCells.has(key)) {
      continue
    }

    const [xPart, yPart] = key.split(',')
    const x = Number.parseInt(xPart, 10)
    const y = Number.parseInt(yPart, 10)
    if (!Number.isInteger(x) || !Number.isInteger(y)) {
      continue
    }

    const tileId = context.grid[y]?.[x]
    if (tileId !== undefined && TOWN_PATH_CLUTTER_TILE_IDS.has(tileId)) {
      context.grid[y][x] = baseTileId
      cleared += 1
    }
  }

  return cleared
}

const clampTrainerDensity = (context: GenerationContext, occupiedBuildingCells: Set<string>): { removed: number; added: number } => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const baseTileId = getBaseTerrainTileId(context)
  const trainerSet = new Set<number>(TRAINER_TILE_IDS)
  const trainers = collectCellsByTileSet(context, trainerSet)
  const pathCells = context.state.primaryPathCells
  const pathCellCount = Math.max(1, pathCells.length)
  const minTrainerCount = Math.max(1, Math.floor(pathCellCount * 0.04))
  const maxTrainerCount = Math.max(minTrainerCount, Math.ceil(pathCellCount * 0.16))
  const rng = context.rng.fork('balance:trainers')
  const townPathBuffer = collectTownPathBufferCells(context)

  let removed = 0
  let added = 0

  if (trainers.length > maxTrainerCount) {
    const toRemove = trainers.length - maxTrainerCount
    const ranked = rng.shuffle(trainers).sort((left, right) => {
      const leftTownScore = townPathBuffer.has(pointKey(left.x, left.y)) ? 1 : 0
      const rightTownScore = townPathBuffer.has(pointKey(right.x, right.y)) ? 1 : 0
      if (leftTownScore !== rightTownScore) {
        return rightTownScore - leftTownScore
      }

      const leftDistance = minPathDistance(left, pathCells)
      const rightDistance = minPathDistance(right, pathCells)
      if (leftDistance !== rightDistance) {
        return leftDistance - rightDistance
      }

      if (left.y !== right.y) {
        return left.y - right.y
      }

      return left.x - right.x
    })

    for (let index = 0; index < toRemove; index += 1) {
      const cell = ranked[index]
      if (!cell) {
        break
      }

      context.grid[cell.y][cell.x] = baseTileId
      removed += 1
    }
  }

  const updatedTrainerCount = trainers.length - removed
  if (updatedTrainerCount < minTrainerCount && pathCells.length > 0) {
    const needed = minTrainerCount - updatedTrainerCount
    const pathSet = new Set(pathCells.map((cell) => pointKey(cell.x, cell.y)))
    const candidates: GridPoint[] = []

    for (let y = 0; y < height; y += 1) {
      for (let x = 0; x < width; x += 1) {
        const key = pointKey(x, y)
        if (occupiedBuildingCells.has(key) || townPathBuffer.has(key) || pathSet.has(key)) {
          continue
        }

        const tileId = context.grid[y][x]
        if (tileId !== baseTileId && tileId !== TILE_ID_FLOWER && tileId !== TILE_ID_TREE) {
          continue
        }

        const isAdjacentToPath = pathCells.some((pathCell) => Math.abs(pathCell.x - x) + Math.abs(pathCell.y - y) === 1)
        if (!isAdjacentToPath) {
          continue
        }

        candidates.push({ x, y })
      }
    }

    const shuffledCandidates = rng.shuffle(candidates)
    for (let index = 0; index < needed && index < shuffledCandidates.length; index += 1) {
      const cell = shuffledCandidates[index]
      context.grid[cell.y][cell.x] = rng.pick(TRAINER_TILE_IDS)
      added += 1
    }
  }

  return { removed, added }
}

const clampEncounterDensity = (context: GenerationContext, occupiedBuildingCells: Set<string>): { removed: number; added: number } => {
  const width = context.config.dimensions.width
  const height = context.config.dimensions.height
  const area = width * height
  const baseTileId = getBaseTerrainTileId(context)
  const encounterTileOptions = getEncounterTileOptions(context)
  const encounterSet = new Set<number>(encounterTileOptions)
  const pathCells = context.state.primaryPathCells
  const townPathBuffer = collectTownPathBufferCells(context)
  const rng = context.rng.fork('balance:encounters')

  const minEncounterCount = clamp(Math.floor(area * 0.035), 8, Math.floor(area * 0.1))
  const maxEncounterCount = Math.max(minEncounterCount, Math.floor(area * 0.14))

  const encounters = collectCellsByTileSet(context, encounterSet)
  let removed = 0
  let added = 0

  if (encounters.length > maxEncounterCount) {
    const toRemove = encounters.length - maxEncounterCount
    const ranked = rng.shuffle(encounters).sort((left, right) => {
      const leftTownScore = townPathBuffer.has(pointKey(left.x, left.y)) ? 1 : 0
      const rightTownScore = townPathBuffer.has(pointKey(right.x, right.y)) ? 1 : 0
      if (leftTownScore !== rightTownScore) {
        return rightTownScore - leftTownScore
      }

      const leftDistance = minPathDistance(left, pathCells)
      const rightDistance = minPathDistance(right, pathCells)
      if (leftDistance !== rightDistance) {
        return leftDistance - rightDistance
      }

      if (left.y !== right.y) {
        return left.y - right.y
      }

      return left.x - right.x
    })

    for (let index = 0; index < toRemove; index += 1) {
      const cell = ranked[index]
      if (!cell) {
        break
      }

      context.grid[cell.y][cell.x] = baseTileId
      removed += 1
    }
  }

  const updatedEncounterCount = encounters.length - removed
  if (updatedEncounterCount < minEncounterCount && pathCells.length > 0) {
    const pathSet = new Set(pathCells.map((cell) => pointKey(cell.x, cell.y)))
    const needed = minEncounterCount - updatedEncounterCount
    const candidates: GridPoint[] = []

    for (let y = 0; y < height; y += 1) {
      for (let x = 0; x < width; x += 1) {
        const key = pointKey(x, y)
        if (pathSet.has(key) || townPathBuffer.has(key) || occupiedBuildingCells.has(key)) {
          continue
        }

        const tileId = context.grid[y][x]
        if (tileId !== baseTileId && tileId !== TILE_ID_FLOWER) {
          continue
        }

        const distanceToPath = minPathDistance({ x, y }, pathCells)
        if (distanceToPath < 2 || distanceToPath > 7) {
          continue
        }

        candidates.push({ x, y })
      }
    }

    const shuffledCandidates = rng.shuffle(candidates)
    for (let index = 0; index < needed && index < shuffledCandidates.length; index += 1) {
      const cell = shuffledCandidates[index]
      context.grid[cell.y][cell.x] = encounterTileOptions[0]
      added += 1
    }

    const remainingNeeded = needed - added
    if (remainingNeeded > 0) {
      const fallbackCandidates: GridPoint[] = []
      for (let y = 0; y < height; y += 1) {
        for (let x = 0; x < width; x += 1) {
          const key = pointKey(x, y)
          if (pathSet.has(key) || townPathBuffer.has(key) || occupiedBuildingCells.has(key)) {
            continue
          }

          const tileId = context.grid[y][x]
          if (tileId !== baseTileId && tileId !== TILE_ID_FLOWER && tileId !== TILE_ID_TREE) {
            continue
          }

          const distanceToPath = minPathDistance({ x, y }, pathCells)
          if (distanceToPath < 1 || distanceToPath > 9) {
            continue
          }

          fallbackCandidates.push({ x, y })
        }
      }

      const shuffledFallback = rng.shuffle(fallbackCandidates)
      for (let index = 0; index < remainingNeeded && index < shuffledFallback.length; index += 1) {
        const cell = shuffledFallback[index]
        context.grid[cell.y][cell.x] = encounterTileOptions[0]
        added += 1
      }
    }
  }

  return { removed, added }
}

export const applyGenerationBalancePass = (context: GenerationContext): GenerationContext => {
  const occupiedBuildingCells = buildOccupiedBuildingCellSet(context)
  const clearedTownPathClutter = clearTownPathClutter(context, occupiedBuildingCells)
  const trainerTuning = clampTrainerDensity(context, occupiedBuildingCells)
  const encounterTuning = clampEncounterDensity(context, occupiedBuildingCells)

  if (clearedTownPathClutter > 0 || trainerTuning.removed > 0 || trainerTuning.added > 0 || encounterTuning.removed > 0 || encounterTuning.added > 0) {
    context.diagnostics.warnings.push(
      `Balance pass adjusted clutter:${clearedTownPathClutter} trainers:-${trainerTuning.removed}/+${trainerTuning.added} encounters:-${encounterTuning.removed}/+${encounterTuning.added}.`,
    )
  }

  return context
}
