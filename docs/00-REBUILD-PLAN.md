# PokemonGreen Rebuild Plan

## Overview
Rebuild the Pokemon-style 2D tile-based RPG from scratch with clean architecture.

## Projects

### 1. PokemonGreen.Core (C# Class Library)
The shared game engine library.

**Maps/**
- `TileCategory.cs` - Enum: Terrain, Decoration, Interactive, Entity, Trainer, Encounter, Structure, Item
- `TileDefinition.cs` - Record: Id, Name, Walkable, Color, Category, OverlayBehavior
- `TileRegistry.cs` - Static registry of 104 tile definitions
- `TileMap.cs` - 2D grid with base/overlay layers, walkability checks
- `MapDefinition.cs` - Abstract base class for generated map classes
- `MapCatalog.cs` - Static registry for runtime map lookup

**Player/**
- `PlayerState.cs` - Enum: Idle, Walk, Run, Jump, Climb, Combat, Spellcast
- `Direction.cs` - Enum: Up, Down, Left, Right
- `Player.cs` - Position, state machine, facing direction, collision

**Systems/**
- `Camera.cs` - Auto-scaling viewport, follows player, clamps to map bounds
- `InputManager.cs` - Keyboard polling (WASD/arrows, shift=run, space=jump)
- `SoundManager.cs` - Singleton for BGM and SFX playback

**Core/**
- `GameWorld.cs` - Orchestrates map, player, camera, input each frame

### 2. PokemonGreen (MonoGame Executable)
The game application.

- `Program.cs` - Entry point
- `Game1.cs` - MonoGame Game class, init/load/update/draw
- `TextureStore.cs` - Centralized texture loading with fallbacks
- `PlayerRenderer.cs` - Sprite sheet animation based on state/direction
- `TileRenderer.cs` - Renders tile layers

### 3. PokemonGreen.MapEditor (React/TS/Vite)
Visual map editor.

**Store/**
- `editorStore.ts` - Zustand store with slices
- `mapSlice.ts` - Grid state, dimensions, name
- `paletteSlice.ts` - Selected tile, category, building
- `historySlice.ts` - Undo/redo stacks

**Components/**
- `Canvas.tsx` - Grid rendering, mouse painting
- `TilePalette.tsx` - Tile selection by category
- `Toolbar.tsx` - Map controls, import/export
- `Sidebar.tsx` - Properties panel

**Services/**
- `mapIO.ts` - Import/export .map.json
- `tileRegistry.ts` - Mirror of C# tile definitions

**Types/**
- `editor.ts` - TypeScript types for editor state
- `map.ts` - Map JSON schema types

### 4. PokemonGreen.MapGen (C# CLI) - Later
Code generation tool (build after core + editor working).

## Execution Plan

**Phase 1 - Core Foundation (Parallel)**
- Agent A: TileRegistry, TileDefinition, TileCategory (C#)
- Agent B: TileMap, MapDefinition, MapCatalog (C#)
- Agent C: Player state machine (C#)
- Agent D: Camera, InputManager, SoundManager (C#)

**Phase 2 - Integration**
- GameWorld orchestrator
- Game1 wiring

**Phase 3 - Map Editor (Parallel)**
- Agent A: Zustand store architecture
- Agent B: Canvas and tile rendering
- Agent C: Tile palette and UI
- Agent D: Map I/O services

**Phase 4 - Polish**
- Wire everything together
- Test full pipeline
