import type { EditorTileRegistry, EditorTileDefinition } from '../types/editor'

// --- C# class template (from MapClass.tpl) ---

const MAP_CLASS_TEMPLATE = `#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class {{CLASS_NAME}} : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
{{BASE_TILE_DATA}}
    ];

    private static readonly int?[] OverlayTileData =
    [
{{OVERLAY_TILE_DATA}}
    ];

    private static readonly int[] WalkableTileIds =
    [
{{WALKABLE_TILE_IDS}}
    ];

    public static {{CLASS_NAME}} Instance { get; } = new();

    private {{CLASS_NAME}}()
        : base("{{MAP_ID}}", "{{DISPLAY_NAME}}", {{WIDTH}}, {{HEIGHT}}, {{TILE_SIZE}}, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}
`

// --- Helpers ---

const DEFAULT_BASE_TILE_ID = 1 // Grass

export function toPascalCase(input: string): string {
  if (!input) return input
  let result = ''
  let capitalizeNext = true
  for (const c of input) {
    if (c === '_' || c === '-' || c === ' ') {
      capitalizeNext = true
    } else if (capitalizeNext) {
      result += c.toUpperCase()
      capitalizeNext = false
    } else {
      result += c
    }
  }
  return result
}

function escapeString(input: string): string {
  if (!input) return input
  return input
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t')
}

function isTerrainTile(tileId: number, tilesById: Map<number, EditorTileDefinition>): boolean {
  const tile = tilesById.get(tileId)
  if (!tile) return true
  return tile.category === 'terrain'
}

// --- Array formatters (matching C# CodeGenerator exactly) ---

function formatBaseTiles(
  mapData: number[][],
  w: number,
  h: number,
  tilesById: Map<number, EditorTileDefinition>,
): string {
  const parts: string[] = []
  for (let y = 0; y < h; y++) {
    let row = '        '
    for (let x = 0; x < w; x++) {
      let tileId = mapData[y]?.[x] ?? 0
      if (!isTerrainTile(tileId, tilesById)) {
        tileId = DEFAULT_BASE_TILE_ID
      }
      row += String(tileId)
      if (x < w - 1 || y < h - 1) row += ', '
    }
    parts.push(row)
  }
  return parts.join('\n')
}

function formatOverlayTiles(
  mapData: number[][],
  w: number,
  h: number,
  tilesById: Map<number, EditorTileDefinition>,
): string {
  const parts: string[] = []
  for (let y = 0; y < h; y++) {
    let row = '        '
    for (let x = 0; x < w; x++) {
      const tileId = mapData[y]?.[x] ?? 0
      const hasOverlay = !isTerrainTile(tileId, tilesById)
      row += hasOverlay ? String(tileId).padStart(4) : 'null'
      if (x < w - 1 || y < h - 1) row += ', '
    }
    parts.push(row)
  }
  return parts.join('\n')
}

function formatWalkableIds(ids: number[]): string {
  if (ids.length === 0) return ''
  return '        ' + ids.join(', ')
}

// --- Main code generator ---

export function generateMapClass(
  mapData: number[][],
  mapWidth: number,
  mapHeight: number,
  mapName: string,
  cellSize: number,
  tilesById: Map<number, EditorTileDefinition>,
): string {
  const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
  const className = toPascalCase(mapId) || 'UntitledMap'

  // Collect all unique tile IDs and determine which are walkable
  const usedIds = new Set<number>()
  for (let y = 0; y < mapHeight; y++) {
    for (let x = 0; x < mapWidth; x++) {
      usedIds.add(mapData[y]?.[x] ?? 0)
    }
  }

  const walkableIds = [...usedIds]
    .filter(id => tilesById.get(id)?.walkable === true)
    .sort((a, b) => a - b)

  return MAP_CLASS_TEMPLATE
    .replace(/\{\{CLASS_NAME\}\}/g, className)
    .replace('{{MAP_ID}}', escapeString(mapId))
    .replace('{{DISPLAY_NAME}}', escapeString(mapName))
    .replace('{{WIDTH}}', String(mapWidth))
    .replace('{{HEIGHT}}', String(mapHeight))
    .replace('{{TILE_SIZE}}', String(cellSize))
    .replace('{{BASE_TILE_DATA}}', formatBaseTiles(mapData, mapWidth, mapHeight, tilesById))
    .replace('{{OVERLAY_TILE_DATA}}', formatOverlayTiles(mapData, mapWidth, mapHeight, tilesById))
    .replace('{{WALKABLE_TILE_IDS}}', formatWalkableIds(walkableIds))
}

// --- Registry export ---

export function exportRegistryJson(registry: EditorTileRegistry): string {
  const output = {
    id: registry.id,
    name: registry.name,
    version: registry.version,
    categories: registry.categories.map(c => ({
      id: c.id,
      label: c.label,
      showInPalette: c.showInPalette,
    })),
    tiles: registry.tiles
      .slice()
      .sort((a, b) => a.id - b.id)
      .map(t => {
        const entry: Record<string, unknown> = {
          id: t.id,
          name: t.name,
          walkable: t.walkable,
          color: t.color,
          category: t.category,
        }
        if (t.encounter) entry.encounter = t.encounter
        return entry
      }),
    buildings: registry.buildings,
  }
  return JSON.stringify(output, null, 2)
}
