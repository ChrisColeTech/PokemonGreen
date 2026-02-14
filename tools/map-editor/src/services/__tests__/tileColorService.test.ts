import { describe, expect, it } from 'vitest'
import { createDistinctTileColorMap, getDisplayTileColor } from '../tileColorService'
import type { EditorRegistryTileDefinition } from '../../types/registry'

const dynamicTiles: EditorRegistryTileDefinition[] = [
  { id: 0, name: 'Anchor Water', color: '#808080', walkable: false, category: 'terrain' },
  { id: 1, name: 'Anchor Grass', color: '#808080', walkable: true, category: 'terrain' },
  { id: 2, name: 'Anchor Path', color: '#808080', walkable: true, category: 'terrain' },
  { id: 70, name: 'Neon A', color: '#55aaee', walkable: true, category: 'new-biome' },
  { id: 71, name: 'Neon B', color: '#33bb77', walkable: true, category: 'new-biome' },
]

describe('createDistinctTileColorMap', () => {
  it('creates distinct colors for dynamic category ids deterministically', () => {
    const first = createDistinctTileColorMap(dynamicTiles)
    const second = createDistinctTileColorMap(dynamicTiles)

    expect(first).toEqual(second)
    expect(first[70]).not.toBe(dynamicTiles[3].color)
    expect(first[71]).not.toBe(dynamicTiles[4].color)
  })

  it('keeps anchor tile ids on source colors', () => {
    const map = createDistinctTileColorMap(dynamicTiles)

    expect(map[0]).toBe('#808080')
    expect(map[1]).toBe('#808080')
    expect(map[2]).toBe('#808080')
  })
})

describe('getDisplayTileColor', () => {
  it('falls back to tile color when distinct map entry is missing', () => {
    const tile = dynamicTiles[3]

    expect(getDisplayTileColor(tile, {}, true)).toBe(tile.color)
    expect(getDisplayTileColor(tile, { 70: '#123456' }, false)).toBe(tile.color)
    expect(getDisplayTileColor(tile, { 70: '#123456' }, true)).toBe('#123456')
  })
})
