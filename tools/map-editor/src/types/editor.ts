export type TileCategory = string

export type PaletteCategory = string

export type TileDirection =
  | 'up'
  | 'down'
  | 'left'
  | 'right'
  | 'north'
  | 'south'
  | 'east'
  | 'west'
  | 'northeast'
  | 'northwest'
  | 'southeast'
  | 'southwest'

export interface TileDefinition {
  id: number
  name: string
  color: string
  walkable: boolean
  category: TileCategory
  encounter?: string
  direction?: TileDirection
  isOverlay?: boolean
}

export type TileGrid = number[][]
export type OverlayGrid = (number | null)[][]

export type DrawMode = 'paint' | 'erase' | null

export type BuildingId = string

export type BuildingRotation = 0 | 1 | 2 | 3

export type BuildingStampCell = number | null
export type BuildingStampMatrix = BuildingStampCell[][]

export interface BuildingDefinition {
  id: BuildingId
  name: string
  width: number
  height: number
  tiles: BuildingStampMatrix
}

export interface GridPoint {
  x: number
  y: number
}

export interface BuildingPlacementCell extends GridPoint {
  tileId: number
}
