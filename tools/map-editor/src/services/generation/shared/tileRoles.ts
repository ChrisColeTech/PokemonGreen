import type { GenerationContext } from '../../../types/generation'
import { DEFAULT_ENCOUNTER_TILE_IDS, TILE_ID_PRIMARY_PATH } from './tileConstants'

export const getBaseTerrainTileId = (context: GenerationContext): number =>
  context.archetype.tileRoles.baseTerrainTileId ?? context.config.baseFillTileId

export const getPrimaryPathTileId = (context: GenerationContext): number =>
  context.archetype.tileRoles.primaryPathTileId ?? TILE_ID_PRIMARY_PATH

export const getEncounterTileOptions = (context: GenerationContext): readonly number[] =>
  context.archetype.tileRoles.optionalEncounterTileIds.length > 0
    ? context.archetype.tileRoles.optionalEncounterTileIds
    : DEFAULT_ENCOUNTER_TILE_IDS
