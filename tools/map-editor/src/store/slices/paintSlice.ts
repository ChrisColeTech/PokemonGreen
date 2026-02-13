import { applyPaintOperation } from '../../services/gridService'
import type { EditorStoreSlice } from '../editorStore.types'
import { areGridsEqual, cloneGrid, GRASS_TILE_ID, pushPastGrid } from './shared'

type PaintSlice = Pick<
  import('../editorStore.types').EditorStoreState,
  'drawMode' | 'drawStartGrid' | 'beginDraw' | 'endDraw' | 'paintCell'
>

export const createPaintSlice: EditorStoreSlice<PaintSlice> = (set) => ({
  drawMode: null,
  drawStartGrid: null,
  beginDraw: (mode) =>
    set((state) => ({
      drawMode: mode,
      drawStartGrid: state.drawStartGrid ?? cloneGrid(state.grid),
    })),
  endDraw: () =>
    set((state) => {
      if (!state.drawStartGrid) {
        if (state.drawMode === null) {
          return state
        }

        return {
          drawMode: null,
        }
      }

      if (areGridsEqual(state.drawStartGrid, state.grid)) {
        return {
          drawMode: null,
          drawStartGrid: null,
        }
      }

      return {
        drawMode: null,
        drawStartGrid: null,
        pastGrids: pushPastGrid(state.pastGrids, state.drawStartGrid),
        futureGrids: [],
      }
    }),
  paintCell: (x, y, mode) =>
    set((state) => {
      const nextGrid = applyPaintOperation(state.grid, x, y, mode, state.selectedTileId, GRASS_TILE_ID)
      if (nextGrid === state.grid) {
        return state
      }

      if (state.drawStartGrid) {
        return {
          grid: nextGrid,
        }
      }

      return {
        grid: nextGrid,
        pastGrids: pushPastGrid(state.pastGrids, state.grid),
        futureGrids: [],
      }
    }),
})
