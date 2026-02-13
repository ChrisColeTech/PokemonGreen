import type { useRandomGenerationControls } from '../../hooks/useRandomGenerationControls'
import type { useSidebarControls } from '../../hooks/useSidebarControls'

export type SidebarControls = ReturnType<typeof useSidebarControls>
export type RandomGenerationControls = ReturnType<typeof useRandomGenerationControls>
