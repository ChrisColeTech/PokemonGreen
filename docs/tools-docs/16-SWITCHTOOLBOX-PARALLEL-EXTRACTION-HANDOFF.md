# SwitchToolboxCli Parallel Extraction - Lessons Learned & Handoff

**Date**: 2026-02-18
**Status**: Parallel Extraction Working - BIN CONTENTS NOT YET EXTRACTED
**Project**: `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`

---

## CRITICAL NOTE FOR NEXT AGENT

**THE BIN CONTENTS HAVE NOT BEEN EXTRACTED YET.**

Current state extracts this hierarchy:
```
TRPFS (filesystem) → TRPAKs (archives) → Raw bin files (decompressed)
```

What we have:
- 178,109 raw `.bin` files extracted from 9,892 TRPAKs
- Files are Oodle-decompressed (verified: BNTX headers present)
- Files have generic names like `file_000.bin`, `file_001.bin`

**What we DO NOT have yet:**
- Models converted to DAE/OBJ/GLTF
- Textures converted to PNG/DDS
- Animations converted to usable format
- Proper file extensions based on content

The bins contain Trinity engine formats (BNTX, TRMDL, TRMSH, TRMBF, TRSKL, TRANM, etc.) that need further decoding/conversion.

---

## 1. What We Accomplished

### Parallel Extraction Infrastructure
- **Rewrote `StreamingNestedExtractionService`** with parallel processing
- **Two-phase approach**:
  - Phase 1: Spool all entries to temp files (sequential, minimal memory)
  - Phase 2: Process temp files in parallel (4 workers by default)
- **Temp file cleanup**: Files deleted immediately after processing
- **Thread-safe counters**: Using `Interlocked` for result tracking

### Oodle Decompression Fix
- **Fixed DLL version**: Changed from `oo2core_6_win64.dll` to `oo2core_8_win64.dll`
- **Removed broken sample decompression**: Sample decompression was trying to decompress partial data (272 bytes) which Oodle cannot do
- **Added availability check**: Now checks if Oodle codec is available instead of trying sample decompression
- **Added diagnostic output**: Logs when full decompression fails with reason

### Performance Results
- **9,892 TRPAKs** processed from `data.trpfs`
- **178,109 bin files** extracted (decompressed)
- **0 failures**
- **No .oodle.bin files** - all properly decompressed
- Processing time significantly reduced with 4 parallel workers

### Code Changes

#### `StreamingNestedExtractionService.cs` (Complete Rewrite)
```csharp
// Phase 1: Spool to temp files
foreach (entry in entries)
{
    var tempPath = Path.Combine(tempDir, $"{i:00000}_{name}");
    using var entryStream = entry.OpenRead();
    using var tempFile = new FileStream(tempPath, ...);
    entryStream.CopyTo(tempFile);
}

// Phase 2: Parallel extraction
Parallel.ForEach(spooledEntries,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    spooled => {
        ProcessSpooledEntry(spooled, outputDir, result);
        File.Delete(spooled.TempPath); // Immediate cleanup
    });
```

#### `TrpakFormat.cs` (Oodle Fix)
```csharp
// OLD: Tried sample decompression (always failed)
var compressedPayload = ReadSlice(stream, dataVectorPos, Math.Min(dataLength, 0x110));
TrpakOodleCodec.TryDecompress(compressedPayload, Math.Min(decompressedSize, 0x110), ...);

// NEW: Check if Oodle is available, assume decompression will work at extraction time
var oodleStatus = TrpakOodleCodec.GetStatus();
if (oodleStatus.IsAvailable)
{
    suffix = string.Empty;  // No .oodle suffix
    detailMessage = "compression=oodle; lazy=true; oodle_available";
}
```

#### `TrpakOodleCodec.cs` (Made Public + GetStatus)
```csharp
public static class TrpakOodleCodec  // Changed from internal
{
    public static OodleStatus GetStatus()
    {
        EnsureInitialized();
        return new OodleStatus(_isAvailable, _availabilityDetail, GetResolvedDllPath());
    }

    public readonly record struct OodleStatus(bool IsAvailable, string Detail, string ResolvedPath);
}
```

---

## 2. What Work Remains

### HIGH PRIORITY: Extract Bin Contents

The extracted `.bin` files contain Trinity engine formats that need conversion:

| Bin Type | Extension | Description | Conversion Target |
|----------|-----------|-------------|-------------------|
| BNTX | `.bntx` | Nintendo textures | PNG, DDS |
| TRMDL | `.trmdl` | Trinity model root | DAE, OBJ, GLTF |
| TRMSH | `.trmsh` | Trinity mesh | (part of model) |
| TRMBF | `.trmbf` | Trinity mesh buffer | (vertex/index data) |
| TRSKL | `.trskl` | Trinity skeleton | (armature) |
| TRANM | `.tranm` | Trinity animation | (clips) |
| TRMTR | `.trmtr` | Trinity material | (shaders/textures) |

### Specific Tasks

1. **Model Extraction** (`TrinityStaticMeshDecoder.cs`)
   - Currently produces empty meshes (0 vertices)
   - Need to compare with gftool's `Model.cs` implementation
   - Vertex buffer parsing may be incorrect

2. **Texture Extraction** (`BntxDecoder.cs` or similar)
   - BNTX files need to be decoded to PNG/DDS
   - Switch-Toolbox original has BNTX support

3. **File Extension Detection**
   - Currently all files named `.bin`
   - Need to read magic bytes and assign proper extensions
   - Can be done post-extraction or during extraction

4. **Animation Export**
   - TRANM files detected but not decoded
   - Need skeleton binding for animation export

---

## 3. Optimization Suspects (Where to Begin)

### 3.1 Trinity Model Decode Path
**File**: `TrinityStaticMeshDecoder.cs`

The decoder reads TRMDL/TRMSH/TRMBF but produces empty meshes. This is the main blocker for model export.

**Investigation**:
```csharp
// Check if vertex buffer is being read correctly
// Compare stride/attribute parsing with gftool
// Verify index buffer interpretation
```

**Reference**: `GFTool.Renderer/Scene/GraphicsObjects/Model.cs`

### 3.2 File Extension Detection
**File**: `TrpakFormat.cs` - `GuessExtension()` method

Currently returns `.bin` for most files because we removed sample decompression. Need to detect extension AFTER decompression.

**Fix**: Add post-decompression magic byte detection:
```csharp
openRead = () => {
    var decompressed = TrpakOodleCodec.TryDecompress(...);
    // Read first 4-8 bytes to detect type
    // BNTX, TRMDL, etc. all have magic headers
    return new MemoryStream(decompressed);
};
```

### 3.3 BNTX Texture Extraction
**Current state**: BNTX files extracted as raw `.bin`

**Need**: Decoder that extracts textures from BNTX format to PNG/DDS

**Reference**: Original Switch-Toolbox has full BNTX support

### 3.4 Bundle Assembly for Models
**File**: `TrinityModelAssemblyService.cs`

Models require multiple files assembled together:
- TRMDL (root)
- TRMSH (mesh definitions)
- TRMBF (vertex/index buffers)
- TRSKL (skeleton, optional)
- TRMTR (materials)
- BNTX (textures)

These are linked by FNV-1a hashes. The assembly logic exists but may have bugs.

---

## 4. Step-by-Step to Get Fully Working

### Phase 1: Verify Current Extraction (Complete)
- [x] Parallel extraction working
- [x] Oodle decompression working
- [x] No .oodle.bin files
- [x] 178,109 files extracted

### Phase 2: Add File Extension Detection (1-2 hours)
1. Create `MagicByteSniffer` utility
2. Map magic bytes to extensions:
   - `BNTX` → `.bntx`
   - FlatBuffer with TRMDL structure → `.trmdl`
   - etc.
3. Apply during extraction or as post-process rename

### Phase 3: Fix Trinity Model Decoder (2-4 hours)
1. Add verbose logging to `TrinityStaticMeshDecoder`
2. Dump raw vertex buffer bytes for comparison
3. Compare with gftool's parsing logic
4. Fix attribute/stride interpretation
5. Test with single known-good model

### Phase 4: Add BNTX Texture Extraction (2-4 hours)
1. Port BNTX decoder from Switch-Toolbox or implement new
2. Extract DDS/PNG from BNTX containers
3. Wire into extraction pipeline

### Phase 5: Wire Complete Export Pipeline (2-4 hours)
1. Ensure model assembly receives all required files
2. Export to DAE with proper geometry
3. Include textures and materials
4. Test with complete Pokemon model

---

## 5. How to Start/Test

### Prerequisites
```bash
# Oodle DLL should be copied automatically on build
# Or set environment variable:
export PG_OO2CORE_PATH="D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\lib\oo2core_8_win64.dll"
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

### Extract Bins (Working)
```bash
# Extract all bins from TRPFS (parallel, decompressed)
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- extract-bins "path/to/data.trpfs" -o ./output

# Example output:
# Phase 1: Spooling 9892 entries to temp files...
# Phase 2: Extracting archives in parallel (4 workers)...
# Extract-bins complete. Archives: 9892, Files: 178109, Failed: 0
```

### Convert to Models (NOT WORKING YET)
```bash
# This command exists but produces empty models
dotnet run --project src/PokemonGreen.SwitchToolboxCli.App -- convert "path/to/file.trpfs" -o ./output --model-format dae
```

### Test Corpus
- `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\pokemon-lza-dump\arc\data.trpfs` (3.1GB)

---

## 6. Issues and Resolution Strategies

### Issue 1: Bin Contents Not Extracted (Models/Textures)
**Symptom**: Have 178,109 `.bin` files but no DAE models or PNG textures

**Strategies**:
1. **Fix TrinityStaticMeshDecoder**: Compare with gftool's Model.cs line by line
2. **Add BNTX decoder**: Port from Switch-Toolbox original
3. **Debug bundle assembly**: Log which files are being linked together
4. **Test with minimal case**: Extract single Pokemon model, trace through pipeline

### Issue 2: All Files Named .bin Instead of Proper Extensions
**Symptom**: Files are `file_000.bin` instead of `file_000.bntx`

**Strategies**:
1. **Post-extraction rename**: Read magic bytes, rename files
2. **Extraction-time detection**: Detect after Oodle decompression
3. **Use TRPAK hints**: Some info in TRPAK structure about file types
4. **Hash lookup**: TRPFD contains filename hashes that could be reversed

### Issue 3: Empty Model Output
**Symptom**: DAE files have 0 vertices/faces

**Strategies**:
1. **Add decode logging**: Print vertex counts at each stage
2. **Dump raw buffers**: Save TRMBF bytes for manual inspection
3. **Compare with gftool**: Step through gftool decode, compare byte offsets
4. **Simplify test**: Use smallest model possible

### Issue 4: Missing Bundle Components
**Symptom**: Model export fails because TRMSH or TRMBF not found

**Strategies**:
1. **Verify hash matching**: FNV-1a hash lookup may return wrong files
2. **Log bundle resolution**: Print which files resolve for each model
3. **Check dependency parsing**: TRMDL contains references to other files
4. **Manual assembly test**: Manually identify files for one model, test export

---

## 7. New Architecture and Quick Wins

### Quick Wins (1 hour each)

1. **Magic byte extension detection**
   ```csharp
   public static string DetectExtension(byte[] header)
   {
       if (header.AsSpan().StartsWith("BNTX"u8)) return ".bntx";
       if (header.AsSpan().StartsWith("TRMDL"u8)) return ".trmdl";
       // etc.
   }
   ```

2. **Add `--workers N` flag for parallelism control**
   ```csharp
   var parallelism = args.TryGetOption("--workers", out var w) ? int.Parse(w) : 4;
   ```

3. **Progress bar for extraction**
   ```csharp
   Console.Write($"\r[{processed}/{total}] {percent:P0}");
   ```

4. **Dry-run mode**
   ```bash
   dotnet run -- extract-bins input.trpfs -o ./out --dry-run
   # Lists what would be extracted without writing
   ```

### Architecture Improvements

1. **Separate Extraction and Conversion**
   ```
   extract-bins: TRPFS → TRPAKs → raw bins (DONE)
   extract-models: bins → DAE/GLTF (TODO)
   extract-textures: bins → PNG/DDS (TODO)
   ```

2. **File Type Registry**
   ```csharp
   interface IBinDecoder {
       bool CanDecode(byte[] header);
       object Decode(Stream input);
   }
   // Register: BntxDecoder, TrmdlDecoder, TramnDecoder, etc.
   ```

3. **Manifest Output**
   Write JSON manifest of extracted files with types:
   ```json
   {
     "files": [
       {"path": "arc_pokemon.../file_000.bin", "type": "BNTX", "size": 12345},
       {"path": "arc_pokemon.../file_001.bin", "type": "TRMDL", "size": 6789}
     ]
   }
   ```

### Feature Additions for Next Phase

1. **`extract-models` command** - Convert TRMDL/TRMSH/TRMBF to DAE
2. **`extract-textures` command** - Convert BNTX to PNG
3. **`identify` command** - Scan bins and report file types
4. **`assemble` command** - Combine model components into single export

---

## 8. Key Files Reference

| Component | Path |
|-----------|------|
| CLI Entry | `src/PokemonGreen.SwitchToolboxCli.App/Program.cs` |
| Streaming Extractor | `src/PokemonGreen.SwitchToolboxCli.Core/Extraction/StreamingNestedExtractionService.cs` |
| TRPAK Format | `src/PokemonGreen.SwitchToolboxCli.Formats/Archives/TRPAK/TrpakFormat.cs` |
| Oodle Codec | `src/PokemonGreen.SwitchToolboxCli.Formats/Archives/TRPAK/TrpakOodleCodec.cs` |
| Mesh Decoder | `src/PokemonGreen.SwitchToolboxCli.Formats/Export/Models/Trinity/TrinityStaticMeshDecoder.cs` |
| Model Assembly | `src/PokemonGreen.SwitchToolboxCli.Formats/Export/Models/TrinityModelAssemblyService.cs` |

---

## 9. Reference Implementations

| Component | Reference |
|-----------|-----------|
| Oodle Decompress | `GFTool.Core/Compression/Oodle.cs` |
| Model Decode | `GFTool.Renderer/Scene/GraphicsObjects/Model.cs` |
| BNTX Decode | Switch-Toolbox `FileFormats/Texture/BNTX.cs` |
| GLTF Export | `TrinityModelViewer/Export/GltfExporter.cs` |

---

## 10. Summary for Next Agent

**You have:**
- Working parallel extraction pipeline
- 178,109 decompressed bin files
- Oodle decompression confirmed working

**You need to:**
1. Fix `TrinityStaticMeshDecoder` to produce actual vertices
2. Add BNTX texture extraction
3. Wire the complete export pipeline
4. Add proper file extension detection

**Start here:**
1. Run extraction: `extract-bins data.trpfs -o ./extracted`
2. Pick one bin file, inspect its magic bytes
3. Trace through the model decode path with debugging
4. Compare with gftool's implementation

Good luck!
