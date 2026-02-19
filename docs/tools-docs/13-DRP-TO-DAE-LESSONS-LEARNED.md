# 13-DRP-TO-DAE Converter - Lessons Learned

## Overview

**Project**: DRP to DAE Converter for Pokken Tournament
**Status**: Core features complete, BCA animation RE in progress
**Location**: `D:\Projects\PokemonGreen\tools\drp-to-dae\`

---

## 1. What We Accomplished

### Completed

| Feature | Description |
|---------|-------------|
| DRP Decryption | Full support for encrypted archives |
| NUD Model Export | Parse and export to Collada DAE |
| NUT Texture Export | DXT1/3/5, BC4/5 → PNG |
| VBN Skeleton Parsing | 85 bones with hierarchy |
| OMO Animation Export | Working DAE/JSON export |
| Batch Processing | Model + animation pairing from separate folders |
| Memory Optimization | GC flush per character |
| Analysis Tools | Hex dump, string search, pattern analysis |
| BCA Track Parsing | Track header structure identified |
| BCL/BCH Analysis | Bone count and structure analysis |

### Export Results

```
chrdep → 132 characters exported
- 264 DAE models
- 311+ texture PNGs
- Raw BCA/BCL/BCH animation files
```

---

## 2. BCA Animation Format (NEW - Partially Reverse Engineered)

### BCA Header Structure

| Offset | Type | Description | Example (p006) |
|--------|------|-------------|----------------|
| 0x00 | char[4] | Magic | "BCA " |
| 0x04 | ushort[2] | Version | 256.513 |
| 0x08 | uint32 | Track count | 192 |
| 0x0C | uint32 | Unknown (always 1?) | 1 |
| 0x10 | uint32 | Unknown | 0x0263F1A8 |
| 0x14 | float | Default rotation (radians) | 6.1618 (353 deg) |
| 0x18 | uint32 | Frame count | 100 |
| 0x1C | uint32 | Keyframe count | 26 |
| 0x20 | uint32 | Unknown | 0 |
| 0x24 | uint32 | Unknown (always 1?) | 1 |
| 0x28 | - | Track data starts | - |

### BCA Track Structure (24 bytes each)

| Offset | Type | Description | Example |
|--------|------|-------------|---------|
| 0x00 | uint32 | Bone hash/ID | 0x05650D6C |
| 0x04 | float | Default value | 2.9703 |
| 0x08 | uint32 | Keyframe count | 73 |
| 0x0C | uint32 | Flags | 0x22 |
| 0x10 | uint32 | Data offset | 0x0000 |
| 0x14 | uint32 | Data size | 0 |

### BCA Observations

1. **Track count varies by character**: p006=192, p025=175
2. **Frame count varies**: p006=100, p025=80
3. **Header size = 0x28 + (trackCount * 0x18)**
4. **Most tracks have keyframeCount=0** (static bones)
5. **Only ~10-20% of tracks have actual animation data**

### Sample BCA Tracks (p006)

```
[0] hash=0x05650D6C def=2.9703 keys=73 flags=0x22 (ANIMATED)
[1] hash=0x05CF13D8 def=0.0000 keys=0   flags=0x00 (static)
[2] hash=0x0740B6EE def=0.0000 keys=0   flags=0x00 (static)
[3] hash=0x08DB1127 def=0.0000 keys=0   flags=0x00 (static)
[4] hash=0x094D07AF def=6.6126 keys=80  flags=0x12 (ANIMATED)
[5] hash=0x09E1C634 def=6.1618 keys=100 flags=0x1A (ANIMATED)
```

---

## 3. BCL Bone List Format (NEW - Partially Reverse Engineered)

### BCL Header Structure

| Offset | Type | Description | Example (p006) |
|--------|------|-------------|----------------|
| 0x00 | char[4] | Magic | "BCL " |
| 0x04 | ushort[2] | Version | 256.4353 |
| 0x08 | uint32 | Bone count | 137 |
| 0x0C-0x2C | - | Reserved (zeros) | - |
| 0x30 | uint32 | Unknown hash | 0x19E9F372 |

### BCL Observations

1. **Bone count varies**: p006=137, p025=127
2. **Mostly sparse data** - appears to be metadata/binding info
3. **No readable bone names** stored in file
4. **Struct size appears to be 24 bytes** based on pattern analysis
5. **Contains "CL " marker at offset in some files** indicating struct boundaries

### BCL vs VBN Bone Counts

| Character | VBN Bones | BCL Entries | BCA Tracks |
|-----------|-----------|-------------|------------|
| p006 (Charizard) | 85 | 137 | 192 |
| p025 (Pikachu?) | 85? | 127 | 175 |

**Hypothesis**: 
- VBN = skeleton bones (85)
- BCL = animated bones + helpers (137)
- BCA = all tracks including camera/effects (192)

---

## 4. BCH Header Format (NEW - Partially Reverse Engineered)

### BCH Header Structure

| Offset | Type | Description | Example (p006) |
|--------|------|-------------|----------------|
| 0x00 | char[4] | Magic | "BCH " |
| 0x04 | ushort[2] | Version | 256.8 |
| 0x08 | uint32 | Entry count | 241 |
| 0x0C | uint32 | Unknown hash | 0x5317FA24 |

### BCH Bone Reference Structure (0x34 = 52 bytes each)

Repeats `entryCount` times starting at 0x3C:

| Offset | Type | Description |
|--------|------|-------------|
| 0x00 | uint32 | Bone index (0x14 = 20) |
| ... | ... | ... |

### BCH Observations

1. **Entry count is larger than BCL** (241 vs 137)
2. **May contain bone→track mapping**
3. **Fixed struct size of 0x34 bytes**

---

## 5. Critical Discovery: Hash Mismatch

### VBN Bone Hashes
```
BASE:      0x4F101F52
CENTER_RT: 0x7024616A
Spine1:    0x93136EBA
Head:      0x4ADBF3FE
```

### BCA Track Hashes (First Track)
```
Track[0]:  0x05650D6C (does NOT match any VBN hash!)
```

### Theories

1. **Different hash function**: BCA uses different hashing than VBN
2. **Indices not hashes**: Values might be indices into BCL table
3. **Offset encoding**: Hash + file offset combined somehow
4. **Endianness**: Try reading as big-endian

### Next Step: Test Hash Mapping

```bash
# Extract all VBN bone hashes
# Compare with all BCA track hashes
# Look for patterns (XOR, bit shifts, etc.)
```

---

## 6. FULL PLAN: BCA Animation Export

### The Problem
We have animation data (BCA) but can't export it because:
- BCA tracks have hashes that don't match VBN skeleton bone hashes
- We don't know which bone each animation track controls

### Phase 1: Hash Mapping (CURRENT - ~2 hours)
**Goal**: Create mapping from BCA tracks → VBN bones

**Steps**:
1. Extract all VBN bone hashes (85 bones) → list A
2. Extract all BCA track hashes (192 tracks) → list B
3. Extract all BCL entries (137 items) → list C
4. Compare all three lists, find correlation
5. Check if: BCA hash = index into BCL, BCL = index into VBN?

**Result**: `Dictionary<int bcaTrackIndex, int vbnBoneIndex>`

**If this fails**: Try XOR patterns, bit shifts, or check if BCA hashes are actually offsets

---

### Phase 2: Keyframe Extraction (~3 hours)
**Goal**: Read actual animation data from BCA file

**Steps**:
1. Find animated track with keyframeCount > 0
2. Locate keyframe data (after track headers? embedded?)
3. Determine format: raw floats? compressed? quaternion?
4. Extract rotation (and translation/scale if present)
5. Validate: values should change smoothly over time

**Result**: `List<Keyframe> GetKeyframes(int trackIndex)`

**Unknowns**:
- Where is keyframe data stored? (dataOffset field is 0 for most tracks)
- What's the compression format? (if any)

---

### Phase 3: Single Bone Test (~2 hours)
**Goal**: Validate pipeline with one bone before scaling up

**Steps**:
1. Pick track[0] with keyframes
2. Map to VBN bone using Phase 1 result
3. Convert keyframes to quaternion rotation
4. Export single animation channel to DAE
5. Import in Blender, verify bone moves

**Result**: Working DAE with one animated bone

**Reference**: Use `OMOReader.cs` and `AnimationExporter.cs` as templates

---

### Phase 4: Full Export (~4 hours)
**Goal**: Export complete animations for all characters

**Steps**:
1. Loop all tracks with keyframeCount > 0
2. Export all animation channels
3. Test full animation in Blender
4. Integrate into `--batch` mode
5. Test with multiple characters

**Result**: Working BCA → DAE pipeline integrated into batch export

---

### Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Can't find hash mapping | Medium | Try multiple approaches: indices, XOR, BCL lookup |
| Keyframe format is complex | Medium | Start with simplest track, compare with OMO format |
| Animation looks wrong in Blender | Medium | Compare frame-by-frame with OMO reference |
| DAE export is too slow | Low | Could export BVH or JSON instead |
| Some characters have different formats | Low | Test with p006 and p025, verify structure same |

---

### Current Status

| Phase | Status | Notes |
|-------|--------|-------|
| 0. Format structure | Done | BCA/BCL/BCH headers parsed |
| 1. Hash mapping | **IN PROGRESS** | Need to compare VBN/BCA/BCL lists |
| 2. Keyframe extraction | Not started | Blocked on Phase 1 |
| 3. Single bone test | Not started | Blocked on Phase 1 & 2 |
| 4. Full export | Not started | Blocked on Phase 1-3 |

---

## 7. Architecture

```
drp-to-dae/
├── DrpToDae/               # Main CLI
│   ├── Core/               # Extensions, utilities
│   ├── IO/                 # FileData, FileOutput, zlib
│   ├── Formats/
│   │   ├── DRP/            # DRP extraction
│   │   ├── NUD/            # Model parsing
│   │   ├── VBN/            # Skeleton parsing
│   │   ├── NUT/            # Texture parsing
│   │   ├── Collada/        # DAE export
│   │   └── Animation/      # OMO parsing, BCA (future)
│   └── Program.cs          # Entry point
│
├── BcaToDae/               # Animation RE tool
│   ├── Dumpers/
│   │   ├── BcaDumper.cs    # BCA track analysis
│   │   ├── BclDumper.cs    # BCL bone list
│   │   ├── BchDumper.cs    # BCH header
│   │   ├── VbnDumper.cs    # VBN skeleton
│   │   └── ValidationUtils.cs
│   └── Program.cs          # Analysis CLI
│
└── README.md
```

---

## 8. Key Lessons Learned

### 1. Two Animation Systems Exist
- **chrind**: Uses OMO format (Smash 4) - **WORKING**
- **chrmhd**: Uses BCA/BCL/BCH format - **NEEDS RE**

### 2. Files Are Split Across Folders
```
chrdep → models + textures (no animations)
chrind → models + textures + skeleton + OMO animations
chrmhd → BCA animations only
```
**Solution**: `--batch` mode pairs chrdep + chrmhd

### 3. Memory Optimization Required
**Problem**: Loading all DRPs crashes memory
**Solution**: GC.Collect() after each character

### 4. BCA Track Structure Identified
```
Each track = 24 bytes
- Bone hash (4 bytes)
- Default value float (4 bytes)
- Keyframe count (4 bytes)
- Flags (4 bytes)
- Data offset (4 bytes)
- Data size (4 bytes)
```

### 5. BCA Hashes Don't Match VBN
```
VBN "BASE" hash: 0x4F101F52
BCA track[0] hash: 0x05650D6C
```
**Implication**: Need mapping layer via BCL or hash function

### 6. Most Tracks Are Static
```
~10-20% of tracks have keyframes
~80-90% have keyframeCount=0 (use default value)
```
**Optimization**: Only export animated tracks

---

## 9. Step-by-Step Workflow: Extract & Analyze

### Step 1: Extract Raw Files from DRP

```bash
# Set paths
DRP_TOOL="D:/Projects/PokemonGreen/tools/drp-to-dae/publish/DrpToDae.exe"
BCA_TOOL="D:/Projects/PokemonGreen/tools/drp-to-dae/bca-publish/BcaToDae.exe"
PACKDATA="D:/Projects/PokemonGreen/src/PokemonGreen.Tests/pokken-dump/NX/packdata"

# Extract animation files from chrmhd (BCA/BCL/BCH)
$DRP_TOOL --extract-raw "$PACKDATA/chrmhd/master/chrmhdp006_000.drp" "$PACKDATA/chrmhd/extracted_p006/"

# Extract skeleton from chrind (VBN)
$DRP_TOOL --extract-raw "$PACKDATA/chrind/master/indp006_000.drp" "$PACKDATA/chrind/extracted_p006/"
```

### Step 2: List Extracted Files

```bash
# Check what we got from chrmhd
ls "$PACKDATA/chrmhd/extracted_p006/"

# Expected output:
# BCA _0_p006_000_bca.raw    ← Animation tracks
# BCH _0_p006_000_bch.raw    ← Header/metadata
# BCL _0_p006_000_bcl.raw    ← Bone list
# BCS_*.raw                  ← Scene scripts (253 files)
# BHA _*.raw                 ← Hitboxes
# etc.

# Check skeleton from chrind
ls "$PACKDATA/chrind/master/raw_indp006/" | grep VBN

# Expected output:
# VBN_0_bindpose_p006_000.raw  ← Skeleton with bone hashes
```

### Step 3: Analyze Each File Type

```bash
BCA_TOOL="D:/Projects/PokemonGreen/tools/drp-to-dae/bca-publish/BcaToDae.exe"
BASE="D:/Projects/PokemonGreen/src/PokemonGreen.Tests/pokken-dump/NX/packdata"

# Analyze skeleton (VBN) - get bone hashes
$BCA_TOOL dump "$BASE/chrind/master/raw_indp006/VBN_0_bindpose_p006_000.raw"

# Analyze animation tracks (BCA)
$BCA_TOOL dump "$BASE/chrmhd/extracted_p006/BCA _0_p006_000_bca.raw"

# Deep analysis with hex dump
$BCA_TOOL analyze "$BASE/chrmhd/extracted_p006/BCA _0_p006_000_bca.raw"

# Analyze bone list (BCL)
$BCA_TOOL dump "$BASE/chrmhd/extracted_p006/BCL _0_p006_000_bcl.raw"

# Analyze header (BCH)
$BCA_TOOL dump "$BASE/chrmhd/extracted_p006/BCH _0_p006_000_bch.raw"
```

### Step 4: Extract Hashes for Comparison

```bash
# Extract VBN bone hashes (manual for now)
# Look for lines like: [0] "BASE" type=0 parent=... id=0x4F101F52

# Extract BCA track hashes (manual for now)
# Look for lines like: [0] hash=0x05650D6C def=2.9703

# TODO: Add --export-hashes flag to BcaToDae tool
```

### File Types Summary

| Extension | Contains | Tool Command |
|-----------|----------|--------------|
| `.drp` | Encrypted archive | `DrpToDae.exe --extract-raw` |
| `BCA *.raw` | Animation tracks | `BcaToDae.exe dump/analyze` |
| `BCL *.raw` | Bone list | `BcaToDae.exe dump/analyze` |
| `BCH *.raw` | Animation header | `BcaToDae.exe dump/analyze` |
| `VBN *.raw` | Skeleton bones | `BcaToDae.exe dump` |
| `OMO *.raw` | Working animations | Use DrpToDae (already working) |

---

## 10. Commands Reference

```bash
# Build main tool
cd D:\Projects\PokemonGreen\tools\drp-to-dae\DrpToDae
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish

# Build BCA analysis tool
cd D:\Projects\PokemonGreen\tools\drp-to-dae\BcaToDae
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../bca-publish

# Extract raw files from DRP
DrpToDae.exe --extract-raw model.drp ./raw/

# Analyze animation files
BcaToDae.exe dump BCA_file.raw
BcaToDae.exe analyze BCA_file.raw
BcaToDae.exe dump VBN_file.raw

# Batch export
DrpToDae.exe --batch ./chrdep/master ./chrmhd/master ./output/
```

---

## 11. Files to Continue Work

| File | Purpose |
|------|---------|
| `DrpToDae/Program.cs` | Main CLI, batch logic |
| `DrpToDae/Formats/VBN/VBN.cs` | Skeleton parser |
| `DrpToDae/Formats/Animation/OMOReader.cs` | Reference for animation parsing |
| `BcaToDae/Dumpers/BcaDumper.cs` | BCA track analysis (updated) |
| `BcaToDae/Dumpers/BclDumper.cs` | BCL bone list |
| `BcaToDae/Dumpers/VbnDumper.cs` | VBN hash extraction |

---

## 12. Test Data Locations

```
D:\Projects\PokemonGreen\src\PokemonGreen.Tests\pokken-dump\NX\packdata\
├── chrdep/
│   ├── master/              # Model DRPs
│   └── extracted_p006/      # Raw NUD/NUT files
├── chrind/
│   └── master/raw_indp006/  # VBN + OMO files
└── chrmhd/
    ├── master/              # Animation DRPs
    ├── extracted_p006/      # BCA/BCL/BCH for Charizard
    └── extracted_p025/      # BCA/BCL/BCH for Pikachu
```

---

*Last Updated: 2025-02-18*
*Next Step: Phase 1 - Extract VBN/BCA/BCL hash lists and find mapping*
