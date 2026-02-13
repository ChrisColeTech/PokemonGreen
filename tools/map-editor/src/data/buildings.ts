import type { BuildingDefinition, BuildingId, BuildingRotation, BuildingStampMatrix } from '../types/editor'

export interface RotatedBuildingPrefab {
  width: number
  height: number
  tiles: BuildingStampMatrix
}

const cloneStampMatrix = (tiles: BuildingStampMatrix): BuildingStampMatrix => tiles.map((row) => [...row])

const rotateStampMatrix = (tiles: BuildingStampMatrix): BuildingStampMatrix => {
  const sourceHeight = tiles.length
  const sourceWidth = tiles[0]?.length ?? 0
  const nextTiles: BuildingStampMatrix = Array.from({ length: sourceWidth }, () =>
    Array.from({ length: sourceHeight }, () => null),
  )

  for (let y = 0; y < sourceHeight; y += 1) {
    for (let x = 0; x < sourceWidth; x += 1) {
      nextTiles[x][sourceHeight - 1 - y] = tiles[y][x]
    }
  }

  return nextTiles
}

const buildRotatedPrefabs = (baseTiles: BuildingStampMatrix): Record<BuildingRotation, RotatedBuildingPrefab> => {
  const rotation0Tiles = cloneStampMatrix(baseTiles)
  const rotation1Tiles = rotateStampMatrix(rotation0Tiles)
  const rotation2Tiles = rotateStampMatrix(rotation1Tiles)
  const rotation3Tiles = rotateStampMatrix(rotation2Tiles)

  const toPrefab = (tiles: BuildingStampMatrix): RotatedBuildingPrefab => ({
    width: tiles[0]?.length ?? 0,
    height: tiles.length,
    tiles,
  })

  return {
    0: toPrefab(rotation0Tiles),
    1: toPrefab(rotation1Tiles),
    2: toPrefab(rotation2Tiles),
    3: toPrefab(rotation3Tiles),
  }
}

const defineBuilding = (id: BuildingId, name: string, tiles: BuildingStampMatrix): BuildingDefinition => ({
  id,
  name,
  width: tiles[0]?.length ?? 0,
  height: tiles.length,
  tiles,
})

export const BUILDINGS: BuildingDefinition[] = [
  defineBuilding('pokecenter', 'Pokecenter', [
      [3, 3, 3, 3],
      [3, 4, 4, 3],
      [3, 4, 4, 3],
      [6, 4, 4, 6],
    ]),
  defineBuilding('pokemart', 'Pokemart', [
      [3, 3, 3, 3],
      [3, 6, 6, 3],
      [3, 11, 6, 3],
      [6, 4, 4, 6],
    ]),
  defineBuilding('gym', 'Gym', [
      [3, 3, 3, 3, 3],
      [3, 6, 6, 6, 3],
      [3, 6, 12, 6, 3],
      [3, 6, 4, 6, 3],
      [6, 6, 4, 6, 6],
    ]),
  defineBuilding('house-small', 'House Small', [
      [3, 3, 3],
      [3, 4, 3],
      [6, 4, 6],
    ]),
  defineBuilding('house-large', 'House Large', [
      [3, 3, 3, 3],
      [3, 6, 6, 3],
      [3, 4, 6, 3],
      [6, 4, 6, 6],
    ]),
  defineBuilding('lab', 'Lab', [
      [3, 3, 3, 3, 3],
      [3, 6, 6, 6, 3],
      [3, 4, 41, 4, 3],
      [6, 4, 4, 4, 6],
    ]),
  defineBuilding('cave-entrance', 'Cave Entrance', [
      [3, 3, 3],
      [15, 15, 15],
    ]),
  defineBuilding('gate', 'Gate', [
      [6, 6, 6, 6],
      [6, 16, 16, 6],
      [6, 6, 6, 6],
    ]),
  defineBuilding('pond', 'Pond', [
      [17, 0, 0, 17],
      [0, 0, 0, 0],
      [17, 0, 0, 17],
    ]),
  defineBuilding('fence-h', 'Fence H', [[18, 18, 18, 18]]),
  defineBuilding('fence-v', 'Fence V', [[18], [18], [18], [18]]),
]

export const BUILDINGS_BY_ID: Record<BuildingId, BuildingDefinition> = BUILDINGS.reduce<Record<BuildingId, BuildingDefinition>>(
  (buildingById, building) => {
    buildingById[building.id] = building
    return buildingById
  },
  {} as Record<BuildingId, BuildingDefinition>,
)

export const BUILDING_PREFAB_STAMPS_BY_ID: Record<BuildingId, Record<BuildingRotation, RotatedBuildingPrefab>> =
  BUILDINGS.reduce<Record<BuildingId, Record<BuildingRotation, RotatedBuildingPrefab>>>((prefabById, building) => {
    prefabById[building.id] = buildRotatedPrefabs(building.tiles)
    return prefabById
  }, {} as Record<BuildingId, Record<BuildingRotation, RotatedBuildingPrefab>>)
