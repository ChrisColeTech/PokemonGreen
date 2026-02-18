# SwitchToolboxCli Lessons Learned

**Date**: 2026-02-18
**Status**: In Progress - Memory Optimized, Decode Path Incomplete
**Project**: `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`

---

## 1. What We Accomplished

### Core Infrastructure
- **Scaffolded .NET 8 CLI solution** with clean separation: Core, Formats, App layers
- **Implemented archive parsers**: GARC, NCSD, NCCH, RomFS, TRPAK, TRPFS/TRPFD
- **Added Oodle decompression** via P/Invoke to `oo2core_8_win64.dll`
- **Created CLI commands**: `info`, `scan`, `extract-bins`, `convert`, `batch`, `bulk-convert`, `bulk-extract-bins`

### Format Support
- **Trinity format hierarchy** parsed: TRPAK containers, TRPFS filesystem archives
- **FlatBuffer-based parsing** for TRPAK entries (vtable traversal)
- **Hash-guided dependency linking** using FNV-1a 64-bit hashes from TRPFD
- **Trinity bin classification**: TRMDL, TRMSH, TRMBF, TRSKL, TRMTR, BNTX, TRANM, etc.

### Memory Optimization (Critical Fix)
- **Lazy Oodle decompression**: Decompress only when `OpenRead()` is called, not during parse
- **Header-only sniffing**: `TrinityBinIndexerService` reads only 512 bytes for classification
- **Non-recursive extraction**: `extract-bins` now extracts top-level archive only (no nested walking)
- **Explicit GC hints**: Added collection points after major processing phases

### Test Results
- **78 tests passing**, 1 skipped (corpus test requires env var)
- **Real corpus processing**: `data.trpfs` (3.1GB) processes without OOM
- **Extraction verified**: 9,892 TRPAK files extracted from Pokemon Legends: Z-A

---

## 2. What Work Remains

### Critical Issues
1. **TRPAK contents show as `.oodle.bin`** - Decompression not triggering correctly
   - Files are detected as Oodle-compressed but output raw compressed data
   - Need to verify Oodle codec is being invoked during extraction

2. **0 DAE models exported** on real corpus
   - `convert` command reports 4,999 bundles as `pending_conversion`
   - Trinity decode path incomplete - geometry not being extracted

3. **0 animation clips exported** on real corpus
   - TRANM files detected but not decoded to DAE

### Incomplete Features
- Trinity static mesh decoder produces no vertices
- Armature builder not wired to export
- Animation decoder not producing clips
- Material/texture linking not complete

---

## 3. Prime Optimization Suspects

### 3.1 TRPAK Oodle Decompression Path
**File**: `TrpakFormat.cs:139-165`

Current issue: Oodle-compressed entries get `.oodle.bin` extension instead of decompressed.

```csharp
// Current code stores raw compressed data when decompression fails
if (!TrpakOodleCodec.TryDecompress(compressed, decompressedSize, out var decompressed, out _))
{
    return new MemoryStream(compressed, writable: false);  // Returns compressed!
}
```

**Fix**: Verify `PG_OO2CORE_PATH` is set, or copy `oo2core_8_win64.dll` to app directory. Check that decompression is being attempted.

### 3.2 Trinity Model Decode Path
**File**: `TrinityStaticMeshDecoder.cs`

The decoder reads TRMDL/TRMSH/TRMBF but produces empty meshes.

**Investigation needed**:
- Check if vertex buffer parsing matches gftool's `Model.cs`
- Verify stride/attribute declaration interpretation
- Check if indices are being read correctly

### 3.3 Archive Walker Recursive Descent
**File**: `ArchiveWalker.cs`

Previously caused OOM by recursively walking all nested archives.

**Current state**: Non-recursive mode added, but recursive still slow.

**Fix**: Add streaming iterator that yields one entry at a time without materializing the full list.

### 3.4 TRPFS Extension Guessing
**File**: `TrpfsFormat.cs:288-292`

Previously did random disk seek per entry. Now disabled (just `.bin`).

**Improvement**: Lazy extension detection at extraction time using magic bytes from first stream read.

---

## 4. Step-by-Step to Fully Working

### Phase 1: Fix Oodle Decompression (1-2 hours)
1. Copy `oo2core_8_win64.dll` to build output directory
2. Add diagnostic logging to `TrpakOodleCodec.TryDecompress`
3. Verify `decompressedSize` is being read correctly from TRPAK entry
4. Test with single TRPAK file, verify decompressed output

### Phase 2: Fix Trinity Mesh Decode (2-4 hours)
1. Compare `TrinityStaticMeshDecoder.cs` with gftool's `Model.cs`
2. Add vertex buffer dump to diagnose parsing issues
3. Verify attribute declarations match expected format
4. Test with known-good TRMDL/TRMSH/TRMBF set

### Phase 3: Wire Export Pipeline (1-2 hours)
1. Ensure `TrinityModelAssemblyService` receives decompressed payloads
2. Verify DAE exporter receives valid `ExportModel` with meshes
3. Add fallback logging when meshes are empty

### Phase 4: Animation Export (2-4 hours)
1. Wire `TrinityAnimationDecoder` to actually process TRANM files
2. Verify skeleton/armature is available for animation binding
3. Test DAE clip export

---

## 5. How to Start/Test

### Prerequisites
```bash
# Set Oodle DLL path
export PG_OO2CORE_PATH="D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\lib\oo2core_8_win64.dll"

# Or copy to build output
cp lib/oo2core_8_win64.dll src/PokemonGreen.SwitchToolboxCli.App/bin/Debug/net8.0/
```

### Build
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli
dotnet build
```

### Test
```bash
dotnet test
```

### Run Commands
```bash
# Info on archive
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- info path/to/file.trpfs

# Extract binaries (non-recursive, fast)
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- extract-bins path/to/file.trpfs -o ./output

# Convert to models
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- convert path/to/file.trpfs -o ./output --model-format dae

# Bulk process directory
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- bulk-extract-bins ./input -o ./output --resume true
```

### Test Corpus
- `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\arc\data.trpfs` (1.3GB)
- `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\pokemon-lza-dump\arc\data.trpfs` (3.1GB)

---

## 6. Issues and Resolution Strategies

### Issue 1: Oodle Decompression Silent Failure
**Symptom**: Files output as `.oodle.bin` instead of decompressed

**Strategies**:
1. **Add logging**: Print whether Oodle DLL loaded, what `TryDecompress` returns
2. **Check decompressedSize**: May be 0 or wrong value from TRPAK parse
3. **Verify DLL version**: `oo2core_8_win64.dll` vs `oo2core_6_win64.dll` - gftool uses v8
4. **Test isolation**: Create unit test that decompresses known-good Oodle data

### Issue 2: Empty Mesh Output
**Symptom**: DAE files have 0 vertices/faces

**Strategies**:
1. **Binary diff**: Compare TRMBF buffer bytes with gftool's interpretation
2. **Step-through debug**: Use debugger to trace vertex attribute parsing
3. **Simplify test**: Create minimal TRMDL/TRMSH/TRMBF with known geometry
4. **Reference implementation**: Port gftool's `Model.cs` decode logic directly

### Issue 3: Pending Conversion on All Bundles
**Symptom**: All 4,999 bundles report `pending_conversion`

**Strategies**:
1. **Check prerequisites**: TRMDL, TRMSH, TRMBF must all be present and readable
2. **Trace bundle assembly**: Log which files are found for each bundle
3. **Validate hashes**: FNV-1a hash lookup may be returning wrong files
4. **Container resolution**: `TrinityContainerReferenceResolver` may have bugs

### Issue 4: Slow Recursive Extraction
**Symptom**: Recursive archive walking times out on large corpus

**Strategies**:
1. **Already fixed**: Non-recursive mode for `extract-bins`
2. **Streaming refactor**: Change `ArchiveWalker.Walk()` to `IAsyncEnumerable`
3. **Parallel processing**: Use `Parallel.ForEach` for independent archives
4. **Progress reporting**: Add progress bar for long operations

---

## 7. New Architecture and Quick Wins

### Quick Wins (1 hour each)

1. **Copy Oodle DLL on build**
   Add to `.csproj`:
   ```xml
   <ItemGroup>
     <None Include="..\..\lib\oo2core_8_win64.dll" CopyToOutputDirectory="PreserveNewest" />
   </ItemGroup>
   ```

2. **Add `--verbose` flag**
   Add diagnostic output controlled by verbosity level.

3. **Progress reporting**
   Use `System.Console.Write($"\rProcessed {count} files...")` for long operations.

4. **Extension detection at extraction**
   Read first 4 bytes when opening stream, map to extension.

### Architecture Improvements

1. **Streaming Pipeline**
   ```
   Archive --(IEnumerable<Entry>)--> Transform --(IAsyncEnumerable<Output>)--> Writer
   ```
   Never materialize full lists; process one entry at a time.

2. **Plugin Format Registry**
   Allow runtime loading of format handlers via reflection or DI.

3. **Unified Error Handling**
   Create `Result<T>` type for operations that can fail with details.

4. **Checkpoint Format**
   Standardize checkpoint JSON for all bulk operations.

### Feature Additions

1. **`inspect` command** - Deep dive into single file format structure
2. **`validate` command** - Check archive integrity without extraction
3. **`diff` command** - Compare two archives, report differences
4. **Filter expressions** - `--filter "*.trmdl"` to process subset

---

## 8. Key Files Reference

| Component | Path |
|-----------|------|
| CLI Entry | `src/PokemonGreen.SwitchToolboxCli.App/Program.cs` |
| Archive Walker | `src/PokemonGreen.SwitchToolboxCli.Core/Extraction/ArchiveWalker.cs` |
| TRPAK Format | `src/PokemonGreen.SwitchToolboxCli.Formats/Archives/TRPAK/TrpakFormat.cs` |
| TRPFS Format | `src/PokemonGreen.SwitchToolboxCli.Formats/Archives/TRPFS/TrpfsFormat.cs` |
| Oodle Codec | `src/PokemonGreen.SwitchToolboxCli.Formats/Archives/TRPAK/TrpakOodleCodec.cs` |
| Mesh Decoder | `src/PokemonGreen.SwitchToolboxCli.Formats/Export/Models/Trinity/TrinityStaticMeshDecoder.cs` |
| Model Assembly | `src/PokemonGreen.SwitchToolboxCli.Formats/Export/Models/TrinityModelAssemblyService.cs` |
| DAE Exporter | `src/PokemonGreen.SwitchToolboxCli.Formats/Export/Dae/DaeExporter.cs` |
| Tests | `tests/PokemonGreen.SwitchToolboxCli.Tests/` |
| README | `README.md` |

---

## 9. Reference Implementations

| Component | gftool Path |
|-----------|-------------|
| Oodle | `GFTool.Core/Compression/Oodle.cs` |
| GFPak | `GFTool.Core/Serializers/GFLX/GFPakSerializer.cs` |
| Model Decode | `GFTool.Renderer/Scene/GraphicsObjects/Model.cs` |
| GLTF Export | `TrinityModelViewer/Export/GltfExporter.cs` |
| File Explorer | `TrinityFileExplorer/TrinityExplorerWindow.cs` |

---

## 10. Commit History

This session's work should be committed with message:
```
Optimize memory usage and add non-recursive extraction

- Lazy Oodle decompression in TRPAK (decompress on read, not parse)
- Header-only sniffing in TrinityBinIndexerService (512 bytes max)
- Non-recursive extract-bins command (FileLoader overload)
- Remove TRPFS per-entry disk seeks during parse
- Add explicit GC hints in ConvertCommand
- Fix SubStream disposal issue for nested archives
- Update tests for lazy decompression behavior
- Add README with usage documentation

Test results: 78 passing, extraction of 3.1GB data.trpfs completes
```
