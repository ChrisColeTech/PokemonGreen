# BC6H Decode Session & SwitchToolboxMiniCli Handoff

**Date**: 2026-02-19
**Status**: BC6H bridge verified working — needs integration with real data
**Project**: `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`

---

## 1. What We Accomplished

### BC6H DirectXTex Bridge (C#)
- Created `Bc6hBridge.cs` in `SwitchToolboxCli.Api/` with three components:
  - **`DirectXTexDecoder`** — Uses `Hexa.NET.DirectXTex` NuGet to decompress BC6H blocks (both UF16 and SF16), applies Reinhard HDR→LDR tonemapping, outputs RGBA8
  - **`BcnDecodeBridge`** — Routes format codes `0x2006` (SF16) and `0x2106` (UF16) to DirectXTex, other formats to BCnEncoder.Net
  - **`TegraSwizzleBridge`** — P/Invoke wrapper for `tegra_swizzle_x64.dll`
- Built standalone C# test (`TestDirectXTex/`) confirming `Hexa.NET.DirectXTex` successfully decompresses BC6H

### Node.js → C# Bridge
- Created `DirectXTexBridge.ts` — loads C# assemblies via `node-api-dotnet`, registers decode callbacks with `BntxDecoder`
- **Critical Discovery**: Native `DirectXTex.dll` requires `process.env['PATH']` to include the nuget output dir for resolution from Node.js context

### TypeScript BntxDecoder Updates
- Added `BC6H_UF16` and `BC6H_SF16` to `BntxFormat` enum
- Added format codes `0x2006`/`0x2106` to `convertFormat()` map
- Added `getFormatInfo()` entry (16 bytes per 4×4 block)
- Added injectable `setBc6hDecoder()` callback pattern
- Added `decodeBc6h()` method that uses the injected callback

### Integration Test Results
- Synthetic 16×16 BC6H image: **231/256 non-black pixels**, varied colors
- Both UF16 and SF16 modes decode successfully
- PNG output via sharp confirmed working

---

## 2. What Work Remains

### Critical — Not Validated
1. **No real BC6H texture tested** — Only synthetic data was decoded. We could not extract `pm1038_12_00_body_a_alb.bntx` from the archive (hash not found in partial dump)
2. **Unknown if BC6H is actually used** — We never confirmed which texture formats are present in the actual game data (violet-dump, scarlet-dump, pokemon-lza-dump). BC6H may not even be needed for core functionality
3. **Reinhard tonemapping not validated** — The HDR→LDR mapping in `DirectXTexDecoder` may produce incorrect brightness/contrast on real game textures

### Architecture Issue
4. **Two parallel pipelines exist** — There are TWO separate BntxDecoder implementations:
   - `SwitchToolboxCli.Core/Texture/BntxDecoder.cs` (C#) — used by `SwitchToolboxCli.App/Program.cs` (the .NET CLI)
   - `SwitchToolboxCli.Api/src/lib/Texture/BntxDecoder.ts` (TypeScript) — used by `SwitchToolboxCli.Api/src/lib/Program.ts` (the Node.js API)
   - BC6H support was only added to the TypeScript version
   - The C# `BntxDecoder.cs` has NO BC6H support

### Bloat Added
5. **Unnecessary C# complexity** — The `bcn-encdec-wasm` npm package (already installed) has `_decode_bc6h_image` WASM function that could replace the entire C# bridge with a few lines of TypeScript
6. **Files created that may not be needed**: `Bc6hBridge.cs`, `DirectXTexBridge.ts`, `Bridge/index.ts`, `test-bc6h-pipeline.ts`, `debug-bntx.ts`, `TestDirectXTex/` folder

---

## 3. Prime Optimization Suspects

### 3.1 Replace C# Bridge with WASM Decoder
**File**: `node_modules/bcn-encdec-wasm/bcn_encdec_wasm.d.ts:294`

The `bcn-encdec-wasm` package already has `_decode_bc6h_image(pSrc, pDst, width, height)` which outputs Float32Array RGB directly in WASM — no C# bridge, no native DLL, no `node-api-dotnet` needed.

**Impact**: Eliminates `Bc6hBridge.cs`, `DirectXTexBridge.ts`, `Hexa.NET.DirectXTex` NuGet, and the PATH hack entirely.

### 3.2 Audit Actual Texture Formats in Game Data
**Action**: Run the existing export pipeline on real game archives and log which `BntxFormat` values are actually encountered.

Most Pokémon SV textures use **BC1** (diffuse), **BC3** (normal maps), **BC4** (roughness), **BC5** (normal maps), and **BC7** (high quality). BC6H is typically reserved for HDR environment maps — which may not even be in the model textures.

### 3.3 Eliminate Dual Pipeline
**Problem**: Two independent decode pipelines (C# and TypeScript) doing the same thing.

**Fix**: Pick one. The TypeScript/Node.js pipeline (`SwitchToolboxCli.Api`) appears to be the active, working one. The C# pipeline (`SwitchToolboxCli.App`) has the same issues documented in docs 15/16 (0 models exported).

### 3.4 Slim Down .csproj Dependencies
**File**: `SwitchToolboxCli.Api.csproj`

The `.csproj` currently pulls in `Hexa.NET.DirectXTex`, `BCnEncoder.Net`, and other packages that are only used by the C# bridge. If we switch to WASM decoding, these can be removed.

---

## 4. Step-by-Step to Fully Working (No Errors)

### Phase 1: Audit Existing Formats (30 min)
1. Add logging to `BntxDecoder.cs` and `BntxDecoder.ts` to print every format code encountered
2. Run the export pipeline against `violet-dump/arc/data.trpfs`
3. Collect the list of actually-used texture formats
4. Determine if BC6H is even needed for core functionality

### Phase 2: Fix Core Pipeline First (2-4 hours)
1. Focus on the working pipeline (TypeScript API or C# App — pick one)
2. Ensure BC1, BC3, BC4, BC5, BC7 decode correctly (these are the common formats)
3. These already have decode implementations — verify they produce correct output

### Phase 3: Add BC6H Only If Needed (1 hour)
1. If format audit shows BC6H textures exist in game data:
   - Use `bcn-encdec-wasm` `_decode_bc6h_image` (TypeScript pipeline)
   - Or add `Hexa.NET.DirectXTex` call to C# `BntxDecoder.cs` (C# pipeline)
2. If BC6H is not present: skip entirely, mark as add-on module

### Phase 4: Create SwitchToolboxMiniCli (2-4 hours)
1. Trace the minimum code path: TRPFS → TRPAK → Oodle decompress → TRMDL/TRMSH/TRMBF parse → DAE export
2. Copy only the files on that critical path to `SwitchToolboxMiniCli`
3. Strip all bloat (UI, Electron, React, unused format handlers)
4. Add-on module system for optional features (BC6H, ASTC, animations)

---

## 5. How to Start/Test

### The TypeScript API Pipeline
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\SwitchToolboxCli.Api

# Build the C# packages (for BNTX parsing via node-api-dotnet)
dotnet build -c Release

# Run the export pipeline
npx tsx src/lib/Program.ts --arc "D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\arc" --model "pokemon/data/pm0025/pm0025_00_00/pm0025_00_00.trmdl" --output ./test-output
```

### The C# CLI Pipeline
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli

# Build
dotnet build

# Run extraction
dotnet run --project src/SwitchToolboxCli.App -- extract-bins "path/to/data.trpfs" -o ./output

# Run model export (currently produces 0 models)
dotnet run --project src/SwitchToolboxCli.App -- convert "path/to/data.trpfs" -o ./output --model-format dae
```

### BC6H Test (Standalone)
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.Tests\test-textures\TestDirectXTex
dotnet run
```

### BC6H Integration Test
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\SwitchToolboxCli.Api
npx tsx src/test-bc6h-pipeline.ts
```

---

## 6. Issues and Resolution Strategies

### Issue 1: Two Parallel Pipelines (C# and TypeScript)
**Symptom**: Changes made to one pipeline don't affect the other. BC6H was only added to TypeScript but the C# pipeline is also in use.

**Strategies**:
1. **Pick one pipeline** — Audit which one actually produces output and focus there
2. **Merge into MiniCli** — Create a new stripped-down project that consolidates the working parts
3. **Deprecate the failing one** — If C# pipeline produces 0 models (per docs 15/16), mark it deprecated
4. **Share core logic** — Use the C# packages via `node-api-dotnet` for parsing, TypeScript for orchestration

### Issue 2: Unknown Format Requirements
**Symptom**: We added BC6H support without knowing if any game textures use it

**Strategies**:
1. **Format audit** — Log all encountered format codes during a real export run
2. **Hash file scan** — Search the hash list for `.bntx` entries and extract a sample set
3. **Test with known model** — Export Pikachu (pm0025), check which formats its textures use
4. **Reference check** — Look at gftool's format handling to see which formats they prioritize

### Issue 3: Over-Engineering (C# Bridge for WASM Problem)
**Symptom**: Built a complex C# → native C++ bridge when a WASM solution existed in npm

**Strategies**:
1. **Replace with WASM** — `bcn-encdec-wasm` has `_decode_bc6h_image` built in
2. **Keep as fallback** — WASM decoder may have different quality; keep C# bridge as optional module
3. **Clean up** — Remove `Bc6hBridge.cs`, `DirectXTexBridge.ts`, excess NuGet packages
4. **Document the learning** — Native DLL loading from Node.js via PATH (useful for future features)

### Issue 4: Archive Extraction Can't Find Expected Files
**Symptom**: `pm1038_12_00_body_a_alb.bntx` not found in archive hash table

**Strategies**:
1. **Verify hash function** — The FNV hash may be producing incorrect results for some paths
2. **Try different archive** — Test with `scarlet-dump` or `pokemon-lza-dump` instead of `violet-dump`
3. **Use wildcard search** — Search for any `body_a_alb.bntx` file in the hash list
4. **Extract by index** — Bypass hash lookup and extract files by index position in TRPFS

---

## 7. New Architecture and Quick Wins

### Quick Wins

1. **Use `bcn-encdec-wasm` for ALL BCn decoding** — Replace the BCnEncoder.Net C# dependency entirely. The WASM package decodes BC1, BC4, BC6H, and BC7 natively in JavaScript.

2. **Add format logging** — One-line change in `BntxDecoder.decodeTexture()` to log format codes:
   ```typescript
   console.log(`[BntxDecoder] ${tex.name}: format=0x${tex.format.toString(16)}`);
   ```

3. **Remove `Bridge/` directory** — If switching to WASM, the entire `Bridge/DirectXTexBridge.ts` and `Bridge/index.ts` can be deleted along with the `Bc6hBridge.cs`.

4. **Clean up test files** — Remove `debug-bntx.ts`, `test-bc6h-pipeline.ts`, `TestDirectXTex/` folder after documenting findings.

### New Architecture: SwitchToolboxMiniCli

Create `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxMiniCli` with:

```
SwitchToolboxMiniCli/
├── core/                    # Minimum viable pipeline
│   ├── TrpfsLoader.ts       # Archive filesystem (TRPFS/TRPFD)
│   ├── TrpakLoader.ts       # Archive container (TRPAK + Oodle)
│   ├── BntxDecoder.ts       # Texture decode (BC1/BC3/BC4/BC5/BC7 only)
│   ├── TegraSwizzle.ts      # GPU deswizzle
│   ├── TrinityDecoder.ts    # Model decode (TRMDL/TRMSH/TRMBF/TRSKL)
│   └── DaeExporter.ts       # DAE output
├── addons/                  # Optional feature modules
│   ├── bc6h-decoder/        # BC6H HDR texture support
│   ├── astc-decoder/        # ASTC texture support
│   ├── animation-decoder/   # TRANM animation to clip DAE
│   └── ui-viewer/           # React/Electron viewer
└── cli.ts                   # Entry point
```

**Core dependencies**: Only `node-api-dotnet` (for Syroot BNTX parsing), `bcn-encdec-wasm` (texture decode), `sharp` (PNG output).

**Add-on pattern**: Each add-on exports a `register()` function that hooks into the decoder pipeline:
```typescript
// addons/bc6h-decoder/index.ts
export function register(decoder: BntxDecoder) {
  decoder.registerFormat('BC6H_UF16', decodeBc6hUf16);
  decoder.registerFormat('BC6H_SF16', decodeBc6hSf16);
}
```

### Features (Prioritized)

| Priority | Feature | Effort | Add-on? |
|----------|---------|--------|---------|
| P0 | TRPFS/TRPAK extraction | Done | Core |
| P0 | BC1/BC3/BC5/BC7 texture decode | Done | Core |
| P0 | Model decode (TRMDL/TRMSH/TRMBF) | Done* | Core |
| P0 | DAE export (model-only) | Done* | Core |
| P1 | Animation decode (TRANM) | Done* | Core |
| P1 | DAE export (clip-only) | Done* | Core |
| P2 | BC6H texture decode | Done | Add-on |
| P2 | ASTC texture decode | Stub | Add-on |
| P3 | Electron/React viewer | Exists | Add-on |

*Done = code exists but may have bugs producing 0 output

---

## 8. Key Files Reference

| Component | Path |
|-----------|------|
| **TypeScript Pipeline** | |
| API Entry | `SwitchToolboxCli.Api/src/lib/Program.ts` |
| BNTX Decoder (TS) | `SwitchToolboxCli.Api/src/lib/Texture/BntxDecoder.ts` |
| Tegra Swizzle (TS) | `SwitchToolboxCli.Api/src/lib/Texture/TegraSwizzle.ts` |
| Archive Loader (TS) | `SwitchToolboxCli.Api/src/lib/Archive/TrpfsLoader.ts` |
| DAE Exporter (TS) | `SwitchToolboxCli.Api/src/lib/Exporters/TrinityColladaExporter.ts` |
| Model Decoder (TS) | `SwitchToolboxCli.Api/src/lib/Decoders/TrinityModelDecoder.ts` |
| BC6H Bridge (TS) | `SwitchToolboxCli.Api/src/lib/Bridge/DirectXTexBridge.ts` |
| **C# Pipeline** | |
| App Entry | `SwitchToolboxCli.App/Program.cs` |
| BNTX Decoder (C#) | `SwitchToolboxCli.Core/Texture/BntxDecoder.cs` |
| C# Bridge | `SwitchToolboxCli.Api/Bc6hBridge.cs` |
| NuGet Packages | `SwitchToolboxCli.Api/SwitchToolboxCli.Api.csproj` |
| **Test Data** | |
| Violet dump | `PokemonGreen.Tests/violet-dump/arc/data.trpfs` |
| Scarlet dump | `PokemonGreen.Tests/scarlet-dump/arc/data.trpfs` |
| PL:Z-A dump | `PokemonGreen.Tests/pokemon-lza-dump/arc/data.trpfs` |
| BC6H test images | `PokemonGreen.Tests/test-textures/format-investigation/` |
| Standalone test | `PokemonGreen.Tests/test-textures/TestDirectXTex/` |

---

## 9. Commit Message

```
Add BC6H decode bridge and BntxDecoder format support

- Create Bc6hBridge.cs with DirectXTexDecoder (Hexa.NET.DirectXTex)
- Add BC6H_UF16/BC6H_SF16 to BntxDecoder.ts format enum and code map
- Create DirectXTexBridge.ts for node-api-dotnet assembly loading
- Add injectable setBc6hDecoder() callback pattern to BntxDecoder
- Wire initDirectXTexBridge() into Program.ts before texture decode
- Standalone test confirms BC6H decode works (synthetic 16x16)
- Document: bcn-encdec-wasm already has _decode_bc6h_image (simpler alternative)

NOTE: BC6H may not be used by actual game textures — format audit needed.
Files created: Bc6hBridge.cs, DirectXTexBridge.ts, test-bc6h-pipeline.ts
```
