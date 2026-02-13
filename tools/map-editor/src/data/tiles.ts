import type { PaletteCategory, TileDefinition } from '../types/editor'

export const GRASS_TILE_ID = 1
export const DEFAULT_MAP_WIDTH = 25
export const DEFAULT_MAP_HEIGHT = 18
export const DEFAULT_CELL_SIZE = 24

export const TILE_CATEGORIES: Array<{ id: PaletteCategory; label: string }> = [
  { id: 'terrain', label: 'Terrain' },
  { id: 'encounter', label: 'Encounters' },
  { id: 'interactive', label: 'Interactive' },
  { id: 'entity', label: 'Entities' },
  { id: 'trainer', label: 'Trainers' },
  { id: 'buildings', label: 'Buildings' },
]

export const OVERLAY_TILE_IDS = new Set([3, 8, 19, 26, 27, 49])

export const isOverlayTile = (tileId: number): boolean => OVERLAY_TILE_IDS.has(tileId)

export const TILES: TileDefinition[] = [
  { id: 0, name: 'Water', color: '#1a4a7a', walkable: false, category: 'terrain' },
  { id: 1, name: 'Grass', color: '#2d5a27', walkable: true, category: 'terrain' },
  { id: 2, name: 'Path', color: '#c9a86c', walkable: true, category: 'terrain' },
  { id: 3, name: 'Tree', color: '#1a4a1a', walkable: false, category: 'terrain', isOverlay: true },
  { id: 4, name: 'Door', color: '#8b4513', walkable: true, category: 'interactive' },
  { id: 5, name: 'Bridge', color: '#6b4423', walkable: true, category: 'terrain' },
  { id: 6, name: 'Wall', color: '#555555', walkable: false, category: 'terrain' },
  { id: 7, name: 'Tall Grass', color: '#1a8a1a', walkable: true, category: 'encounter', encounter: 'wild' },
  { id: 8, name: 'Rock', color: '#696969', walkable: false, category: 'terrain', isOverlay: true },
  { id: 9, name: 'Sign', color: '#8b7355', walkable: false, category: 'interactive' },
  { id: 10, name: 'NPC', color: '#ff6b6b', walkable: false, category: 'entity' },
  { id: 11, name: 'Shop', color: '#ffd93d', walkable: false, category: 'entity' },
  { id: 12, name: 'Heal', color: '#6bcb77', walkable: false, category: 'entity' },
  { id: 13, name: 'Item', color: '#9d4edd', walkable: true, category: 'entity' },
  { id: 14, name: 'Key Item', color: '#e040fb', walkable: true, category: 'entity' },
  { id: 15, name: 'Cave', color: '#2c2c2c', walkable: true, category: 'encounter', encounter: 'wild_cave' },
  { id: 16, name: 'Warp', color: '#00cec9', walkable: true, category: 'interactive' },
  { id: 17, name: 'Water Edge', color: '#2980b9', walkable: false, category: 'terrain' },
  { id: 18, name: 'Fence', color: '#795548', walkable: false, category: 'terrain' },
  { id: 19, name: 'Flower', color: '#e84393', walkable: true, category: 'terrain', isOverlay: true },
  { id: 20, name: 'Trainer Up', color: '#ff922b', walkable: false, category: 'trainer', direction: 'up' },
  { id: 21, name: 'Trainer Down', color: '#ff922b', walkable: false, category: 'trainer', direction: 'down' },
  { id: 22, name: 'Trainer Left', color: '#ff922b', walkable: false, category: 'trainer', direction: 'left' },
  { id: 23, name: 'Trainer Right', color: '#ff922b', walkable: false, category: 'trainer', direction: 'right' },
  { id: 24, name: 'Gym Leader', color: '#ff1744', walkable: false, category: 'trainer', direction: 'down' },
  {
    id: 25,
    name: 'Surf Water',
    color: '#1e90ff',
    walkable: false,
    category: 'encounter',
    encounter: 'surf',
  },
  { id: 26, name: 'Strength Rock', color: '#8d6e63', walkable: false, category: 'interactive', isOverlay: true },
  { id: 27, name: 'Cut Tree', color: '#4caf50', walkable: false, category: 'interactive', isOverlay: true },
  { id: 28, name: 'Rare Grass', color: '#ffd700', walkable: true, category: 'encounter', encounter: 'rare_wild' },
  { id: 29, name: 'Legendary', color: '#ff00ff', walkable: true, category: 'encounter', encounter: 'legendary' },
  { id: 30, name: 'Villain Boss', color: '#800080', walkable: false, category: 'trainer', direction: 'down' },
  { id: 31, name: 'Villain Up', color: '#4a0080', walkable: false, category: 'trainer', direction: 'up' },
  { id: 32, name: 'Villain Down', color: '#4a0080', walkable: false, category: 'trainer', direction: 'down' },
  { id: 33, name: 'Villain Left', color: '#4a0080', walkable: false, category: 'trainer', direction: 'left' },
  { id: 34, name: 'Villain Right', color: '#4a0080', walkable: false, category: 'trainer', direction: 'right' },
  { id: 35, name: 'Minion Up', color: '#6a0dad', walkable: false, category: 'trainer', direction: 'up' },
  { id: 36, name: 'Minion Down', color: '#6a0dad', walkable: false, category: 'trainer', direction: 'down' },
  { id: 37, name: 'Minion Left', color: '#6a0dad', walkable: false, category: 'trainer', direction: 'left' },
  { id: 38, name: 'Minion Right', color: '#6a0dad', walkable: false, category: 'trainer', direction: 'right' },
  { id: 39, name: 'Rival', color: '#dc143c', walkable: false, category: 'trainer', direction: 'down' },
  { id: 40, name: 'Hidden Item', color: '#4682b4', walkable: true, category: 'entity' },
  { id: 41, name: 'PC', color: '#a9a9a9', walkable: false, category: 'interactive' },
  { id: 42, name: 'Pokeball', color: '#ff0000', walkable: true, category: 'entity' },
  { id: 43, name: 'Elite 4', color: '#c0c0c0', walkable: false, category: 'trainer', direction: 'down' },
  { id: 44, name: 'Champion', color: '#ffd700', walkable: false, category: 'trainer', direction: 'down' },
  { id: 45, name: 'Champion Alt', color: '#ffd700', walkable: false, category: 'trainer', direction: 'down' },
  { id: 46, name: 'Rival Final', color: '#dc143c', walkable: false, category: 'trainer', direction: 'down' },
  { id: 47, name: 'Prof Oak', color: '#8b4513', walkable: false, category: 'entity' },
  { id: 48, name: 'Mom', color: '#dda0dd', walkable: false, category: 'entity' },
  { id: 49, name: 'Statue', color: '#d4af37', walkable: false, category: 'interactive', isOverlay: true },
  { id: 50, name: 'Badge', color: '#00ced1', walkable: true, category: 'entity' },
]

export const TILES_BY_ID: Record<number, TileDefinition> = TILES.reduce<Record<number, TileDefinition>>(
  (tileById, tile) => {
    tileById[tile.id] = tile
    return tileById
  },
  {},
)
