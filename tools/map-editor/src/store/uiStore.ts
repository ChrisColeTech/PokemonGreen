import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

interface UiStoreState {
  isSidebarOpen: boolean
  useDistinctColors: boolean
  toggleSidebar: () => void
  setSidebarOpen: (isOpen: boolean) => void
  setUseDistinctColors: (useDistinctColors: boolean) => void
}

export const useUiStore = create<UiStoreState>()(
  persist(
    (set) => ({
      isSidebarOpen: false,
      useDistinctColors: true,
      toggleSidebar: () => set((state) => ({ isSidebarOpen: !state.isSidebarOpen })),
      setSidebarOpen: (isOpen) => set({ isSidebarOpen: isOpen }),
      setUseDistinctColors: (useDistinctColors) => set({ useDistinctColors }),
    }),
    {
      name: 'map-editor-ui',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        isSidebarOpen: state.isSidebarOpen,
        useDistinctColors: state.useDistinctColors,
      }),
    },
  ),
)
