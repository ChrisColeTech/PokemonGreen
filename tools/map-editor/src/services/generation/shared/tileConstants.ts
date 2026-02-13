export const TILE_ID_PRIMARY_PATH = 2
export const TILE_ID_DOOR = 4
export const TILE_ID_TALL_GRASS = 7
export const TILE_ID_SIGN = 9
export const TILE_ID_NPC = 10
export const TILE_ID_TREE = 3
export const TILE_ID_FLOWER = 19
export const TILE_ID_RARE_GRASS = 28
export const TILE_ID_HIDDEN_ITEM = 40
export const TILE_ID_ITEM = 42

export const TRAINER_TILE_IDS = [20, 21, 22, 23] as const
export const DEFAULT_ENCOUNTER_TILE_IDS = [TILE_ID_TALL_GRASS, TILE_ID_RARE_GRASS] as const
export const TOWN_PATH_CLUTTER_TILE_IDS = new Set<number>([
  TILE_ID_TREE,
  TILE_ID_TALL_GRASS,
  TILE_ID_SIGN,
  TILE_ID_NPC,
  TILE_ID_FLOWER,
  ...TRAINER_TILE_IDS,
  TILE_ID_RARE_GRASS,
  TILE_ID_HIDDEN_ITEM,
  TILE_ID_ITEM,
])
