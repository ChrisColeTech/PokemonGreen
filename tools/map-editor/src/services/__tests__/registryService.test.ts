import { describe, expect, it } from 'vitest'
import { parseRegistryJson, resolveFallbackTileId } from '../registryService'
import type { EditorTileRegistry } from '../../types/registry'

const buildValidRegistry = (): EditorTileRegistry => ({
  metadata: {
    id: 'test-registry',
    name: 'Test Registry',
    version: '1.0.0',
  },
  categories: [
    { id: 'terrain', label: 'Terrain', showInPalette: true },
    { id: 'special-zone', label: 'Special Zone', showInPalette: true },
  ],
  tiles: [
    { id: 40, name: 'Stone', color: '#777777', walkable: false, category: 'terrain' },
    { id: 41, name: 'Field', color: '#00aa00', walkable: true, category: 'terrain' },
    { id: 42, name: 'Portal', color: '#4f88ff', walkable: true, category: 'special-zone' },
  ],
  buildings: [
    {
      id: 'lab',
      name: 'Lab',
      tiles: [
        [41, 41],
        [41, null],
      ],
    },
  ],
})

describe('parseRegistryJson', () => {
  it('parses a valid registry payload', () => {
    const result = parseRegistryJson(JSON.stringify(buildValidRegistry()), 'fixture')

    expect(result.ok).toBe(true)
    if (!result.ok) {
      return
    }

    expect(result.data.metadata.id).toBe('test-registry')
    expect(result.data.categories).toHaveLength(2)
    expect(result.data.tiles).toHaveLength(3)
    expect(result.data.buildings).toHaveLength(1)
  })

  it('rejects buildings that reference unknown tile ids', () => {
    const invalid = buildValidRegistry()
    invalid.buildings = [
      {
        id: 'invalid-house',
        name: 'Invalid House',
        tiles: [[999]],
      },
    ]

    const result = parseRegistryJson(JSON.stringify(invalid), 'fixture')

    expect(result.ok).toBe(false)
    if (result.ok) {
      return
    }

    expect(result.error).toContain('references unknown tile id 999')
  })

  it('rejects duplicate category ids', () => {
    const invalid = buildValidRegistry()
    invalid.categories = [
      { id: 'terrain', label: 'Terrain', showInPalette: true },
      { id: 'terrain', label: 'Terrain Again', showInPalette: true },
    ]

    const result = parseRegistryJson(JSON.stringify(invalid), 'fixture')

    expect(result.ok).toBe(false)
    if (result.ok) {
      return
    }

    expect(result.error).toContain("id 'terrain' is duplicated")
  })
})

describe('resolveFallbackTileId', () => {
  it('prefers a walkable terrain-like tile when available', () => {
    const registry = buildValidRegistry()

    expect(resolveFallbackTileId(registry)).toBe(41)
  })

  it('falls back to first walkable tile if no terrain-like category exists', () => {
    const registry = buildValidRegistry()
    registry.tiles = [
      { id: 5, name: 'Block', color: '#111111', walkable: false, category: 'rocks' },
      { id: 6, name: 'Walk', color: '#abcdef', walkable: true, category: 'roads' },
    ]

    expect(resolveFallbackTileId(registry)).toBe(6)
  })
})
