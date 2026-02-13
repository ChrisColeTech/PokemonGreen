import { RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID } from '../../../data/generation/archetypes'
import { createGrid } from '../../gridService'
import { createDefaultRandomGenerationConfig } from '../randomMapGenerator'
import { createSeededRng } from '../seededRng'
import { replaceUnknownTileIds } from '../repair/actions/replaceUnknownTileIds'
import { knownTileIdsOnlyValidator } from '../validation/constraints/knownTileIdsOnly'
import { describe, expect, it } from 'vitest'
import type { GenerationContext } from '../../../types/generation'
import type { TileGrid } from '../../../types/editor'

const createGenerationContext = (grid: TileGrid): GenerationContext => {
  const width = grid[0]?.length ?? 0
  const height = grid.length
  const config = createDefaultRandomGenerationConfig({
    seed: 'phase8-module-tests',
    archetypeId: 'town_route_basic',
    templateId: 'compact_town_spine',
    dimensions: { width, height },
  })

  return {
    config,
    archetype: RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[config.archetypeId],
    rng: createSeededRng(config.seed),
    grid,
    buildingPlacements: [],
    state: {
      primaryPathCells: [],
      reservedDistricts: [],
      encounterAnchorCells: [],
      activeTemplateId: null,
      templateHints: null,
    },
    diagnostics: {
      warnings: [],
      passDurationsMs: {},
      hardConstraintIssues: [],
      softGoalScores: [],
      appliedRepairs: [],
    },
  }
}

describe('knownTileIdsOnly validator', () => {
  it('reports all cells with unknown tile ids', () => {
    const grid = createGrid(5, 5, 1)
    grid[1][3] = 999
    grid[4][0] = -1
    const context = createGenerationContext(grid)

    const issues = knownTileIdsOnlyValidator(context)

    expect(issues).toHaveLength(1)
    expect(issues[0].id).toBe('knownTileIdsOnly')
    expect(issues[0].cells).toEqual([
      { x: 3, y: 1 },
      { x: 0, y: 4 },
    ])
  })
})

describe('replaceUnknownTileIds repair action', () => {
  it('replaces unknown ids with archetype base terrain id', () => {
    const grid = createGrid(5, 5, 1)
    grid[0][0] = 999
    grid[2][4] = -8
    const context = createGenerationContext(grid)

    const replacedCount = replaceUnknownTileIds(context)

    expect(replacedCount).toBe(2)
    expect(context.grid[0][0]).toBe(context.archetype.tileRoles.baseTerrainTileId)
    expect(context.grid[2][4]).toBe(context.archetype.tileRoles.baseTerrainTileId)
    expect(knownTileIdsOnlyValidator(context)).toEqual([])
  })
})
