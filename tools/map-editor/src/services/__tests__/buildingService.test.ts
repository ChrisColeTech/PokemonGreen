import { describe, expect, it } from 'vitest'
import { getPlacementCells, placeBuildingFootprint, rotateBuildingFootprint } from '../buildingService'
import type { EditorRegistryBuildingDefinition } from '../../types/registry'

const footprint3x2: EditorRegistryBuildingDefinition = {
  id: 'workshop',
  name: 'Workshop',
  tiles: [
    [10, 11, 12],
    [13, null, 14],
  ],
}

describe('rotateBuildingFootprint', () => {
  it('rotates non-square footprints and swaps dimensions', () => {
    const rotated = rotateBuildingFootprint(footprint3x2, 1)

    expect(rotated.width).toBe(2)
    expect(rotated.height).toBe(3)
    expect(rotated.tiles).toEqual([
      [13, 10],
      [null, 11],
      [14, 12],
    ])
  })
})

describe('placement with variable building footprints', () => {
  it('places all non-null cells for wide footprints', () => {
    const cells = getPlacementCells(footprint3x2, 0, 2, 1, 10, 10)

    expect(cells).toEqual([
      { x: 2, y: 1, tileId: 10 },
      { x: 3, y: 1, tileId: 11 },
      { x: 4, y: 1, tileId: 12 },
      { x: 2, y: 2, tileId: 13 },
      { x: 4, y: 2, tileId: 14 },
    ])
  })

  it('clips placement at map boundaries for tall rotated footprints', () => {
    const cells = getPlacementCells(footprint3x2, 1, 4, 2, 5, 5)

    expect(cells).toEqual([
      { x: 4, y: 2, tileId: 13 },
      { x: 4, y: 4, tileId: 14 },
    ])
  })

  it('applies placement to grid for single-column buildings', () => {
    const fence: EditorRegistryBuildingDefinition = {
      id: 'fence-v',
      name: 'Fence Vertical',
      tiles: [[7], [8], [9], [10]],
    }
    const grid = Array.from({ length: 5 }, () => Array.from({ length: 5 }, () => 1))

    const next = placeBuildingFootprint(grid, fence, 0, 3, 0, 5, 5)

    expect(next[0][3]).toBe(7)
    expect(next[1][3]).toBe(8)
    expect(next[2][3]).toBe(9)
    expect(next[3][3]).toBe(10)
  })
})
