import type { TileDirection } from './editor'

export interface EditorRegistryMetadata {
  id: string
  name: string
  version: string
}

export interface EditorRegistryCategoryDefinition {
  id: string
  label: string
  showInPalette: boolean
}

export interface EditorRegistryTileDefinition {
  id: number
  name: string
  color: string
  walkable: boolean
  category: string
  encounter?: string
  direction?: TileDirection
  isOverlay?: boolean
}

export interface EditorRegistryBuildingDefinition {
  id: string
  name: string
  tiles: (number | null)[][]
}

export interface EditorTileRegistry {
  metadata: EditorRegistryMetadata
  categories: EditorRegistryCategoryDefinition[]
  tiles: EditorRegistryTileDefinition[]
  buildings: EditorRegistryBuildingDefinition[]
}
