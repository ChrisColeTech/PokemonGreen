import type { EditorStoreSlice } from '../editorStore.types'
import { cloneGrid, pushPastGrid } from './shared'

type HistorySlice = Pick<import('../editorStore.types').EditorStoreState, 'pastGrids' | 'futureGrids' | 'undo' | 'redo'>

export const createHistorySlice: EditorStoreSlice<HistorySlice> = (set) => ({
  pastGrids: [],
  futureGrids: [],
  undo: () =>
    set((state) => {
      if (state.pastGrids.length === 0) {
        return state
      }

      const previousGrid = state.pastGrids[state.pastGrids.length - 1]
      const nextPast = state.pastGrids.slice(0, state.pastGrids.length - 1)

      return {
        grid: cloneGrid(previousGrid),
        mapWidth: previousGrid[0]?.length ?? state.mapWidth,
        mapHeight: previousGrid.length,
        widthInput: previousGrid[0]?.length ?? state.widthInput,
        heightInput: previousGrid.length,
        drawMode: null,
        drawStartGrid: null,
        pastGrids: nextPast,
        futureGrids: [cloneGrid(state.grid), ...state.futureGrids],
      }
    }),
  redo: () =>
    set((state) => {
      if (state.futureGrids.length === 0) {
        return state
      }

      const [nextGrid, ...remainingFuture] = state.futureGrids
      return {
        grid: cloneGrid(nextGrid),
        mapWidth: nextGrid[0]?.length ?? state.mapWidth,
        mapHeight: nextGrid.length,
        widthInput: nextGrid[0]?.length ?? state.widthInput,
        heightInput: nextGrid.length,
        drawMode: null,
        drawStartGrid: null,
        pastGrids: pushPastGrid(state.pastGrids, state.grid),
        futureGrids: remainingFuture,
      }
    }),
})
