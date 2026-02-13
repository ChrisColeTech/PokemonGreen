import { createGrid } from '../../gridService'
import { getBaseTerrainTileId } from '../shared/tileRoles'
import { applyGenerationTemplatePrepass } from '../templatePrepassService'
import type { GenerationPass } from '../../../types/generation'

export const initializePass: GenerationPass = {
  id: 'initialize',
  run: (context) => {
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const baseTileId = getBaseTerrainTileId(context)

    context.grid = createGrid(width, height, baseTileId)
    context.buildingPlacements = []
    context.state.primaryPathCells = []
    context.state.reservedDistricts = []
    context.state.encounterAnchorCells = []
    return applyGenerationTemplatePrepass(context)
  },
}
