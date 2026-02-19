# Trinity gftool Port Plan (DAE + DAE Clips)

## Scope

- New CLI project: `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`
- Legacy Switch-Toolbox reference: `D:\Projects\Switch-Toolbox`
- Proven Trinity implementation source: `D:\Projects\gftool\TrinityFileExplorer`, `D:\Projects\gftool\TrinityModelViewer`, `D:\Projects\gftool\TrinityModLoader`, `D:\Projects\gftool\TrinitySceneView`, `D:\Projects\gftool\TrinityUikitEditor`

Goal: port the **working extraction/link/decode path** used by gftool so CLI can export:

1) `model.dae`
2) separate clip DAEs in `clips/`
3) textures in `textures/`

## What gftool already solved

- TRPFS/TRPFD hash/path resolution and pack extraction via file hash mapping
- Oodle decompression integration
- Trinity dependency walk from model roots (TRMDL -> TRMSH/TRMBF/TRMTR/TRSKL)
- animation discovery from motion folders and GF animation decode for playback/export

Key source anchors:

- `D:\Projects\gftool\TrinityFileExplorer\TrinityExplorerWindow.cs`
- `D:\Projects\gftool\GFTool.Core\Compression\Oodle.cs`
- `D:\Projects\gftool\GFTool.Core\Math\Hash\GFFnv64a.cs`
- `D:\Projects\gftool\GFTool.Core\Cache\GFPakHashCache.cs`
- `D:\Projects\gftool\GFTool.Renderer\Scene\GraphicsObjects\Model.cs`
- `D:\Projects\gftool\GFTool.Renderer\Scene\GraphicsObjects\Armature.cs`
- `D:\Projects\gftool\GFTool.Renderer\Scene\GraphicsObjects\Animation.cs`

## Current blocker on real corpus

- Real run on `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\arc` produces textures, but still `0` DAE and `0` clips.
- Root issue: `.trmmt/.trmdt` container relationship and dependency resolution do not yet match gftool decode path strongly enough for full model/clip assembly.

## 3-Phase port sequence

### Phase 1 - Dependency parity with gftool (no renderer)

- Implement deterministic dependency graph builder matching gftool candidate-path/hash behavior.
- Consolidate hash implementation into one shared utility.
- Use path/hash cache parity for container refs (`.trmmt/.trmdt/.trmdl`) and enforce stable link ordering.

Target files:

- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Common\Fnv1a64.cs` (new shared hash utility)
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Models\Trinity\TrinityDependencyGraphBuilder.cs` (new)
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Models\Trinity\TrinityContainerReferenceResolver.cs`
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Archives\TRPAK\TrpakFormat.cs`

Acceptance:

- For real `.trmmt/.trmdt` samples, resolver consistently finds required `.trmbf/.trmtr/.trskl` edges with deterministic ordering.

### Phase 2 - Real static+skinned model decode to DAE

- Port declaration-driven TRMSH/TRMBF decode from gftool model path.
- Port TRSKL armature + inverse bind mapping.
- Extend DAE writer path to include controller/skin for valid bundles.

Target files:

- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Models\Trinity\TrinityStaticMeshDecoder.cs`
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Models\Trinity\TrinityArmatureBuilder.cs` (new)
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Dae\ColladaWriter.cs`
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Models\TrinityModelAssemblyService.cs`

Acceptance:

- Non-zero DAE output count on real corpus; at least one bundle transitions from `pending_conversion` to `completed`.

### Phase 3 - DAE animation clip export (separate clips)

- Port GF animation decode path used by gftool playback.
- Add clip exporter writing separate DAE clips linked by bundle key.
- Keep control/effect clips as raw sidecars where decode is not yet supported.

Target files:

- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Animations\TrinityAnimationDecoder.cs` (new)
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Animations\TrinityAnimationLocator.cs` (new)
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.Formats\Export\Animations\AnimationClipArchiveExportService.cs`
- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.App\Commands\ConvertCommand.cs`

Acceptance:

- Separate clip outputs present under `clips/` with DAE animation files for decodable `.tranm/.gfbanm`.

## Execution policy

- Use one agent at a time per phase.
- After each phase: run `dotnet build` and `dotnet test` on
  `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\PokemonGreen.SwitchToolboxCli.sln`
- Validate each phase on real corpus path:
  `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\arc`
