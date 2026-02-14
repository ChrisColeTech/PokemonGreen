import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { EditorStoreState } from './editorStore.types'
import { createBuildingSlice } from './slices/buildingSlice'
import { createGenerationSlice } from './slices/generationSlice'
import { createHistorySlice } from './slices/historySlice'
import { createIoSlice } from './slices/ioSlice'
import { createMapSlice } from './slices/mapSlice'
import { createPaintSlice } from './slices/paintSlice'
import { createRegistrySlice } from './slices/registrySlice'
import { partializeEditorStore, STORE_NAME } from './slices/shared'

export const useEditorStore = create<EditorStoreState>()(
  persist(
    (...args) => ({
      ...createMapSlice(...args),
      ...createRegistrySlice(...args),
      ...createPaintSlice(...args),
      ...createBuildingSlice(...args),
      ...createHistorySlice(...args),
      ...createGenerationSlice(...args),
      ...createIoSlice(...args),
    }),
    {
      name: STORE_NAME,
      storage: createJSONStorage(() => localStorage),
      partialize: partializeEditorStore,
    },
  ),
)
