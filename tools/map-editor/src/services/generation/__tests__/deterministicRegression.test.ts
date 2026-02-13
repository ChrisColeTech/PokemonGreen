import { RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID } from '../../../data/generation/archetypes'
import { createDefaultRandomGenerationConfig, generateRandomMap } from '../randomMapGenerator'
import { describe, expect, it } from 'vitest'
import type { ArchetypeId, GenerationTemplateId } from '../../../types/generation'
import type { TileGrid } from '../../../types/editor'

interface RegressionFixture {
  archetypeId: ArchetypeId
  templateId: GenerationTemplateId
  seed: string
  expectedSignature: string
}

const countTiles = (grid: TileGrid, targetTileId: number): number => {
  let count = 0
  for (const row of grid) {
    for (const tileId of row) {
      if (tileId === targetTileId) {
        count += 1
      }
    }
  }
  return count
}

const gridHash = (grid: TileGrid): string => {
  let hash = 0x811c9dc5
  for (const row of grid) {
    for (const tileId of row) {
      hash ^= tileId
      hash = Math.imul(hash, 0x01000193) >>> 0
    }
  }
  return hash.toString(16).padStart(8, '0')
}

const buildSignature = (grid: TileGrid, primaryPathTileId: number): string => {
  const pathCount = countTiles(grid, primaryPathTileId)
  return `${gridHash(grid)}:${pathCount}`
}

const REGRESSION_FIXTURES: readonly RegressionFixture[] = [
  {
    archetypeId: 'town_route_basic',
    templateId: 'compact_town_spine',
    seed: 'phase8-reg-town-001',
    expectedSignature: 'a4083ebd:51',
  },
  {
    archetypeId: 'coastal_town_route',
    templateId: 'northern_crossing',
    seed: 'phase8-reg-coastal-002',
    expectedSignature: '515dc3e2:58',
  },
  {
    archetypeId: 'canyon_corridor_route',
    templateId: 'cliffside_detour',
    seed: 'phase8-reg-canyon-003',
    expectedSignature: '349c8592:47',
  },
  {
    archetypeId: 'riverlands_town_route',
    templateId: 'riverbend_market',
    seed: 'phase8-reg-river-004',
    expectedSignature: '011a9f3a:50',
  },
]

describe('deterministic generation regression fixtures', () => {
  it('keeps signatures stable for representative archetype/template combinations', () => {
    const actualSignatures = REGRESSION_FIXTURES.map((fixture) => {
      const result = generateRandomMap(
        createDefaultRandomGenerationConfig({
          seed: fixture.seed,
          archetypeId: fixture.archetypeId,
          templateId: fixture.templateId,
        }),
      )
      const primaryPathTileId = RANDOM_MAP_ARCHETYPE_PRESETS_BY_ID[fixture.archetypeId].tileRoles.primaryPathTileId

      return buildSignature(result.grid, primaryPathTileId)
    })

    expect(actualSignatures).toEqual(REGRESSION_FIXTURES.map((fixture) => fixture.expectedSignature))
  })
})
