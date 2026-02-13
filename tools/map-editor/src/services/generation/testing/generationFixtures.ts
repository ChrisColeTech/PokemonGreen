import type { ArchetypeId, GenerationTemplateId } from '../../../types/generation'

export interface GenerationFixtureInvariants {
  minPathTiles: number
  expectConnectedPathEdges: boolean
  maxHardConstraintIssues: number
  trainerDensityRange: readonly [number, number]
  encounterDensityRange: readonly [number, number]
}

export interface GenerationFixture {
  id: string
  seed: string
  archetypeId: ArchetypeId
  templateId: GenerationTemplateId | null
  invariants: GenerationFixtureInvariants
}

export const GENERATION_FIXTURES: readonly GenerationFixture[] = [
  {
    id: 'basic-route-reference',
    seed: 'phase6-basic-001',
    archetypeId: 'town_route_basic',
    templateId: 'compact_town_spine',
    invariants: {
      minPathTiles: 24,
      expectConnectedPathEdges: true,
      maxHardConstraintIssues: 0,
      trainerDensityRange: [0.001, 0.02],
      encounterDensityRange: [0.03, 0.16],
    },
  },
  {
    id: 'coastal-route-reference',
    seed: 'phase6-coastal-019',
    archetypeId: 'coastal_town_route',
    templateId: 'northern_crossing',
    invariants: {
      minPathTiles: 28,
      expectConnectedPathEdges: true,
      maxHardConstraintIssues: 0,
      trainerDensityRange: [0.001, 0.02],
      encounterDensityRange: [0.029, 0.16],
    },
  },
  {
    id: 'forest-route-reference',
    seed: 'phase6-forest-007',
    archetypeId: 'forest_town_route',
    templateId: 'southern_wilds',
    invariants: {
      minPathTiles: 24,
      expectConnectedPathEdges: true,
      maxHardConstraintIssues: 0,
      trainerDensityRange: [0.001, 0.025],
      encounterDensityRange: [0.03, 0.17],
    },
  },
]
