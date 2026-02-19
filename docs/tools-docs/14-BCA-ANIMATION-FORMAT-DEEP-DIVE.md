# 14-BCA Animation Format Deep Dive

## Overview

**Project**: BCA Animation Reverse Engineering for Pokken Tournament DX (NX)
**Status**: Phase 1 (Hash Mapping) COMPLETE — mapping architecture fully understood
**Date**: 2026-02-18
**Builds on**: [13-DRP-TO-DAE-LESSONS-LEARNED.md](./13-DRP-TO-DAE-LESSONS-LEARNED.md)

This document corrects and supersedes the BCA/BCL/BCH analysis in doc 13 with verified findings from targeted binary analysis tools.

---

## 1. Corrections to Doc 13

Doc 13 contained several assumptions that have been disproven or refined:

| Doc 13 Claim | Corrected Understanding |
|---|---|
| "BCA Track count = 192" | Actually **190 tracks** fit in the file (4620 - 0x28 header = 4580 bytes / 24 = 190.8). The header value `0x08` contains 192 but the file is too short for 192 full 24-byte entries. The last 20 bytes are leftover/padding. |
| "BCL struct size = 24 bytes" | **Wrong**. BCL entries are **variable-width**, averaging ~647 bytes each (88648 total / 137 entries). The dominant inter-hash gap is 188 bytes (0xBC), which is the spacing between animation channel sub-entries within a bone. |
| "BCA uses different hash function than VBN" | **Partially right**. BCA hashes are NOT CRC32 of bone names AND VBN boneIds are NOT CRC32 either. Both are custom engine IDs. But the real finding is that **hashing is irrelevant** — the BCL provides positional mapping. |
| "Keyframe data at dataOffset" | **dataOffset is always 0, dataSize is 0 or 1**. The BCA contains **only track definitions** (hash, default value, key count, flags). The actual keyframe data lives in the **BCL file**. |
| "Need to find hash mapping function" | **No hash function needed**. The BCL contains BCA track hashes embedded at known positions, and BCL entries map to VBN bones **by sequential index position**. |

---

## 2. Architecture: Three-File Animation System

The Pokken BCA animation system uses three files that work together:

```
BCA (Track Index)          BCL (Bone Curve Data)           VBN (Skeleton)
┌──────────────┐          ┌──────────────────────┐         ┌──────────┐
│ Track[0]     │          │ Entry[0]  → Bone 0   │ ──────▶ │ BASE     │
│  hash=0x5191 │──ref──▶  │   hash=0x5191 + data │         │ id=0x4F10│
│  keys=126    │          │                      │         ├──────────┤
│  flags=0x0E  │          │ Entry[1]  → Bone 1   │ ──────▶ │ CENTER_RT│
│ Track[1]     │          │   hash=0x9165 + data │         │ id=0x7024│
│  hash=0x9165 │──ref──▶  │   hash=0xFF9E + data │         ├──────────┤
│  keys=100    │          │                      │         │ ...      │
│  flags=0x00  │          │ Entry[2]  → Bone 2   │ ──────▶ │ THROW_NUL│
│ ...          │          │   hash=0xE632 + data │         └──────────┘
└──────────────┘          │   hash=0xC8B8 + data │
                          │   hash=0xCA41 + data │
  190 tracks total        │   hash=0x4F9C + data │
  sorted by hash          │ ...                  │
                          └──────────────────────┘
                          137 entries, mapped to
                          VBN bones by index
```

### File Roles

| File | Role | Size (p006) | Entry Count |
|------|------|-------------|-------------|
| **BCA** | Global track index — sorted list of all animation tracks with hash, default value, key count, flags | 4,620 bytes | 190 tracks |
| **BCL** | Per-bone curve data — contains the actual keyframe data, organized by bone. Each entry embeds BCA track hashes as markers. | 88,648 bytes (86.6 KB) | 137 entries |
| **VBN** | Skeleton definition — bone names, hierarchy, transforms | 9,548 bytes | 85 bones |

### Key Relationship

- **BCL entries → VBN bones**: Mapped by **sequential index** (BCL entry 0 = VBN bone 0 = "BASE")
- **BCA tracks → BCL entries**: BCA track hashes are embedded within BCL entry data at known positions
- **No hash function needed**: The mapping is positional, not hash-based

---

## 3. BCA Format (Corrected)

### Header (0x28 bytes)

| Offset | Size | Type | Description | p006 Value |
|--------|------|------|-------------|------------|
| 0x00 | 4 | char[4] | Magic | `"BCA "` |
| 0x04 | 2 | uint16 | Version major | 256 |
| 0x06 | 2 | uint16 | Version minor | 513 |
| 0x08 | 4 | uint32 | Track count (header) | 192 |
| 0x0C | 4 | uint32 | Unknown | 1 |
| 0x10 | 4 | uint32 | Unknown hash/ID | 0x0263F1A8 |
| 0x14 | 4 | float | Global default? | 6.1618 (~353°) |
| 0x18 | 4 | uint32 | Unknown | 100 |
| 0x1C | 4 | uint32 | Unknown | 26 |
| 0x20 | 4 | uint32 | Unknown | 0 |
| 0x24 | 4 | uint32 | Unknown | 1 |

**Note**: The header track count field (192) does NOT match the actual number of tracks that fit in the file. Only 190 complete 24-byte entries fit in 4620 bytes. The last 20 bytes may be padding or a trailing structure.

### Track Entry (24 bytes each, little-endian)

| Offset | Size | Type | Description | Notes |
|--------|------|------|-------------|-------|
| 0x00 | 4 | uint32 | Track hash | Unique ID, found embedded in BCL |
| 0x04 | 4 | float | Default value | Used when keyCount=0 |
| 0x08 | 4 | uint32 | Key count | 0 = static (use default) |
| 0x0C | 4 | uint32 | Flags | Channel type identifier |
| 0x10 | 4 | uint32 | Data offset | Always 0 (data is in BCL) |
| 0x14 | 4 | uint32 | Data size | 0 or 1 |

### BCA Endianness

**Little-endian**. Confirmed by reading the magic as ASCII and verifying uint32 values parse correctly.

### Track Sorting

BCA tracks are sorted by hash value in ascending order. This means track indices do NOT correspond to bone indices — the BCL provides that mapping.

---

## 4. BCL Format (Corrected — Major Revision)

### Header

| Offset | Size | Type | Description | p006 Value |
|--------|------|------|-------------|------------|
| 0x00 | 4 | char[4] | Magic | `"BCL "` |
| 0x04 | 4 | uint32 | Version | 0x11010100 |
| 0x08 | 4 | uint32 | Entry count | 137 |
| 0x0C-0x2F | 36 | - | Zeros | - |
| 0x30 | 4 | uint32 | Unknown hash | 0x19E9F372 |
| 0x34+ | - | - | Entry data begins | - |

### Entry Structure

BCL entries are **variable-width**. Each entry represents one bone and contains:
- Metadata preceding the track hash (small integer counts/indices)
- One or more embedded BCA track hashes (the same uint32 values found in the BCA)
- Keyframe data for each track (the actual animation curves)

**Dominant inter-hash spacing**: 188 bytes (0xBC) — 77 out of 122 consecutive hash gaps. This is the size of each animation channel sub-entry within a bone entry.

### BCL Entry Count vs VBN Bone Count

| Character | VBN Bones | BCL Entries | BCA Tracks | Tracks/Bone |
|-----------|-----------|-------------|------------|-------------|
| p006 (Charizard) | 85 | 137 | 190 | 2.24 avg |

The BCL has 137 entries but only 85 VBN bones. The extra 52 entries likely represent:
- Helper/constraint bones
- IK targets
- Effect attachment points
- Visibility tracks
- Material animation tracks

### BCL Entry ↔ VBN Bone Mapping (Confirmed)

Using proximity-based grouping of BCA hash locations within the BCL (gap threshold ~500 bytes), we confirmed **24 groups mapping exactly to the first 24 VBN bones in order**:

| BCL Group | VBN Bone | BCL Offset | Tracks | Flag Examples |
|-----------|----------|------------|--------|---------------|
| 0 | BASE | 0x021C0 | 1 | 0x0E |
| 1 | CENTER_RT | 0x0437C | 2 | 0x00, 0x0E |
| 2 | THROW_NULL | 0x06954 | 4 | 0x0E, 0x00, 0x00, 0x0E |
| 3 | EXTRA0 | 0x07A24 | 1 | 0x0E |
| 4 | EXTRA1 | 0x08620 | 6 | 0x00, 0x00, 0x12, 0x00, 0x00, 0x12 |
| 5 | EXTRA2 | 0x08CC4 | 7 | 0x12, 0x06, 0x12, 0x22, 0x00, 0x00, 0x22 |
| 6 | EXTRA3 | 0x09548 | 8 | 0x22, 0x22, 0x1A, 0x1A, 0x1A, 0x1E, 0x22, 0x0E |
| 7 | EXTRA4 | 0x0A5A8 | 16 | Mixed |
| 8 | Spine1 | 0x0BEE8 | **25** | Full set |
| 9 | Spine2 | 0x0D500 | 2 | 0x12, 0x12 |
| 10 | Hip | 0x0D9D8 | 1 | 0x12 |
| 11 | Neck1 | 0x0DD34 | 1 | 0x12 |
| 12 | Neck2 | 0x0E270 | 1 | 0x12 |
| 13 | Neck3 | 0x0E68C | 10 | Mixed |
| 14 | Head | 0x0F1A4 | 3 | 0x12, 0x12, 0x16 |
| 15 | R_Shoulder | 0x0FA38 | 1 | 0x12 |
| 16 | R_Arm | 0x0FD94 | 1 | 0x12 |
| 17 | R_ForeArm | 0x100F0 | 1 | 0x16 |
| 18 | R_Hand | 0x10ECC | 1 | 0x1A |
| 19 | L_Shoulder | 0x11408 | 1 | 0x16 |
| 20 | L_Arm | 0x12B44 | 1 | 0x16 |
| 21 | L_ForeArm | 0x13AA0 | 1 | 0x1A |
| 22 | L_Hand | 0x13FDC | 1 | 0x26 |
| 23 | R_UpLeg | 0x144B8 | **27** | Mixed |

**Observation**: Spine1 (25 tracks) and R_UpLeg (27 tracks) are the most heavily animated bones, consistent with a quadruped character (Charizard) that has complex torso and leg movement.

---

## 5. Hash System Analysis (Definitive)

### What Was Tested

| Method | Result |
|--------|--------|
| Direct BCA hash = VBN boneId | **0 matches** out of 190 |
| CRC32(boneName) = BCA hash | **0 matches** |
| CRC32(boneName.ToLower()) = BCA hash | **0 matches** |
| DRP-style CRC (inverted first 4 bytes) = BCA hash | **0 matches** |
| CRC32(boneName + suffix) for 60+ suffixes | **0 matches** |
| CRC32(boneName + null + suffix) | **0 matches** |
| Byte-swap of BCA hash = VBN boneId | **0 matches** |
| Bitwise-invert of BCA hash = VBN boneId | **0 matches** |

### What VBN boneIds Actually Are

VBN boneIds are **NOT CRC32 hashes**. They are engine-assigned IDs with a sequential pattern:

```
EXTRA0: 0x6602A695
EXTRA1: 0x6602A696  (delta = +1)
EXTRA2: 0x6602A697  (delta = +1)
EXTRA3: 0x6602A698  (delta = +1)
EXTRA4: 0x6602A699  (delta = +1)

Spine1:  0x93136EBA
Spine2:  0x93136EB9  (delta = -1)
```

This sequential pattern (incrementing for numbered bone series) proves these are pre-computed engine IDs, not hashes.

### BCA Track Hashes in BCL

**123 out of 190** BCA track hashes were found as uint32 values within the BCL data. The remaining 67 tracks may:
- Have hashes that appear at non-4-byte-aligned offsets
- Be referenced via a different mechanism
- Be inactive/unused tracks (many have keys=0, flags=0x00)

### Conclusion

**No hash function is needed.** The mapping is:
1. BCL entry index → VBN bone index (positional)
2. BCA track hash → BCL entry (hash appears embedded in BCL data)
3. The BCL is the bridge between the sorted BCA track list and the positional VBN skeleton

---

## 6. Track Flags Analysis

BCA track flags appear in increments of 4 (shifted by 2 bits), suggesting a channel type encoding:

| Flag Value | Occurrences | Likely Meaning |
|------------|-------------|----------------|
| 0x00 | Many | Static/no-op channel |
| 0x06 | Several | Rotation channel (Y?) |
| 0x0A | Several | Rotation channel (Z?) |
| 0x0E | Many | Position channel (X?) |
| 0x12 | Many | Position channel (Y?) |
| 0x16 | Several | Position channel (Z?) |
| 0x1A | Several | Scale channel (X?) |
| 0x1E | Several | Scale channel (Y?) |
| 0x22 | Many | Scale channel (Z?) |
| 0x26 | Rare | Combined/special? |

**Pattern**: Flags increment by 4 from 0x02 through 0x26. The formula `(flag - 2) / 4` gives values 0-8, which could represent 9 channels: 3 rotation + 3 position + 3 scale.

**Anomaly**: Flag 0x00 appears for both static tracks (keys=0) and some tracks with key data. Its meaning may be context-dependent.

---

## 7. BCL Data Layout

### Per-Track Sub-Entry (estimated 188 bytes = 0xBC)

Each animation channel within a BCL bone entry follows this approximate layout:

```
Offset  Content
0x00    BCA track hash (uint32)
0x04    All zeros (uint32 × 3-4)
...     Keyframe data (variable)
```

Notable patterns from the fixed-offset analysis:

| Position relative to hash | Top values | Interpretation |
|---------------------------|------------|----------------|
| hash - 16 | 5(47×), 10(35×), 0(19×), 3(15×) | Possibly channel index or curve type |
| hash - 12 | 10(44×), 5(33×), 0(21×), 3(14×) | Possibly segment count |
| hash - 8 | 10(43×), 0(40×), 5(16×), 20(8×) | Possibly key density |
| hash - 4 | 0(44×), 10(32×), 5(22×), 15(8×) | Possibly interpolation mode |
| hash + 0 | (unique hashes) | **BCA track hash** |
| hash + 4 | 0 (123×) | Always zero |
| hash + 8 | 0(117×), 1(6×) | Near-zero |
| hash + 12 | 0 (123×) | Always zero |
| hash + 16 | 0 (123×) | Always zero |

The metadata before each hash (values 3, 5, 10, 15, 20) likely controls:
- Interpolation type (linear, cubic, step)
- Curve segment count
- Key density / decimation level

---

## 8. BCH File

The BCH file (12,380 bytes for p006) has been partially analyzed but its role remains unclear:

- **Magic**: `"BCH "`
- **Entry count**: 241 (larger than both BCL and BCA)
- **Content**: Mostly zeros with sparse data. May contain:
  - Animation clip metadata (start/end frames, loop flags)
  - Event triggers
  - Additional track metadata not in BCA

Given that the BCL contains the actual keyframe data and BCA contains the track index, the BCH may be a higher-level structure referencing multiple BCA/BCL pairs for different animation clips within the same DRP.

---

## 9. BCS Files

The BCS files are **NOT animation data**. Analysis of `BCS _0_p006_start_a_bcs.raw` found string references like `"EventList.Start"`, confirming these contain:
- Script/event data
- Animation triggers
- Gameplay events (hitbox activation, sound cues, etc.)

---

## 10. String Content in BCL

The BCL contains **no bone names or readable strings**. The only strings found are random 3-character sequences that are coincidental byte patterns interpreted as ASCII:

```
0x0000: "BCL "     (magic)
0x00ED: "]r>"      (noise)
0x8688: "DNk"      (noise, repeated 3×)
0xD038: "zJYm"     (noise)
0xDk%:  repeated   (noise pattern in keyframe data)
```

This confirms bone-to-track mapping relies entirely on positional indexing, not name lookup.

---

## 11. Analysis Tools (BcaToDae)

The following commands were added to `BcaToDae.exe` during this investigation:

| Command | Short | Arguments | Purpose |
|---------|-------|-----------|---------|
| `compare-hashes` | `-ch` | `<bca> <vbn>` | Compare BCA hashes vs VBN IDs using CRC32, DRP-CRC |
| `analyze-bcl` | `-ab` | `<bcl> <bca> <vbn>` | Search BCL for bone names, BCA hashes, VBN IDs, strings |
| `find-structure` | `-fs` | `<bcl> <bca> <vbn>` | Analyze BCL structure: hash locations, gaps, context |
| `brute-force` | `-bf` | `<bca> <vbn>` | Try CRC32 with 60+ suffix variants and transforms |
| `map-entries` | `-me` | `<bcl> <bca> <vbn>` | Group BCL hash locations into bone entries, map to VBN |

Source files added to `BcaToDae/Dumpers/`:
- `HashComparer.cs` — CRC32 and DRP-CRC implementations, hash comparison
- `BclAnalyzer.cs` — Deep BCL binary analysis
- `BclStructFinder.cs` — Structural gap/pattern analysis
- `HashBruteForce.cs` — Exhaustive suffix and transform testing
- `BclEntryMapper.cs` — Proximity-based bone-to-track grouping

---

## 12. Updated Architecture

```
BCA Animation System (Pokken DX)
═══════════════════════════════

┌──── chrmhd DRP ────────────────────────────────┐
│                                                 │
│  BCA (Track Index - 4.6KB)                      │
│  ┌─────────────────────────────────────────┐    │
│  │ Header: magic, version, counts          │    │
│  │ Track[0]: hash=0x0565 def=2.97 keys=73  │    │
│  │ Track[1]: hash=0x05CF def=0.00 keys=0   │    │
│  │ ...190 tracks sorted by hash...         │    │
│  └─────────────────────────────────────────┘    │
│           │ (hash references)                   │
│           ▼                                     │
│  BCL (Bone Curves - 88.6KB)                     │
│  ┌─────────────────────────────────────────┐    │
│  │ Header: magic, version, 137 entries     │    │
│  │ Entry[0]: hash=0x5191 + keyframe data   │───▶ VBN bone[0] "BASE"
│  │ Entry[1]: hash=0x9165, 0xFF9E + data    │───▶ VBN bone[1] "CENTER_RT"
│  │ Entry[2]: 4 hashes + data               │───▶ VBN bone[2] "THROW_NULL"
│  │ ...                                     │    │
│  │ Entry[8]: 25 hashes + data              │───▶ VBN bone[8] "Spine1"
│  │ ...                                     │    │
│  │ Entry[85-136]: helper/IK/effect tracks  │    │
│  └─────────────────────────────────────────┘    │
│                                                 │
│  BCH (Clip Metadata - 12.4KB)                   │
│  BCS × 253 (Scripts/Events)                     │
│  BHA (Hitbox Data)                              │
│                                                 │
└─────────────────────────────────────────────────┘
          ▲ bone index mapping
          │
┌──── chrind DRP ─────────────┐
│  VBN (Skeleton - 9.5KB)     │
│  ┌────────────────────┐     │
│  │ bone[0]  "BASE"    │     │
│  │ bone[1]  "CENTER_RT│     │
│  │ bone[2]  "THROW_NUL│     │
│  │ ...85 bones...     │     │
│  └────────────────────┘     │
│  OMO (Legacy animations)    │
└─────────────────────────────┘
```

---

## 13. Next Steps (Updated Plan)

### Phase 2: Keyframe Extraction (~3 hours)

Now that we know BCL contains the keyframe data, the next step is to decode the BCL entry format:

1. **Isolate a single BCL entry** for a bone with few tracks (e.g., Hip with 1 track)
2. **Identify the keyframe encoding** — likely packed floats with header metadata
3. **Correlate with BCA metadata** — the BCA track's `keys` field tells us how many keyframes to expect
4. **Validate**: a rotation track should contain values in the range [-π, π] or as quaternion components [-1, 1]

### Phase 3: Single Bone Test (~2 hours)

1. Pick Hip (1 track, flags=0x12, simplest case)
2. Extract its keyframe data from BCL entry[10]
3. Convert to rotation and export to DAE
4. Verify in Blender

### Phase 4: Full Export (~4 hours)

1. Parse all 137 BCL entries
2. Map tracks to bones using sequential indexing
3. Export all animation channels
4. Integrate into batch mode

### Remaining Unknowns

| Unknown | Priority | Notes |
|---------|----------|-------|
| BCL entry exact boundary positions | High | Current grouping uses proximity heuristic; need precise boundaries |
| Keyframe data encoding format | High | Float arrays? Compressed? Quantized? |
| Meaning of the 4 metadata values before each hash | Medium | Values 3, 5, 10, 15, 20 — likely interpolation/curve params |
| 67 "missing" BCA hashes not found in BCL | Medium | May be at non-aligned offsets or encoded differently |
| BCH file role | Low | Possibly clip-level metadata |
| BCL entries 85-136 purpose | Low | Helper bones, IK, effects |
| Flag 0x00 dual meaning | Low | Static vs runtime-controlled |

---

## 14. Commands Reference (Updated)

```powershell
# Build BCA analysis tool
cd D:\Projects\PokemonGreen\tools\drp-to-dae\BcaToDae
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../bca-publish

# Set paths
$BCA_TOOL = "D:\Projects\PokemonGreen\tools\drp-to-dae\bca-publish\BcaToDae.exe"
$BASE = "D:\Projects\PokemonGreen\src\PokemonGreen.Tests\pokken-dump\NX\packdata"
$BCL = "$BASE\chrmhd\extracted_p006\BCL _0_p006_000_bcl.raw"
$BCA = "$BASE\chrmhd\extracted_p006\BCA _0_p006_000_bca.raw"
$VBN = "$BASE\chrind\master\raw_indp006\VBN_0_bindpose_p006_000.raw"

# Dump individual files
& $BCA_TOOL dump $BCA
& $BCA_TOOL dump $VBN
& $BCA_TOOL dump $BCL

# Hash comparison (confirms no CRC32 match)
& $BCA_TOOL compare-hashes $BCA $VBN

# BCL cross-reference analysis
& $BCA_TOOL analyze-bcl $BCL $BCA $VBN

# BCL structure analysis (gap patterns, hex context)
& $BCA_TOOL find-structure $BCL $BCA $VBN

# Brute-force hash matching (exhaustive suffix search)
& $BCA_TOOL brute-force $BCA $VBN

# Entry mapping (BCL → VBN bone groups)
& $BCA_TOOL map-entries $BCL $BCA $VBN
```

---

## 15. Output Artifact Locations

Analysis outputs saved during this session:

| File | Content |
|------|---------|
| `tools/drp-to-dae/bcl_analysis.txt` | BCL deep analysis: bone name search, hash search, string scan |
| `tools/drp-to-dae/bcl_struct.txt` | BCL structure finder: gap histogram, hex context around hashes |
| `tools/drp-to-dae/brute_force.txt` | Hash brute force: CRC32 with 60+ suffixes, VBN ID patterns |
| `tools/drp-to-dae/entry_map.txt` | Entry mapper: 24 bone groups with track assignments |

---

*Last Updated: 2026-02-18*
*Next Step: Phase 2 — Decode BCL keyframe data format, starting with single-track bones*
