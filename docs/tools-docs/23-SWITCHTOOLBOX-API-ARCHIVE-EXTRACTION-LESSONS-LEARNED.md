# 23-SWITCHTOOLBOX-API-ARCHIVE-EXTRACTION — Lessons Learned & Handoff

**Date**: 2026-02-19
**Status**: Model DAE export working — Texture decode blocked on BNTX offset bug
**Project**: `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\SwitchToolboxCli.Api`

---

## 1. What We Accomplished

### Archive Extraction Pipeline (End-to-End)

| Feature | Status | Details |
|---------|--------|---------|
| **TRPFD parsing** | ✅ Working | FlatBuffer deserialization of `data.trpfd` (273,862 file hashes, 16,112 packs) |
| **TRPFS reading** | ✅ Working | Binary filesystem structure with file offsets into 3.1GB `data.trpfs` |
| **FNV-1a hash computation** | ✅ Fixed | 64-bit masking after each multiply — critical bug fix |
| **Hash cache loading** | ✅ Working | 408K entries from `hashes_inside_fd.txt` text file |
| **Oodle decompression** | ✅ Fixed | Native DLL path resolution via `__dirname` |
| **FlatBuffer model parsing** | ✅ Working | TRMDL, TRMSH, TRMBF, TRSKL, TRMTR deserialized |
| **DAE model export** | ✅ Working | 998KB Pikachu model with 7 submeshes, 5 materials, 81 bones |
| **TegraSwizzle DLL** | ✅ Fixed | Path resolution corrected for `tegra_swizzle_x64.dll` |
| **BNTX texture decode** | ❌ Blocked | Offset overflow in `readInt64()` → `Number()` conversion |

### Pikachu Export Results (pm0025_00_00)

```
exported/
├── model.dae          998 KB   ✅  7 submeshes, 5 materials, 81 bones, 2218+ vertices
├── pm0025_00_00.trmdl 380 B    ✅  Raw TRMDL for reference
├── textures/          (empty)  ❌  BNTX offset bug prevents texture PNG export
└── animations/        (empty)  ⚠️  No matching TRANM files for this model
```

### Critical Bugs Fixed (3 total)

#### Bug 1: FNV Hash 64-bit Overflow (Show-Stopper)

**File**: `src/archive/TrpakTypes.ts` — `FnvHash.Hash()`
**Root Cause**: JavaScript `BigInt` has *unlimited precision*, unlike C# `ulong` which wraps at 64 bits. After multiplication, the hash value grew beyond 64 bits and never matched the archive's stored hashes.
**Fix**: Added `& 0xFFFFFFFFFFFFFFFFn` mask after each `result * FnvPrime`.

```typescript
// BEFORE (broken — hash grows unbounded):
result = result * this.FnvPrime;

// AFTER (correct — matches C# ulong behavior):
result = (result * this.FnvPrime) & this.Mask64;
```

**Impact**: Without this fix, `ExtractFile()` could never locate any file in the archive. This was a complete blocker for all extraction operations.

#### Bug 2: TegraSwizzle DLL Path (Silent Failure)

**File**: `src/bntx/TegraSwizzle.ts`
**Root Cause**: The DLL path was looking for `tegra_swizzle_x64.dll` relative to the wrong directory. Since the file lives in `src/lib/`, we needed `path.resolve(__dirname, '..', 'lib', 'tegra_swizzle_x64.dll')` from the `src/bntx/` directory.
**Fix**: Used `__dirname` + relative path to resolve correctly.

#### Bug 3: Oodle DLL Path (Crash on Decompression)

**File**: `src/archive/Decompressors.ts`
**Root Cause**: `koffi.load('oo2core_8_win64.dll')` used a bare filename, relying on the process working directory or PATH. In `tsx` execution context, neither resolves correctly.
**Fix**: Same pattern as TegraSwizzle — `path.resolve(__dirname, '..', 'lib', 'oo2core_8_win64.dll')`.

---

## 2. What Work Remains

### Critical — Blocks Texture Export

| Issue | File(s) | Root Cause | Effort |
|-------|---------|------------|--------|
| **BNTX offset overflow** | `BinaryDataReader.ts` line 163-168 | `readInt64()` returns `Number(readBigInt64LE(...))` — values exceeding `Number.MAX_SAFE_INTEGER` produce garbage offsets like `-7,493,708,304,967,795,000` | Medium |
| **Texture data loading** | `Texture.ts` line 82-90 | `mipOffsetsOffset` uses corrupted int64 value, causing `SeekTask.run()` to seek out of bounds | Medium |

### Medium Priority — Blocks Animation Export

| Issue | Root Cause | Effort |
|-------|------------|--------|
| **No TRANM files found for pm0025** | The CLI searches for `.tranm` files in the model's directory path, but animations may be stored elsewhere or under different naming | Low |
| **Animation DAE hierarchy** | Per doc 17, animation DAEs must export bones as a nested hierarchy (not flat list) for Blender compatibility | Already diagnosed |

### Low Priority — Completeness

| Issue | Notes |
|-------|-------|
| `--list` mode performance | Lists all 273K files sequentially — could batch output |
| `--all` export mode | Untested on full corpus |
| Error recovery | Single texture decode failure aborts entire export |

---

## 3. Optimizations — Prime Suspects

### 3.1 BNTX readInt64 → Number Truncation (HIGH PRIORITY)

**The Problem**: `BinaryDataReader.readInt64()` converts a BigInt64 to a JavaScript `Number`. For values > `2^53`, this silently produces an incorrect number. The BNTX format stores **absolute memory pointers** as int64 offsets, and when these exceed safe integer range, seeking to them crashes.

**The Fix**: Two approaches:

```typescript
// Option A: Return BigInt directly (breaking change — requires all callers to accept BigInt)
readInt64(): bigint {
    const val = this._needsReversion
        ? this._buffer.readBigInt64BE(this._position)
        : this._buffer.readBigInt64LE(this._position);
    this._position += 8;
    return val;
}

// Option B: Keep Number return but clamp to safe range with error detection
readInt64(): number {
    const big = this._needsReversion
        ? this._buffer.readBigInt64BE(this._position)
        : this._buffer.readBigInt64LE(this._position);
    this._position += 8;
    const num = Number(big);
    if (!Number.isSafeInteger(num) && big !== 0n) {
        console.warn(`readInt64: value ${big} exceeds safe integer range`);
    }
    return num;
}
```

**Recommendation**: Option A is the correct long-term fix. The BNTX parser (`Texture.ts`, `BntxFile.ts`) uses `readInt64()` for pointer offsets that are actually **relative** to the current position in the original C# code. The C# version uses `long` + the reader's base position. We may be misinterpreting absolute vs. relative offsets.

### 3.2 BNTX Offset Interpretation — Absolute vs. Relative (HIGH PRIORITY)

**The Real Issue**: In the original C# Switch-Toolbox, `BinaryDataReader` is a wrapper around a `Stream` with a base offset. When it reads an int64 "offset", it's typically a **relative offset from the current position**, not an absolute file position. The TypeScript port may be treating these as absolute positions.

**Investigation Steps**:
1. Compare `Texture.load()` in TypeScript vs. the original C# `BRTI.Read()` method
2. Check if the C# reader adds `reader.BaseStream.Position` or `reader.BaseOffset` to the raw int64 value
3. The value `-7,493,708,304,967,795,000` as a signed 64-bit int is `0x9828...` — if treated as an unsigned *relative offset from the start of the current section*, it may make sense

### 3.3 Pack Cache Strategy (MEDIUM PRIORITY)

**The Problem**: `TrpfsLoader.TryGetPack()` reads an entire pack from `data.trpfs` each time a new pack hash is encountered. With 16,112 packs and a 3.1GB file, this means potentially reading the entire file multiple times.

**Optimization**: The pack cache (`_packCache`) already prevents re-reads, but for batch export with `--all`, we should:
- Pre-sort requested files by pack to minimize file descriptor open/close cycles
- Use memory-mapped I/O for `data.trpfs` instead of `fs.readSync()`
- Consider streaming pack reads with `createReadStream()` for better OS cache utilization

### 3.4 FlatBuffer Deserialization Overhead (LOW PRIORITY)

**The Problem**: `FlatBufferConverter.DeserializeFrom()` uses a custom schema registry that walks the schema map for every deserialization. For batch export, this is called thousands of times.

**Optimization**: Pre-resolve schema lookups at startup, use a `WeakMap` keyed by constructor function for O(1) lookup. Also consider lazy field access instead of eagerly deserializing all FlatBuffer fields.

---

## 4. Step-by-Step Approach to Get App Fully Working

### Phase 1: Fix BNTX Texture Decode (Estimated: 2-3 hours)

1. **Study the C# original** — Compare `Syroot.NintenTools.Bntx/Texture.cs` `Read()` method with our `Texture.ts` `load()`. Pay special attention to how offsets are computed (absolute vs. relative, signed vs. unsigned).

2. **Add a `readInt64AsBigInt()` method** to `BinaryDataReader.ts`:
   ```typescript
   readInt64AsBigInt(): bigint {
       const val = this._needsReversion
           ? this._buffer.readBigInt64BE(this._position)
           : this._buffer.readBigInt64LE(this._position);
       this._position += 8;
       return val;
   }
   ```

3. **Update `Texture.ts`** to use `readInt64AsBigInt()` for all offset fields, then convert to `Number` only after applying the correct base offset logic:
   ```typescript
   const rawOffset = reader.readInt64AsBigInt();
   const absoluteOffset = Number(rawOffset); // Only if value is a true absolute offset
   // OR
   const relativeOffset = Number(rawOffset) + currentSectionBase; // If relative
   ```

4. **Test with a single BNTX** — Extract one texture file manually and test `BntxFile.fromBuffer()` directly:
   ```typescript
   const buf = loader.ExtractFile('pokemon/data/pm0025/pm0025_00_00/pm0025_00_00_body_a_alb.bntx');
   const bntx = BntxFile.fromBuffer(buf!);
   console.log(`Textures: ${bntx.textures.length}`);
   for (const tex of bntx.textures) {
       console.log(`  ${tex.name} ${tex.width}x${tex.height} format=${tex.format}`);
   }
   ```

5. **Verify PNG output** — Once BNTX parses without errors, run the full export and confirm PNGs appear in `textures/`.

### Phase 2: Fix Animation Discovery (Estimated: 1-2 hours)

1. **Identify TRANM file locations** — Use `--list` mode with grep:
   ```powershell
   npx tsx src/cli.ts --arc "D:\path\to\scarlet" --list | findstr "tranm"
   ```

2. **Map model → animation paths** — Pokémon Scarlet stores animations in paths like:
   ```
   pokemon/data/pm0025/pm0025_00_00/pm0025_00_00_idle.tranm
   pokemon/data/pm0025/pm0025_00_00/pm0025_00_00_walk.tranm
   ```
   If these don't exist, check alternative structures.

3. **Test a single animation export** — Once a TRANM file is found, verify the GFAnimation FlatBuffer deserialization and DAE animation export.

### Phase 3: Validate Blender Import (Estimated: 1 hour)

1. **Import model.dae** into Blender 4.0+ — verify mesh, skeleton, and material assignments
2. **Import animation DAEs** — verify they create a single armature with valid fcurves (per doc 17 diagnostic methodology)
3. **Check texture mapping** — Once texture PNGs export, verify UV mapping in Blender

### Phase 4: Batch Export Validation (Estimated: 2 hours)

1. **Export 3-5 diverse models** — test different Pokémon with varying mesh complexity
2. **Verify error handling** — ensure one bad file doesn't crash the entire batch
3. **Performance baseline** — time a batch of 10 exports to establish throughput

---

## 5. How to Start/Test the API

### Prerequisites

```powershell
# Ensure you're in the API directory
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\SwitchToolboxCli.Api

# Install dependencies (if not done)
npm install

# Required files in src/lib/:
# - oo2core_8_win64.dll    (Oodle decompression)
# - tegra_swizzle_x64.dll  (GPU texture deswizzle)

# Required data files:
# - hashes_inside_fd.txt   (in working directory or src/ directory)
# - data.trpfd + data.trpfs (game archive files)
```

### CLI Commands

```powershell
# List all files in archive (273,862 entries)
npx tsx src/cli.ts --arc "D:\path\to\scarlet\romfs" --list

# Export a single model to DAE
npx tsx src/cli.ts --arc "D:\path\to\scarlet\romfs" --model "pokemon/data/pm0025/pm0025_00_00/pm0025_00_00.trmdl" --output "D:\output\dir"

# Export all models (untested on full corpus)
npx tsx src/cli.ts --arc "D:\path\to\scarlet\romfs" --all --output "D:\output\dir"
```

### Quick Test Script

```powershell
# Run the targeted export test
npx tsx src/test_export.ts
```

### Verify Output

```powershell
# Check exported files
ls D:\Projects\PokemonGreen\src\PokemonGreen.Tests\scarlet-dump\exported\

# Validate DAE structure
Select-String -Path "exported\model.dae" -Pattern "library_geometries" | Measure-Object
# Should show 1 match

# Count vertices
(Select-String -Path "exported\model.dae" -Pattern "float_array").Count
# Should be >0 (position + normal arrays per submesh)
```

---

## 6. Issues & Resolution Strategies

### Issue 1: BNTX Texture Decode — "offset out of range"

**Symptom**: Console error `offset out of range` when decoding any BNTX texture file. Exported `textures/` folder is empty.

**Root Cause**: `BinaryDataReader.readInt64()` converts `BigInt64` → `Number`, producing garbage values for large 64-bit integers.

**Strategies**:

1. **BigInt Propagation** — Change `readInt64()` to return `bigint` natively. Update all callers (`BntxFile.ts`, `Texture.ts`, `ResDict.ts`) to handle `bigint`. Convert to `Number` only at the final consumption point (e.g., `SeekTask.run(reader, Number(offset), ...)`). This is the most correct fix but requires touching many files.

2. **Relative Offset Reinterpretation** — The C# BntxFileLoader computes offsets *relative to the current reader position*. If our TypeScript port is reading absolute values where the C# reads deltas, we're getting garbage because `current_position + delta` ≠ `absolute_pointer`. Audit `Texture.ts` by adding logging: `console.log('offset raw:', rawValue, 'position:', reader.position)` to identify whether deltas or absolutes are expected.

3. **Safe Integer Guard** — Add a guard in `readInt64()` that throws if the result exceeds `Number.MAX_SAFE_INTEGER`, forcing us to find each problematic callsite. This is a diagnostic-first approach:
   ```typescript
   const num = Number(big);
   if (num > Number.MAX_SAFE_INTEGER || num < Number.MIN_SAFE_INTEGER) {
       throw new Error(`Unsafe int64 at position 0x${(this._position - 8).toString(16)}: ${big}`);
   }
   ```

4. **Dual-Method Approach** — Keep `readInt64(): number` for callers that only need 32-bit range values (most game data), and add `readInt64Big(): bigint` for callers that need full 64-bit fidelity (pointers/offsets). This minimizes the blast radius of the change.

### Issue 2: Hash Cache File Discovery

**Symptom**: "Hash file not found" errors when running from different working directories.

**Root Cause**: The CLI looks for `hashes_inside_fd.txt` relative to `process.cwd()`, which varies based on how `tsx` is invoked.

**Strategies**:

1. **CLI argument** — Add `--hashes <path>` flag for explicit path specification
2. **Config-relative** — Look for the file relative to `--arc` directory
3. **Embedded fallback** — Ship a default hash list compiled into the build
4. **Auto-generate** — Build hash cache from TRPFD file hashes + pack names on first run

### Issue 3: Animation File Resolution

**Symptom**: `animations/` directory is empty after export, even for models that have animations in game.

**Strategies**:

1. **Wildcard search** — Instead of looking for exact TRANM paths derived from the model path, search the entire hash cache for entries containing the model's base name (e.g., `pm0025`)
2. **Directory enumeration** — List all files in the model's parent directory via hash cache, then filter for `.tranm` extension
3. **Animation manifest** — Some games store animation lists in the TRMDL or a separate manifest file — inspect the TRMDL FlatBuffer for animation references
4. **Brute-force scan** — Extract all `.tranm` files and match by skeleton bone names instead of filename convention

### Issue 4: DAE Skeleton Hierarchy for Blender

**Symptom**: Per doc 17, flat bone hierarchies in animation DAEs cause Blender to create 100+ separate armatures instead of one.

**Strategies**:

1. **Recursive bone tree** — Already diagnosed in doc 17. The `ColladaExporter` must build `<node>` elements recursively following parent-child relationships from TRSKL
2. **Skeleton root reference** — The `<skeleton>` tag must reference the actual root bone ID (e.g., `#Origin_id`), not a hardcoded `#Bone_0_id`
3. **Pre-import validation** — Write a `verify_dae.ps1` script that checks DAE XML for flat vs. nested bone structures before importing into Blender

---

## 7. Architecture

### TypeScript API Pipeline (Current)

```
SwitchToolboxCli.Api/src/
├── cli.ts                    # Main entry — argument parsing, export orchestration
│   ├── main()                # Parse args, init loader, dispatch commands
│   ├── exportModel()         # Extract TRMDL → parse mesh/skeleton/material → DAE
│   └── enumerateBntxFiles()  # Find texture files for a model
│
├── archive/
│   ├── TrpfsLoader.ts        # Archive extraction — hash lookup, pack reading, Oodle decompress
│   ├── TrpakTypes.ts         # FnvHash, TrpakHashCache, archive type definitions
│   └── Decompressors.ts      # Oodle (koffi FFI) and LZ4 decompression
│
├── bntx/
│   ├── BntxFile.ts           # BNTX container parser — textures, dict, string table
│   ├── Texture.ts            # Individual texture — BRTI header, mip offsets, data loading
│   ├── TegraSwizzle.ts       # GPU deswizzle via tegra_swizzle_x64.dll (koffi FFI)
│   └── BinaryData/
│       └── BinaryDataReader.ts  # Core binary reader — endianness, seek, primitives
│
├── exporters/
│   ├── ColladaExporter.ts    # DAE XML generation — geometry, skeleton, materials
│   └── ColladaTypes.ts       # DAE data model types
│
├── flatbuffers/              # Generated FlatBuffer schemas
│   ├── TR/Model/             # TRMDL, TRMSH, TRMBF, TRMTR, TRSKL
│   └── GF/Animation/         # GFAnimation (TRANM)
│
├── decoders/                 # Format decoders
│   └── Math.ts               # Vector/matrix types
│
├── utils/
│   └── FlatBufferConverter.ts  # Generic FlatBuffer deserialization with schema registry
│
├── lib/                      # Native DLLs
│   ├── oo2core_8_win64.dll   # Oodle decompression
│   └── tegra_swizzle_x64.dll # Tegra GPU texture deswizzle
│
└── test_export.ts            # Quick test script (overrides process.argv)
```

### Data Flow

```
data.trpfd ──→ FlatBufferConverter ──→ CustomFileDescriptor (hashes, pack names, file info)
                                          │
hashes_inside_fd.txt ──→ TrpakHashCache   │  (maps hash → human-readable path)
                              │            │
                              ▼            ▼
                         TrpfsLoader.ExtractFile("pokemon/data/pm0025/.../pm0025_00_00.trmdl")
                              │
                              ├── FnvHash.Hash(path) ──→ file hash
                              ├── TryResolvePackInfo(hash) ──→ pack name + size
                              ├── FnvHash.Hash(packName) ──→ pack hash
                              ├── TryGetPack(packHash) ──→ read pack from data.trpfs
                              ├── FindEntryIndex(pack, fileHash)
                              └── OodleDecompressor.Decompress(buffer)
                                   │
                                   ▼
                              Raw .trmdl / .trmsh / .trskl / .bntx bytes
                                   │
                                   ▼
                         FlatBufferConverter.DeserializeFrom()
                                   │
                                   ▼
                         ColladaExporter → model.dae + textures/*.png
```

### Dual Pipeline Warning

> **Two parallel export pipelines exist in this project** (see doc 20, §2):
> - **C# pipeline**: `SwitchToolboxCli.App/Program.cs` → .NET 8 CLI
> - **TypeScript pipeline**: `SwitchToolboxCli.Api/src/cli.ts` → Node.js via tsx
>
> This document covers only the **TypeScript pipeline**, which is currently further along in actual model export. The C# pipeline has more infrastructure (format detection, batch commands) but produced 0 DAE outputs. BC6H texture support was only added to the TypeScript pipeline.

---

## 8. New Architecture, Features & Quick Wins

### Quick Win 1: Safe Int64 Guard (15 minutes)

Add a diagnostic `readInt64Safe()` that warns on unsafe conversions. This immediately identifies which BNTX fields need BigInt treatment:

```typescript
readInt64Safe(label?: string): number {
    const big = this._needsReversion
        ? this._buffer.readBigInt64BE(this._position)
        : this._buffer.readBigInt64LE(this._position);
    this._position += 8;
    const num = Number(big);
    if (!Number.isSafeInteger(num) && big !== 0n) {
        console.warn(`[INT64 UNSAFE] ${label ?? 'unknown'} at 0x${(this._position - 8).toString(16).padStart(8, '0')}: raw=${big}, num=${num}`);
    }
    return num;
}
```

### Quick Win 2: Graceful Texture Error Recovery (20 minutes)

Currently a single BNTX decode failure may abort the entire export. Wrap the texture loop in try/catch:

```typescript
for (const bntxPath of enumerateBntxFiles(modelDir)) {
    try {
        const bntxBuf = loader.ExtractFile(bntxPath);
        // ... decode and save PNG
    } catch (e) {
        console.warn(`⚠ Skipping texture ${bntxPath}: ${e.message}`);
        // Continue with next texture instead of crashing
    }
}
```

### Quick Win 3: Export Summary Report (15 minutes)

After export, print a summary table:

```
╔══════════════════════════════════════════════╗
║  Export Summary: pm0025_00_00               ║
╠══════════════════════════════════════════════╣
║  Model DAE:    ✅  998 KB, 7 submeshes      ║
║  Skeleton:     ✅  81 bones                 ║
║  Materials:    ✅  5 materials               ║
║  Textures:     ❌  0/4 exported (BNTX bug)  ║
║  Animations:   ⚠️  0 found                  ║
╚══════════════════════════════════════════════╝
```

### Quick Win 4: Hash Cache Auto-Discovery (10 minutes)

Search for `hashes_inside_fd.txt` in multiple locations:

```typescript
const searchPaths = [
    path.join(arcDir, 'hashes_inside_fd.txt'),       // Next to data.trpfd
    path.join(process.cwd(), 'hashes_inside_fd.txt'), // Working directory
    path.resolve(__dirname, '..', 'hashes_inside_fd.txt'), // Relative to src/
];
const hashFile = searchPaths.find(p => fs.existsSync(p));
```

### New Feature: BNTX Standalone Test Tool

Create a standalone script that extracts and decodes a single BNTX file for debugging:

```typescript
// test_bntx.ts
const loader = new TrpfsLoader(arcDir, hashCache);
const buf = loader.ExtractFile(bntxPath);
const bntx = BntxFile.fromBuffer(buf!);
for (const tex of bntx.textures) {
    console.log(`${tex.name}: ${tex.width}x${tex.height} fmt=${SurfaceFormat[tex.format]}`);
    console.log(`  mipOffsets: [${tex.mipOffsets.join(', ')}]`);
    console.log(`  imageSize: ${tex.imageSize}`);
    console.log(`  textureData layers: ${tex.textureData.length}`);
}
```

### New Feature: Batch Export with Progress

For `--all` mode, implement progress tracking with estimated time remaining:

```typescript
const allModels = hashCache.findByExtension('.trmdl');
let done = 0;
const startTime = Date.now();
for (const modelPath of allModels) {
    try {
        await exportModel(loader, modelPath, outputDir);
    } catch (e) {
        console.error(`✗ ${modelPath}: ${e.message}`);
    }
    done++;
    const elapsed = (Date.now() - startTime) / 1000;
    const rate = done / elapsed;
    const eta = Math.round((allModels.length - done) / rate);
    console.log(`[${done}/${allModels.length}] ${rate.toFixed(1)}/s  ETA: ${eta}s`);
}
```

---

## 9. Files Modified This Session

| File | Changes | Lines |
|------|---------|-------|
| `src/archive/TrpakTypes.ts` | Added `Mask64`, applied 64-bit masking in `FnvHash.Hash()` | 73-86 |
| `src/bntx/TegraSwizzle.ts` | Fixed DLL path to `../lib/tegra_swizzle_x64.dll` | 13 |
| `src/archive/Decompressors.ts` | Added `path` import, resolved Oodle DLL path via `__dirname` | 1-4, 20-23 |
| `src/test_export.ts` | Created test script with process.argv override | 1-72 |

## 10. Key Insights for Future Sessions

1. **BigInt is not ulong** — JavaScript BigInt has unlimited precision. Any C#-to-TypeScript port involving `ulong` arithmetic *must* manually mask to 64 bits. This is the #1 gotcha for hash/crypto/checksum ports.

2. **Native DLL paths need `__dirname`** — When running via `tsx` (ESM), `process.cwd()` is unreliable. Always resolve native library paths relative to the source file using `import.meta.url` + `fileURLToPath`.

3. **BNTX offsets may be deltas** — The original C# BNTX reader uses `BinaryDataReader` from Syroot.IO which tracks a base stream offset. The TypeScript port flattens this, treating all offsets as absolute. This works for most fields but breaks for int64 pointer fields that store *stream-relative* positions.

4. **FlatBuffer deserialization needs constructor-type alignment** — The schema registry in `FlatBufferConverter.ts` uses `Function` as the key, which means generic type inference doesn't work well. Passing `new (...args: any[]) => T` would fix TypeScript inference but breaks the runtime `schemaMap.get()` lookup because the key identity changes.

5. **Dual pipeline creates confusion** — The C# and TypeScript pipelines share no code. Fixes applied to one must be manually ported to the other. Consider deprecating the C# pipeline or establishing a single source of truth.

---

*Last Updated: 2026-02-19T13:17:00-05:00*
*Next Step: Fix BNTX readInt64 offset handling to unblock texture export*
