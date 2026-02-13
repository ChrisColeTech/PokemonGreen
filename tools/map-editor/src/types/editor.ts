export type TileCategory = 'terrain' | 'encounter' | 'interactive' | 'entity' | 'trainer'

export type PaletteCategory = TileCategory | 'buildings'

export interface TileDefinition {
  id: number
  name: string
  color: string
  walkable: boolean
  category: TileCategory
  encounter?: string
  direction?: 'up' | 'down' | 'left' | 'right'
  isOverlay?: boolean
}

export type TileGrid = number[][]
export type OverlayGrid = (number | null)[][]

export type DrawMode = 'paint' | 'erase' | null

export type BuildingId =
  | 'pokecenter'
  | 'pokemart'
  | 'gym'
  | 'house-small'
  | 'house-large'
  | 'lab'
  | 'cave-entrance'
  | 'gate'
  | 'pond'
  | 'fence-h'
  | 'fence-v'

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
