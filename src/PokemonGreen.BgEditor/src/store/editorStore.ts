import { create } from 'zustand'
import type * as THREE from 'three'
import type { LoadedTexture, TextureAdjustment } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'
import { loadSceneFromFiles } from '../services/sceneService'

interface EditorState {
  // Scene
  sceneName: string | null
  scene: THREE.Group | null
  textures: LoadedTexture[]
  selectedTextureIndex: number
  loading: boolean
  error: string | null

  // Actions
  loadFiles: (files: File[]) => Promise<void>
  selectTexture: (index: number) => void
  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => void
  resetTexture: (index: number) => void
  resetAll: () => void
  applyToAll: () => void
}

export const useEditorStore = create<EditorState>()((set, get) => ({
  sceneName: null,
  scene: null,
  textures: [],
  selectedTextureIndex: 0,
  loading: false,
  error: null,

  loadFiles: async (files: File[]) => {
    set({ loading: true, error: null })
    try {
      const modelFile = files.find(f => f.name.endsWith('.dae') || f.name.endsWith('.obj'))
      const result = await loadSceneFromFiles(files)
      set({
        scene: result.scene,
        textures: result.textures,
        sceneName: modelFile?.name ?? 'Unknown',
        selectedTextureIndex: 0,
        loading: false,
      })
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load scene',
        loading: false,
      })
    }
  },

  selectTexture: (index: number) => {
    set({ selectedTextureIndex: index })
  },

  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => {
    const textures = [...get().textures]
    if (!textures[index]) return
    textures[index] = {
      ...textures[index],
      adjustment: { ...textures[index].adjustment, ...adj },
    }
    set({ textures })
  },

  resetTexture: (index: number) => {
    const textures = [...get().textures]
    if (!textures[index]) return
    textures[index] = {
      ...textures[index],
      adjustment: { ...DEFAULT_ADJUSTMENT },
      modifiedDataUrl: textures[index].originalDataUrl,
    }
    set({ textures })
  },

  resetAll: () => {
    const textures = get().textures.map(t => ({
      ...t,
      adjustment: { ...DEFAULT_ADJUSTMENT },
      modifiedDataUrl: t.originalDataUrl,
    }))
    set({ textures })
  },

  applyToAll: () => {
    const { textures, selectedTextureIndex } = get()
    const source = textures[selectedTextureIndex]
    if (!source) return
    const adj = source.adjustment
    const updated = textures.map(t => ({
      ...t,
      adjustment: { ...adj },
    }))
    set({ textures: updated })
  },
}))
