import { describe, expect, it } from 'vitest'
import { createRegistrySlice } from '../registrySlice'
import type { EditorStoreState } from '../../editorStore.types'
import type { EditorTileRegistry } from '../../../types/registry'

const buildRegistry = (overrides?: Partial<EditorTileRegistry>): EditorTileRegistry => ({
  metadata: {
    id: 'registry-a',
    name: 'Registry A',
    version: '1.0.0',
  },
  categories: [
    { id: 'terrain', label: 'Terrain', showInPalette: true },
    { id: 'special', label: 'Special', showInPalette: true },
  ],
  tiles: [
    { id: 1, name: 'Grass', color: '#00aa00', walkable: true, category: 'terrain' },
    { id: 2, name: 'Wall', color: '#666666', walkable: false, category: 'special' },
  ],
  buildings: [
    {
      id: 'hut',
      name: 'Hut',
      tiles: [
        [1, 1],
        [1, 1],
      ],
    },
  ],
  ...overrides,
})

const createSliceHarness = (initialState: Partial<EditorStoreState>) => {
  let state = initialState as EditorStoreState

  const setState = (updater: Partial<EditorStoreState> | ((current: EditorStoreState) => Partial<EditorStoreState>)) => {
    const patch = typeof updater === 'function' ? updater(state) : updater
    state = {
      ...state,
      ...patch,
    }
  }

  const slice = createRegistrySlice(setState as never, (() => state) as never, {} as never)
  state = {
    ...state,
    ...slice,
  }

  return {
    getState: () => state,
  }
}

describe('registry slice switching behavior', () => {
  it('falls back missing category, tile, and building on registry switch', () => {
    const sourceRegistry = buildRegistry()
    const targetRegistry = buildRegistry({
      metadata: { id: 'registry-b', name: 'Registry B', version: '2.0.0' },
      categories: [{ id: 'zone', label: 'Zone', showInPalette: true }],
      tiles: [
        { id: 8, name: 'Rock', color: '#444444', walkable: false, category: 'zone' },
        { id: 9, name: 'Route', color: '#44aa44', walkable: true, category: 'zone' },
      ],
      buildings: [],
    })

    const harness = createSliceHarness({
      activeRegistry: sourceRegistry,
      selectedCategory: 'buildings',
      selectedTileId: 999,
      selectedBuildingId: 'hut',
    })

    harness.getState().setActiveRegistry(targetRegistry)

    const nextState = harness.getState()
    expect(nextState.selectedCategory).toBe('zone')
    expect(nextState.selectedTileId).toBe(9)
    expect(nextState.selectedBuildingId).toBeNull()
  })

  it('keeps selected values when they exist in the next registry', () => {
    const sourceRegistry = buildRegistry()
    const targetRegistry = buildRegistry({ metadata: { id: 'registry-c', name: 'Registry C', version: '2.1.0' } })

    const harness = createSliceHarness({
      activeRegistry: sourceRegistry,
      selectedCategory: 'terrain',
      selectedTileId: 1,
      selectedBuildingId: 'hut',
    })

    harness.getState().setActiveRegistry(targetRegistry)

    const nextState = harness.getState()
    expect(nextState.selectedCategory).toBe('terrain')
    expect(nextState.selectedTileId).toBe(1)
    expect(nextState.selectedBuildingId).toBe('hut')
  })
})
