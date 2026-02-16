# 21-3D-OPEN-WORLD-EXPLORATION-HANDOFF

## Summary

This document covers the proof-of-concept work for a 3D open world exploration mode using MonoGame. The goal was to demonstrate that MonoGame can support 3D rendering with third-person camera controls and runtime model loading.

---

## 1. What We Accomplished

### Core Infrastructure Added
- **`PokemonGreen.Core/Rendering/DaeModel.cs`** - Runtime COLLADA (.dae) model loader that bypasses MonoGame's content pipeline
  - Parses XML directly
  - Extracts positions, normals, UV coordinates
  - Builds VertexBuffer/IndexBuffer at runtime
  - Loads textures from PNG files

- **`PokemonGreen.Core/Rendering/Camera3D.cs`** - Third-person camera class
  - Follows a target position
  - Yaw/pitch rotation with clamping
  - Configurable distance and zoom limits
  - Provides View/Projection matrices

### 3D Project Created
- **`PokemonGreen.3D/`** - New MonoGame DesktopGL project
- References `PokemonGreen.Core` for shared rendering classes
- Successfully loads and renders trainer model with textures
- Basic WASD movement and camera controls

### Models Extracted
- Extracted 50+ field character models from Sun/Moon ROM using OhanaCli
- Located at: `PokemonGreen.Tests/exports-field-models/0/`
- Includes trainer NPCs, player characters with textures

---

## 2. What Work Remains

### Critical Issues
1. **Camera/Character Sync** - Third-person camera does not properly follow behind character during turns. Current implementation has camera and character rotation fighting each other.

2. **T-Pose Models** - All loaded models are in T-pose. No skeletal animation support.

3. **No Animation System** - Need runtime bone/morph target animation for:
   - Idle animations
   - Walk/run cycles
   - Camera-relative turn animations

### Missing Features
- Map/terrain loading (procedural or from game data)
- Collision detection
- NPC placement and interaction
- Pokemon spawning in 3D space
- Day/night cycle integration
- Mini-map / UI overlay

### Known Bugs
- Controls can feel unresponsive at times
- Model scale is hardcoded (0.015f) - needs proper bounding box calculation
- No ground collision - player can walk through floor

---

## 3. Optimizations - Prime Suspects

### 1. Frustum Culling
Currently drawing everything. Should cull objects outside camera view.
```csharp
// Add BoundingFrustum check before drawing
var frustum = new BoundingFrustum(_effect.View * _effect.Projection);
if (frustum.Contains(mesh.BoundingSphere) != ContainmentType.Disjoint)
    Draw(mesh);
```

### 2. Batched Rendering
Each model uses separate draw calls. For multiple NPCs:
- Combine static geometry into single vertex buffer
- Use instanced rendering for repeated objects (trees, rocks)

### 3. LOD (Level of Detail)
Terrain should have multiple resolution levels based on distance from camera.

### 4. Texture Atlasing
Multiple small textures cause state changes. Combine into atlas.

---

## 4. Step-by-Step Approach to Fix

### Phase 1: Fix Camera (Priority: HIGH)
1. Simplify - remove all camera smoothing/interpolation
2. Camera yaw = character facing direction
3. Q/E rotates BOTH camera AND character together
4. WASD moves character forward/back/strafe relative to facing
5. Test: Character should always face away from camera

### Phase 2: Add Ground Plane
1. Create simple flat grid mesh
2. Add basic collision (keep player at Y=0)
3. Expand to heightmap terrain later

### Phase 3: Animation Research
1. Research DAE skeletal animation format
2. Extract bone hierarchy and keyframes
3. Implement bone matrix interpolation
4. Start with simple idle animation

### Phase 4: World Loading
1. Design JSON map format (positions, rotations, model refs)
2. Create simple map loader
3. Spawn multiple objects

---

## 5. How to Start/Test

### Build and Run
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.3D
dotnet run
```

### Controls
| Key | Action |
|-----|--------|
| W/Up | Move forward |
| S/Down | Move backward |
| A/Left | Strafe left |
| D/Right | Strafe right |
| Q | Rotate camera left |
| E | Rotate camera right |
| R | Tilt camera up |
| F | Tilt camera down |
| PageUp | Zoom in |
| PageDown | Zoom out |
| Shift | Run (2x speed) |
| Esc | Exit |

### Loading Different Models
Edit `Game1.cs` line ~49:
```csharp
var dir = "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/exports-field-models/0/XXXX_modelname";
```

---

## 6. Issues and Resolution Strategies

### Issue 1: Camera/Character Desync
**Problem**: When moving, character and camera rotation fight each other, causing spinning or frozen movement.

**Strategies:**
1. **Lock-step approach**: Camera yaw IS the character facing. Q/E rotates both simultaneously. No separate character rotation.
2. **Tank controls**: Character rotates in place with A/D, W/S moves forward/back. Camera follows behind.
3. **Dual-stick style**: Camera free-orbits with mouse, character snaps to face camera direction when moving.
4. **Separate states**: Add a "camera mode" where Q/E moves camera only, but locks when moving.

### Issue 2: T-Pose / No Animation
**Problem**: Models load in bind pose, no runtime animation.

**Strategies:**
1. **Pose blending**: Export key poses as separate models, blend vertex positions at runtime.
2. **Skeletal parsing**: Parse DAE `<library_controllers>` and `<library_animations>` for bone data.
3. **Use glTF instead**: Convert DAE to glTF, use glTF runtime loader library.
4. **Procedural animation**: Programmatically animate limbs without skeleton (simple IK).

### Issue 3: Map Geometry Not Found
**Problem**: Sun/Moon map files (a/0/8/5, a/0/8/6) return 0 bytes or parsing errors.

**Strategies:**
1. **Different tool**: Try SPICA or Ohana3DS GUI directly, not CLI.
2. **Raw vertex export**: Write custom exporter to dump raw vertex data.
3. **Recreate maps**: Build maps from scratch using Blender, export as DAE.
4. **Use tile-based 3D**: Extrude existing 2D tile maps into 3D geometry.

### Issue 4: MonoGame Content Pipeline Limitations
**Problem**: Built-in importers don't handle our exported DAEs correctly.

**Strategies:**
1. **Runtime loading only**: Skip content pipeline entirely (current approach).
2. **Custom processor**: Write MonoGame content processor for our DAE format.
3. **Pre-convert to XNB**: Use external tool to generate XNB files.
4. **Use different format**: Convert to FBX with FBX SDK, try MonoGame's FBX importer.

---

## 7. Architecture and Features

### Current Architecture
```
PokemonGreen.3D (MonoGame DesktopGL)
    └── Game1.cs - Main game loop, rendering, input
    └── References Core

PokemonGreen.Core
    └── Rendering/
        ├── DaeModel.cs - COLLADA loader
        └── Camera3D.cs - Third-person camera
```

### Proposed Architecture
```
PokemonGreen.3D
    └── Game1.cs - Slim entry point, scene management
    └── Scenes/
        └── ExplorationScene.cs - Overworld logic
        └── BattleScene.cs - 3D battles
    └── Entities/
        └── Player.cs - Player state, movement
        └── NPC.cs - NPC behavior
        └── Pokemon.cs - Wild/companion Pokemon
    └── World/
        └── Terrain.cs - Heightmap/flat ground
        └── MapLoader.cs - JSON map loading
        └── ChunkManager.cs - Stream large worlds

PokemonGreen.Core
    └── Rendering/
        ├── DaeModel.cs
        ├── Camera3D.cs
        ├── SkeletalAnimation.cs (NEW)
        └── Material.cs (NEW)
    └── World/
        └── Transform.cs - Position, rotation, scale
        └── BoundingVolume.cs - Collision helper
```

### Quick Wins
1. **Simple ground plane** - 10 lines of code, makes movement feel grounded
2. **Model bounding box** - Auto-calculate scale instead of hardcoded 0.015f
3. **Basic shadows** - Render depth to texture, project onto ground
4. **Multiple models** - Load 2-3 NPCs, test batch rendering

---

## 8. Files Modified/Created

### New Files
- `PokemonGreen.Core/Rendering/DaeModel.cs`
- `PokemonGreen.Core/Rendering/Camera3D.cs`
- `PokemonGreen.3D/Game1.cs`
- `PokemonGreen.3D/PokemonGreen.3D.csproj`
- `PokemonGreen.3D/Content/Content.mgcb`

### Modified Files
- `PokemonGreen.Core/PokemonGreen.Core.csproj` - Added MonoGame reference

### Extracted Assets
- `PokemonGreen.Tests/exports-field-models/0/` - 50 field character models

---

## 9. Dependencies

- MonoGame.Framework.DesktopGL 3.8.*
- MonoGame.Content.Builder.Task 3.8.*
- PokemonGreen.Core (project reference)

---

## 10. Next Session Checklist

- [ ] Decide on camera control style (lock-step vs tank vs dual-stick)
- [ ] Implement chosen camera style cleanly
- [ ] Add flat ground plane with grid texture
- [ ] Research DAE skeletal animation format
- [ ] Test loading multiple models simultaneously
- [ ] Profile performance with 10+ models
