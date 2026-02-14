# 01 - Dynamic Tile Registry System

## Overview

A system that allows the map editor to import, manage, and switch between tile registries. The UI dynamically updates based on the selected registry, enabling flexible map editing for different tile sets and game configurations.

## Goals

1. **Single Source of Truth** - Tile definitions maintained in one place (Core's `TileRegistry.cs`)
2. **Dynamic UI** - Editor buttons, palettes, and options update based on loaded registry
3. **Configurable Building Sizes** - Building dimensions defined in registry, not hardcoded
4. **Distinct Color Service** - Clear, non-confusing tile colors in the grid
5. **Extensible Design** - Well-designed enough to potentially serve as a standalone tool

---

## Architecture

### Data Flow

```
PokemonGreen.Core/Maps/TileRegistry.cs
              ↓ (export)
        tiles.json (or .ts generated)
              ↓ (import)
tools/map-editor ← Registry loaded, UI rebuilds
```

### Components

#### 1. Tile Registry Schema

```typescript
interface TileDefinition {
  id: number
  name: string
  category: TileCategory
  visualKind: string
  walkable: boolean
  color: { r: number; g: number; b: number }
  overlayKind?: string
}

type TileCategory = 
  | 'terrain'
  | 'decoration'
  | 'interactive'
  | 'entity'
  | 'trainer'
  | 'encounter'
  | 'structure'
  | 'item'

interface TileRegistry {
  version: string
  name: string
  tiles: TileDefinition[]
  categories: CategoryConfig[]
  buildings: BuildingConfig[]
}
```

#### 2. Building Configuration

```typescript
interface BuildingConfig {
  id: string
  name: string
  size: { width: number; height: number }
  footprint: number[][]
  doorPosition: { x: number; y: number }
  requiredTiles: number[]
}
```

#### 3. Category Configuration

```typescript
interface CategoryConfig {
  id: TileCategory
  displayName: string
  color: string // UI accent color
  icon?: string
  tilesPerPage?: number
  showInPalette: boolean
}
```

---

## Map Editor Changes

### 1. Registry Store (Zustand)

```typescript
// store/slices/registrySlice.ts
interface RegistryState {
  registries: Map<string, TileRegistry>
  activeRegistryId: string | null
  isLoading: boolean
  error: string | null
  
  // Actions
  loadRegistry: (file: File) => Promise<void>
  loadRegistryFromUrl: (url: string) => Promise<void>
  setActiveRegistry: (id: string) => void
  unloadRegistry: (id: string) => void
}
```

### 2. Dynamic Tile Palette

- **Current:** Hardcoded tile buttons
- **Future:** Rendered from active registry's categories and tiles

```tsx
// Dynamic palette based on registry
function TilePalette() {
  const { activeRegistry } = useRegistryStore()
  const [selectedCategory, setSelectedCategory] = useState<TileCategory>('terrain')
  
  const tiles = activeRegistry?.tiles.filter(t => t.category === selectedCategory) ?? []
  const categories = activeRegistry?.categories ?? []
  
  return (
    <div>
      <CategoryTabs categories={categories} onSelect={setSelectedCategory} />
      <TileGrid tiles={tiles} />
    </div>
  )
}
```

### 3. Building Tool Updates

- **Current:** Hardcoded building sizes
- **Future:** Loaded from registry's `buildings` config

```tsx
function BuildingTool() {
  const { activeRegistry } = useRegistryStore()
  const buildings = activeRegistry?.buildings ?? []
  
  return (
    <div>
      {buildings.map(building => (
        <BuildingButton
          key={building.id}
          building={building}
          onClick={() => placeBuilding(building)}
        />
      ))}
    </div>
  )
}
```

### 4. Color Service Integration

The color service should use registry-defined colors instead of hardcoded values:

```typescript
// services/tileColorService.ts
export function getTileColor(tileId: number, registry: TileRegistry): string {
  const tile = registry.tiles.find(t => t.id === tileId)
  if (!tile) return '#ff00ff' // fallback
  
  // Use registry color with category-based modulation for distinction
  return `rgb(${tile.color.r}, ${tile.color.g}, ${tile.color.b})`
}

export function getCategoryAccentColor(category: TileCategory, registry: TileRegistry): string {
  const config = registry.categories.find(c => c.id === category)
  return config?.color ?? '#888888'
}
```

---

## Export Command (Core)

Add to MapGen:

```bash
dotnet run -- export-registry --output tools/map-editor/src/data/registries/default.json
```

Output format matches the TypeScript schema above.

---

## UI Mockup

```
┌─────────────────────────────────────────────────────────────┐
│  Map Editor                              [Registry: Default]│
├─────────────────────────────────────────────────────────────┤
│ ┌─────────┐ ┌─────────────────────────────────────────────┐ │
│ │Registry │ │                                             │ │
│ │─────────│ │                                             │ │
│ │ Default │ │              Map Canvas                      │ │
│ │ Custom1 │ │                                             │ │
│ │ [+]     │ │                                             │ │
│ └─────────┘ └─────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ [Terrain] [Decoration] [Interactive] [Entity] [Item]    │ │
│ │─────────────────────────────────────────────────────────│ │
│ │ 🟩 Grass  🟦 Water  🟫 Path  🟨 Sand                    │ │
│ │ 🌲 Tree   🪨 Rock   🌸 Flower                          │ │
│ │                                                          │ │
│ │ Buildings: [House 3x3] [PokeCenter 4x3] [Gym 5x5]      │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Phases

### Phase 1: Registry Export
- [ ] Add `export-registry` command to MapGen
- [ ] Generate JSON from Core's TileRegistry
- [ ] Include building configurations

### Phase 2: Registry Import (Editor)
- [ ] Create registry store (Zustand)
- [ ] Implement file import UI
- [ ] Parse and validate registry JSON

### Phase 3: Dynamic UI
- [ ] Convert tile palette to registry-driven
- [ ] Convert building tool to registry-driven
- [ ] Update category tabs based on registry

### Phase 4: Color Service
- [ ] Wire tile color service to registry
- [ ] Add category accent colors
- [ ] Ensure color distinction in grid

### Phase 5: Polish
- [ ] Registry switcher dropdown
- [ ] Import/export registry UI
- [ ] Validation and error handling

---

## File Structure

```
tools/map-editor/
├── src/
│   ├── data/
│   │   └── registries/
│   │       └── default.json      # Exported from Core
│   ├── store/
│   │   └── slices/
│   │       └── registrySlice.ts  # New
│   ├── services/
│   │   └── registryService.ts    # New
│   └── components/
│       └── RegistrySelector.tsx  # New

src/PokemonGreen.MapGen/
└── Services/
    └── RegistryExporter.cs       # New
```

---

## Questions to Resolve

1. **Multiple registries at once?** - Should user be able to load multiple and switch, or one at a time?
2. **Registry persistence?** - Store in localStorage, or require import each session?
3. **Custom tile additions?** - Allow users to add tiles within editor, or enforce registry-only?
4. **Validation strictness?** - What happens if map uses tiles not in loaded registry?

---

## Success Criteria

- [ ] Can export registry from Core
- [ ] Can import registry in editor
- [ ] Tile palette updates based on registry categories
- [ ] Building sizes come from registry config
- [ ] Grid colors are clear and distinguishable
- [ ] Adding a tile in Core propagates to editor after export/import
