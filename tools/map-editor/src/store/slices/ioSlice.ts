import type { EditorStoreSlice } from '../editorStore.types'
import { cloneGrid, pushPastGrid } from './shared'

type IoSlice = Pick<import('../editorStore.types').EditorStoreState, 'loadMapData'>

export const createIoSlice: EditorStoreSlice<IoSlice> = (set) => ({
  loadMapData: (mapData) =>
    set((state) => ({
      mapName: mapData.displayName?.trim() || state.mapName,
      mapWidth: mapData.width,
      mapHeight: mapData.height,
      widthInput: mapData.width,
      heightInput: mapData.height,
      drawMode: null,
      grid: cloneGrid(mapData.tiles),
      pastGrids: pushPastGrid(state.pastGrids, state.grid),
      futureGrids: [],
      drawStartGrid: null,
    })),
})
