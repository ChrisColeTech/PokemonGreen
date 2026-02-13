import { clamp } from '../shared/math'
import { getBaseTerrainTileId, getPrimaryPathTileId } from '../shared/tileRoles'
import { fillRect, findPrimaryPathY } from './passHelpers'
import type { GenerationPass } from '../../../types/generation'

export const reserveDistrictsPass: GenerationPass = {
  id: 'reserveDistricts',
  run: (context) => {
    const rng = context.rng.fork('reserveDistricts')
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const townTemplate = context.state.templateHints?.town
    const baseTileId = getBaseTerrainTileId(context)
    const pathTileId = getPrimaryPathTileId(context)
    const townWidth = clamp(Math.floor(width * (townTemplate?.widthRatio ?? 0.3)), 8, 14)
    const townHeight = clamp(Math.floor(height * (townTemplate?.heightRatio ?? 0.5)), 7, 12)
    const anchorXRatio = townTemplate?.anchorXRatio ?? 0.18
    const anchorX = clamp(Math.floor(width * anchorXRatio) + rng.int(-2, 2), 1, width - townWidth - 1)
    const pathYAtTown = findPrimaryPathY(context.state.primaryPathCells, Math.floor(height / 2), anchorX)
    const anchorYFromPath = pathYAtTown - Math.floor(townHeight / 2)
    const anchorYRatio = townTemplate?.anchorYRatio
    const anchorYFromRatio = anchorYRatio !== undefined
      ? Math.floor(height * anchorYRatio) - Math.floor(townHeight / 2)
      : anchorYFromPath
    const anchorY = clamp(Math.round((anchorYFromPath + anchorYFromRatio) / 2), 1, height - townHeight - 1)

    fillRect(context.grid, anchorX, anchorY, townWidth, townHeight, baseTileId)

    const townRoadY = clamp(pathYAtTown, anchorY + 1, anchorY + townHeight - 2)
    const townRoadX = clamp(anchorX + Math.floor(townWidth / 2), anchorX + 1, anchorX + townWidth - 2)

    for (let x = anchorX; x < anchorX + townWidth; x += 1) {
      context.grid[townRoadY][x] = pathTileId
    }
    for (let y = anchorY; y < anchorY + townHeight; y += 1) {
      context.grid[y][townRoadX] = pathTileId
    }

    context.state.reservedDistricts = [
      {
        id: 'town',
        x: anchorX,
        y: anchorY,
        width: townWidth,
        height: townHeight,
      },
      {
        id: 'route_east',
        x: anchorX + townWidth,
        y: 1,
        width: width - (anchorX + townWidth) - 1,
        height: height - 2,
      },
    ]

    return context
  },
}
