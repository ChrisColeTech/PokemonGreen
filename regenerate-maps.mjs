/**
 * Temporary script to add tile legend comments to existing .g.cs files.
 * Parses each file's BaseTileData and OverlayTileData arrays to find used tiles,
 * then looks up names from TileRegistry.cs.
 */
import { readFileSync, writeFileSync, readdirSync } from 'fs';
import { join } from 'path';

// --- Parse TileRegistry.cs for tile ID -> name/category mappings ---

const REGISTRY_CS_PATH = 'D:/Projects/PokemonGreen/src/PokemonGreen.Core/Maps/TileRegistry.cs';
const registryCs = readFileSync(REGISTRY_CS_PATH, 'utf-8');

const tilesById = new Map();

// Match lines like: [0] = new TileDefinition(0, "Water", false, "#3890f8", TileCategory.Terrain),
const tileRegex = /\[(\d+)\]\s*=\s*new\s+TileDefinition\(\d+,\s*"([^"]+)",\s*(?:true|false),\s*"[^"]+",\s*TileCategory\.(\w+)/g;
let match;
while ((match = tileRegex.exec(registryCs)) !== null) {
  const id = parseInt(match[1], 10);
  const name = match[2];
  const category = match[3];
  tilesById.set(id, { id, name, category });
}

console.log(`Loaded ${tilesById.size} tile definitions from TileRegistry.cs`);

// --- Process each .g.cs file ---

const GENERATED_DIR = 'D:/Projects/PokemonGreen/src/PokemonGreen.Core/Maps/Generated';
const files = readdirSync(GENERATED_DIR).filter(f => f.endsWith('.g.cs'));

for (const file of files) {
  const filePath = join(GENERATED_DIR, file);
  const content = readFileSync(filePath, 'utf-8');

  // Extract all integer values from BaseTileData array
  const baseMatch = content.match(/BaseTileData\s*=\s*\[([\s\S]*?)\];/);
  const overlayMatch = content.match(/OverlayTileData\s*=\s*\[([\s\S]*?)\];/);

  const usedIds = new Set();

  if (baseMatch) {
    const numbers = baseMatch[1].match(/\d+/g);
    if (numbers) {
      for (const n of numbers) {
        usedIds.add(parseInt(n, 10));
      }
    }
  }

  if (overlayMatch) {
    // Only extract actual numbers, not "null"
    const numbers = overlayMatch[1].match(/\d+/g);
    if (numbers) {
      for (const n of numbers) {
        usedIds.add(parseInt(n, 10));
      }
    }
  }

  if (usedIds.size === 0) {
    console.log(`Skipping ${file}: no tile IDs found`);
    continue;
  }

  // Build legend
  const sorted = [...usedIds].sort((a, b) => a - b);
  const maxIdWidth = Math.max(...sorted.map(id => String(id).length));

  const legendLines = ['// Tile Legend:'];
  for (const id of sorted) {
    const tile = tilesById.get(id);
    const name = tile ? tile.name : 'Unknown';
    const cat = tile ? tile.category : 'Unknown';
    const idStr = String(id).padStart(maxIdWidth);
    legendLines.push(`//   ${idStr} = ${name} (${cat})`);
  }

  const legend = legendLines.join('\n');

  // Remove any existing legend (between #nullable enable and namespace)
  // Then insert the new legend
  let newContent;
  const hasExistingLegend = content.match(/^#nullable enable\n([\s\S]*?)namespace /m);

  if (hasExistingLegend) {
    // Replace everything between #nullable enable and namespace line
    newContent = content.replace(
      /^(#nullable enable)\n[\s\S]*?(namespace )/m,
      `$1\n\n${legend}\nnamespace `
    );
  } else {
    // Insert before namespace
    newContent = content.replace(
      /^(namespace )/m,
      `${legend}\n$1`
    );
  }

  writeFileSync(filePath, newContent, 'utf-8');
  console.log(`Updated: ${file} (${usedIds.size} unique tiles)`);
}

console.log('Done.');
