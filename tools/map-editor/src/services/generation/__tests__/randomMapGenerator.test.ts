import { RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID } from '../../../data/generation/archetypes'
import { TILES_BY_ID } from '../../../data/tiles'
import { createDefaultRandomGenerationConfig, generateRandomMap } from '../randomMapGenerator'
import { GENERATION_FIXTURES } from '../testing/generationFixtures'
import { describe, expect, it } from 'vitest'
import type { GridPoint, TileGrid } from '../../../types/editor'

const pointKey = (x: number, y: number): string => `${x},${y}`

const countTileIds = (grid: TileGrid, tileIds: ReadonlySet<number>): number => {
  let count = 0
  for (const row of grid) {
    for (const tileId of row) {
      if (tileIds.has(tileId)) {
        count += 1
      }
    }
  }

  return count
}

const collectPathCells = (grid: TileGrid, pathTileId: number): GridPoint[] => {
  const cells: GridPoint[] = []
  for (let y = 0; y < grid.length; y += 1) {
    for (let x = 0; x < grid[y].length; x += 1) {
      if (grid[y][x] === pathTileId) {
        cells.push({ x, y })
      }
    }
  }
  return cells
}

const buildPathSignature = (grid: TileGrid, pathTileId: number): string => {
  const width = grid[0]?.length ?? 0
  const height = grid.length
  const byColumn = new Map<number, number[]>()

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (grid[y][x] !== pathTileId) {
        continue
      }

      const existing = byColumn.get(x)
      if (existing) {
        existing.push(y)
      } else {
        byColumn.set(x, [y])
      }
    }
  }

  const parts: string[] = []
  for (let x = 0; x < width; x += 1) {
    const values = byColumn.get(x)
    if (!values || values.length === 0) {
      parts.push('x')
      continue
    }

    const minY = Math.min(...values)
    const maxY = Math.max(...values)
    const meanY = values.reduce((sum, value) => sum + value, 0) / values.length
    const normalizedMean = Math.round((meanY / Math.max(1, height - 1)) * 10)
    parts.push(`${normalizedMean}.${maxY - minY}`)
  }

  return parts.join('|')
}

const isPathConnectedAcrossEdges = (grid: TileGrid, pathTileId: number): boolean => {
  const width = grid[0]?.length ?? 0
  const height = grid.length
  const pathCells = collectPathCells(grid, pathTileId)
  if (width === 0 || height === 0 || pathCells.length === 0) {
    return false
  }

  const pathSet = new Set(pathCells.map((cell) => pointKey(cell.x, cell.y)))
  const start = pathCells[0]
  const queue: GridPoint[] = [start]
  const visited = new Set<string>([pointKey(start.x, start.y)])
  const offsets: GridPoint[] = [
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

    for (const offset of offsets) {
      const nx = current.x + offset.x
      const ny = current.y + offset.y
      const key = pointKey(nx, ny)
      if (nx < 0 || ny < 0 || nx >= width || ny >= height || !pathSet.has(key) || visited.has(key)) {
        continue
      }

      visited.add(key)
      queue.push({ x: nx, y: ny })
    }
  }

  const touchesLeftEdge = pathCells.some((cell) => cell.x === 0)
  const touchesRightEdge = pathCells.some((cell) => cell.x === width - 1)
  return visited.size === pathCells.length && touchesLeftEdge && touchesRightEdge
}

describe('random map generation sanity fixtures', () => {
  it('produces deterministic output for the same seed and archetype', () => {
    for (const fixture of GENERATION_FIXTURES) {
      const config = createDefaultRandomGenerationConfig({
        seed: fixture.seed,
        archetypeId: fixture.archetypeId,
        templateId: fixture.templateId,
      })

      const first = generateRandomMap(config)
      const second = generateRandomMap(config)

      expect(first.grid).toEqual(second.grid)
      expect(first.diagnostics.hardConstraintIssues).toEqual(second.diagnostics.hardConstraintIssues)
    }
  })

  it('keeps required hard constraints resolved with repairs available', () => {
    for (const fixture of GENERATION_FIXTURES) {
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: fixture.seed,
          archetypeId: fixture.archetypeId,
          templateId: fixture.templateId,
          pipeline: {
            maxRepairAttempts: 3,
          },
        }),
      )

      expect(result.diagnostics.hardConstraintIssues.map((issue) => issue.id)).not.toContain('minRequiredStructures')
      expect(result.diagnostics.passDurationsMs.repair).toBeTypeOf('number')
    }
  })

  it('meets path connectivity expectation from fixture invariants', () => {
    for (const fixture of GENERATION_FIXTURES) {
      const archetype = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[fixture.archetypeId]
      const pathTileId = archetype.tileRoles.primaryPathTileId
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: fixture.seed,
          archetypeId: fixture.archetypeId,
          templateId: fixture.templateId,
        }),
      )

      const pathCells = collectPathCells(result.grid, pathTileId)
      expect(pathCells.length).toBeGreaterThanOrEqual(fixture.invariants.minPathTiles)
      expect(isPathConnectedAcrossEdges(result.grid, pathTileId)).toBe(fixture.invariants.expectConnectedPathEdges)
    }
  })

  it('uses known tile ids and balanced density bounds', () => {
    for (const fixture of GENERATION_FIXTURES) {
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: fixture.seed,
          archetypeId: fixture.archetypeId,
          templateId: fixture.templateId,
        }),
      )

      for (const row of result.grid) {
        for (const tileId of row) {
          expect(TILES_BY_ID[tileId]).toBeDefined()
        }
      }

      const cellCount = result.width * result.height
      const trainerCount = countTileIds(result.grid, new Set([20, 21, 22, 23]))
      const encounterCount = countTileIds(result.grid, new Set([7, 15, 28]))
      const trainerDensity = trainerCount / cellCount
      const encounterDensity = encounterCount / cellCount

      expect(trainerDensity).toBeGreaterThanOrEqual(fixture.invariants.trainerDensityRange[0])
      expect(trainerDensity).toBeLessThanOrEqual(fixture.invariants.trainerDensityRange[1])
      expect(encounterDensity).toBeGreaterThanOrEqual(fixture.invariants.encounterDensityRange[0])
      expect(encounterDensity).toBeLessThanOrEqual(fixture.invariants.encounterDensityRange[1])
      expect(result.diagnostics.passDurationsMs.balance).toBeTypeOf('number')
    }
  })

  it('produces visibly different route profiles across template and archetype inputs', () => {
    const sharedSeed = 'route-variation-014'
    const sharedDimensions = { width: 30, height: 20 }

    const templateRuns = [
      { archetypeId: 'town_route_basic', templateId: 'compact_town_spine' },
      { archetypeId: 'town_route_basic', templateId: 'central_switchbacks' },
      { archetypeId: 'town_route_basic', templateId: 'highland_sweep' },
      { archetypeId: 'town_route_basic', templateId: 'lowland_bypass' },
    ] as const

    const templateSignatures = templateRuns.map((input) => {
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: sharedSeed,
          dimensions: sharedDimensions,
          archetypeId: input.archetypeId,
          templateId: input.templateId,
        }),
      )
      const pathTileId = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[input.archetypeId].tileRoles.primaryPathTileId
      return buildPathSignature(result.grid, pathTileId)
    })

    expect(new Set(templateSignatures).size).toBeGreaterThanOrEqual(3)

    const archetypeRuns = [
      { archetypeId: 'town_route_basic', templateId: 'compact_town_spine' },
      { archetypeId: 'canyon_corridor_route', templateId: 'compact_town_spine' },
      { archetypeId: 'riverlands_town_route', templateId: 'compact_town_spine' },
    ] as const

    const archetypeSignatures = archetypeRuns.map((input) => {
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: sharedSeed,
          dimensions: sharedDimensions,
          archetypeId: input.archetypeId,
          templateId: input.templateId,
        }),
      )
      const pathTileId = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[input.archetypeId].tileRoles.primaryPathTileId
      return buildPathSignature(result.grid, pathTileId)
    })

    expect(new Set(archetypeSignatures).size).toBeGreaterThanOrEqual(2)
  })
})
