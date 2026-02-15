import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import type { EditorTileRegistry, EditorTileDefinition, EditorBuildingDefinition } from '../types/editor'
import {
  loadDefaultRegistry,
  buildTilesById,
  buildingWidth,
  buildingHeight,
  fallbackTileId,
  fallbackCategoryId,
  parseCSharpMap,
  UNKNOWN_TILE,
} from '../services/registryService'
import { generateMapClass, exportRegistryJson } from '../services/codeGenService'

function createGrid(width: number, height: number): number[][] {
  return Array.from({ length: height }, () => Array(width).fill(1))
}

function rotateMatrix(tiles: (number | null)[][], width: number, height: number) {
  const rotated: (number | null)[][] = []
  for (let y = 0; y < width; y++) {
    rotated[y] = []
    for (let x = 0; x < height; x++) {
      rotated[y][x] = tiles[height - 1 - x][y]
    }
  }
  return { tiles: rotated, width: height, height: width }
}

const defaultRegistry = loadDefaultRegistry()
const defaultTilesById = buildTilesById(defaultRegistry)

interface EditorState {
  // Registry
  registry: EditorTileRegistry
  tilesById: Map<number, EditorTileDefinition>

  // Map
  mapData: number[][]
  mapWidth: number
  mapHeight: number
  cellSize: number
  mapName: string

  // Selection
  selectedTile: number
  selectedBuilding: number | null
  buildingRotation: number

  // Actions — registry
  setRegistry: (registry: EditorTileRegistry) => void
  getTile: (id: number) => EditorTileDefinition

  // Actions — map
  paint: (x: number, y: number, tileId?: number) => void
  resize: (width: number, height: number) => void
  clear: () => void
  rotateMap: (direction: 1 | -1) => void
  setMapData: (data: number[][], width: number, height: number) => void
  setCellSize: (size: number) => void
  setMapName: (name: string) => void

  // Actions — IO
  importJson: (json: string) => void
  importCSharpMap: (source: string) => void
  exportJson: () => string
  exportCSharp: () => string
  exportRegistryJson: () => string

  // Actions — selection
  selectTile: (id: number) => void
  selectBuilding: (idx: number) => void
  rotateBuilding: (direction: 1 | -1) => void

  // Actions — building placement
  placeBuilding: (x: number, y: number) => void
  getRotatedBuilding: () => { tiles: (number | null)[][]; width: number; height: number } | null
}

export const useEditorStore = create<EditorState>()(
  immer((set, get) => ({
    registry: defaultRegistry,
    tilesById: defaultTilesById,

    mapData: createGrid(25, 18),
    mapWidth: 25,
    mapHeight: 18,
    cellSize: 24,
    mapName: 'Untitled Map',

    selectedTile: 1,
    selectedBuilding: null,
    buildingRotation: 0,

    setRegistry: (registry) => set(state => {
      state.registry = registry as EditorTileRegistry
      state.tilesById = buildTilesById(registry) as Map<number, EditorTileDefinition>

      // Fallback: if selected tile doesn't exist in new registry, reset
      if (!registry.tiles.some(t => t.id === state.selectedTile)) {
        state.selectedTile = fallbackTileId(registry)
      }

      // Fallback: if selected building index out of range, clear
      if (state.selectedBuilding !== null && state.selectedBuilding >= registry.buildings.length) {
        state.selectedBuilding = null
        state.buildingRotation = 0
      }
    }),

    getTile: (id) => {
      return get().tilesById.get(id) ?? UNKNOWN_TILE
    },

    paint: (x, y, tileId) => set(state => {
      const id = tileId ?? state.selectedTile
      if (y < 0 || y >= state.mapHeight || x < 0 || x >= state.mapWidth) return
      if (state.mapData[y][x] === id && tileId === undefined) {
        state.mapData[y][x] = fallbackTileId(state.registry as EditorTileRegistry)
      } else {
        state.mapData[y][x] = id
      }
    }),

    resize: (width, height) => set(state => {
      const newMap: number[][] = []
      for (let y = 0; y < height; y++) {
        newMap[y] = []
        for (let x = 0; x < width; x++) {
          newMap[y][x] = state.mapData[y]?.[x] ?? fallbackTileId(state.registry as EditorTileRegistry)
        }
      }
      state.mapData = newMap
      state.mapWidth = width
      state.mapHeight = height
    }),

    clear: () => set(state => {
      state.mapData = createGrid(25, 18)
      state.mapWidth = 25
      state.mapHeight = 18
      state.cellSize = 24
      state.mapName = 'Untitled Map'
      state.selectedTile = fallbackTileId(state.registry as EditorTileRegistry)
      state.selectedBuilding = null
      state.buildingRotation = 0
    }),

    rotateMap: (direction) => set(state => {
      const oldW = state.mapWidth
      const oldH = state.mapHeight
      const newW = oldH
      const newH = oldW
      const newMap: number[][] = []

      for (let y = 0; y < newH; y++) {
        newMap[y] = []
        for (let x = 0; x < newW; x++) {
          if (direction === 1) {
            // Clockwise: new[y][x] = old[oldH - 1 - x][y]
            newMap[y][x] = state.mapData[oldH - 1 - x][y]
          } else {
            // Counter-clockwise: new[y][x] = old[x][oldW - 1 - y]
            newMap[y][x] = state.mapData[x][oldW - 1 - y]
          }
        }
      }

      state.mapData = newMap
      state.mapWidth = newW
      state.mapHeight = newH
    }),

    setMapData: (data, width, height) => set(state => {
      state.mapData = data
      state.mapWidth = width
      state.mapHeight = height
    }),

    setCellSize: (size) => set(state => {
      state.cellSize = size
    }),

    setMapName: (name) => set(state => {
      state.mapName = name
    }),

    importJson: (json) => {
      try {
        const data = JSON.parse(json)

        // Schema v2: baseTiles + overlayTiles
        if (data.baseTiles) {
          const width = data.width
          const height = data.height
          const base: number[][] = data.baseTiles
          const overlay: (number | null)[][] | undefined = data.overlayTiles

          const merged: number[][] = []
          for (let y = 0; y < height; y++) {
            merged[y] = []
            for (let x = 0; x < width; x++) {
              const ov = overlay?.[y]?.[x]
              merged[y][x] = ov != null ? ov : (base[y]?.[x] ?? 1)
            }
          }

          set(state => {
            state.mapData = merged
            state.mapWidth = width
            state.mapHeight = height
            state.mapName = data.displayName || data.mapId || 'Imported Map'
            if (data.tileSize) state.cellSize = data.tileSize
          })
          return
        }

        // Legacy format: tiles (flat 2D array)
        if (data.tiles) {
          set(state => {
            state.mapData = data.tiles
            state.mapWidth = data.width
            state.mapHeight = data.height
            state.mapName = data.name || 'Imported Map'
          })
          return
        }
      } catch {
        alert('Invalid JSON file')
      }
    },

    importCSharpMap: (source) => {
      try {
        const parsed = parseCSharpMap(source)
        set(state => {
          state.mapData = parsed.mapData
          state.mapWidth = parsed.width
          state.mapHeight = parsed.height
          state.mapName = parsed.displayName
          state.cellSize = parsed.tileSize
        })
      } catch (err) {
        alert(`Invalid C# map: ${err instanceof Error ? err.message : 'Unknown error'}`)
      }
    },

    exportJson: () => {
      const { mapData, mapWidth, mapHeight, cellSize, mapName, registry } = get()
      const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')

      const data = {
        schemaVersion: 2,
        mapId,
        displayName: mapName,
        tileSize: cellSize,
        width: mapWidth,
        height: mapHeight,
        baseTiles: mapData,
        overlayTiles: mapData.map(row => row.map(() => null)),
        registryId: registry.id,
        registryVersion: registry.version,
      }

      return JSON.stringify(data, null, 2)
    },

    exportCSharp: () => {
      const { mapData, mapWidth, mapHeight, cellSize, mapName, tilesById } = get()
      return generateMapClass(mapData, mapWidth, mapHeight, mapName, cellSize, tilesById as Map<number, EditorTileDefinition>)
    },

    exportRegistryJson: () => {
      const { registry } = get()
      return exportRegistryJson(registry as EditorTileRegistry)
    },

    selectTile: (id) => set(state => {
      state.selectedTile = id
      state.selectedBuilding = null
      state.buildingRotation = 0
    }),

    selectBuilding: (idx) => set(state => {
      if (state.selectedBuilding === idx) {
        state.selectedBuilding = null
        state.buildingRotation = 0
      } else {
        state.selectedBuilding = idx
        state.buildingRotation = 0
      }
    }),

    rotateBuilding: (direction) => set(state => {
      if (state.selectedBuilding === null) return
      state.buildingRotation = (state.buildingRotation + direction + 4) % 4
    }),

    placeBuilding: (x, y) => {
      const state = get()
      if (state.selectedBuilding === null) return
      const rotated = state.getRotatedBuilding()
      if (!rotated) return

      set(draft => {
        for (let by = 0; by < rotated.height; by++) {
          for (let bx = 0; bx < rotated.width; bx++) {
            const tx = x + bx
            const ty = y + by
            const tileId = rotated.tiles[by][bx]
            if (ty < draft.mapHeight && tx < draft.mapWidth && tileId !== null) {
              draft.mapData[ty][tx] = tileId
            }
          }
        }
      })
    },

    getRotatedBuilding: () => {
      const { selectedBuilding, buildingRotation, registry } = get()
      if (selectedBuilding === null) return null
      const building = registry.buildings[selectedBuilding]
      if (!building) return null

      let tiles = building.tiles.map(row => [...row])
      let w = buildingWidth(building)
      let h = buildingHeight(building)

      for (let r = 0; r < buildingRotation; r++) {
        const result = rotateMatrix(tiles, w, h)
        tiles = result.tiles
        w = result.width
        h = result.height
      }

      return { tiles, width: w, height: h }
    },
  }))
)
