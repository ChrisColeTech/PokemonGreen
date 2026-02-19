# 22-STARFOX-GDB1-EXTRACTOR - Lessons Learned

## Overview

**Project**: Star Fox Zero/Guard GDB1 Model/Texture/Animation Extractor
**Status**: Partially working - models extract for some formats, textures raw only
**Location**: `D:\Projects\PokemonGreen\src\PokemonGreen.Tests\starfox-dump\`

---

## 1. What We Accomplished

### Completed

| Feature | Description |
|---------|-------------|
| GDB1 Header Parsing | Magic, flags, root offset, string table |
| Model Structure Discovery | 20-byte vertex records with 0xFFFFFFFF markers |
| Triangle Strip Parsing | Convert strip indices to triangle list |
| Texture Reference Extraction | Find ResourceIDs linking models to textures |
| Batch Extraction Pipeline | Extract all models with linked textures |
| Folder Structure Output | `model_name/model.obj`, `textures/`, `clips/`, `manifest.json` |
| Animation Extractor | ConstColorAnim keyframe extraction to JSON |
| Resource Database | Scan and catalog all resource types |

### Extraction Results

```
Resources scanned:
├── 1893 models (.modelgdb + .modelbin)
├── 1835 textures (.texturegdb + .texturebin)
└── 14 animations (.constcoloranimgdb)

Extraction success:
├── ~24 models with correct geometry (vertex format varies)
├── 262 models with texture references resolved
├── 1835 textures exported as raw
└── 14/14 animations exported to JSON
```

---

## 2. GDB1 Format (Reverse Engineered)

### GDB1 Header Structure (20 bytes)

| Offset | Type | Description | Example |
|--------|------|-------------|---------|
| 0x00 | char[4] | Magic | "GDB1" |
| 0x04 | uint32 | Flags | 0 |
| 0x08 | uint32 | Root object offset? | 7872 |
| 0x0C | uint32 | String table offset? | 1024 |
| 0x10 | uint32 | String table size? | 0 |

### Model Binary Format (.modelbin)

**Structure discovered:**

```
[Index Buffer]     @ offset 0
  - u16 indices (triangle strip format)
  - Ends when 0xFFFFFFFF marker pattern begins

[Vertex Buffer]    @ first 0xFFFFFFFF marker - 12 bytes
  - 20-byte records:
    - bytes 0-5:   Position (3x int16, scale by 1/32767)
    - bytes 6-11:  Normal/other data (3x int16)
    - bytes 12-15: 0xFFFFFFFF marker
    - bytes 16-19: Flags/extra data
```

### Triangle Strip Format

**Key Discovery**: Index buffer uses **triangle strips with degenerate restart markers**

```python
# Raw indices:
[0, 1, 2, 3, 67, 68, 58, 64, 57, 55, 20, 21, 22, 23, 85, 89, 15, 88, 14, 14, 58, 58, 58, ...]
                                                                        ^^  ^^  ^^ restart

# Conversion to triangle list:
for i in range(len(indices) - 2):
    i0, i1, i2 = indices[i], indices[i+1], indices[i+2]
    if i0 == i1 or i1 == i2 or i0 == i2:
        continue  # Skip degenerate
    if i % 2 == 0:
        emit(i0, i1, i2)
    else:
        emit(i0, i2, i1)  # Alternate winding
```

### Texture Format (.texturebin)

| Format ID | Name | BPP | Notes |
|-----------|------|-----|-------|
| 0 | RGBA8 | 32 | Most common |
| 1 | RGB8 | 24 | |
| 2 | RGBA5551 | 16 | |
| 3 | RGB565 | 16 | |
| 6 | L8 | 8 | Grayscale |
| 11 | ETC1 | 4 | Compressed |
| 12 | ETC1A4 | 8 | Compressed + alpha |

### Resource Linking

**Texture references** are stored as 32-bit ResourceIDs in modelgdb:
```python
# In modelgdb binary, search for texture IDs
for i in range(20, len(gdb_data) - 4, 4):
    val = struct.unpack('<I', gdb_data[i:i+4])[0]
    hex_id = f'{val:08x}'
    if hex_id in texture_files:
        # This model references this texture
```

---

## 3. What Work Remains

### Critical Issues

| Issue | Impact | Effort |
|-------|--------|--------|
| **Vertex format varies** | Only ~24/1893 models extract geometry | High |
| **Textures are raw** | Need format conversion for viewing | Medium |
| **No skeletal animation** | Only color animations extracted | High |
| **No material mapping** | Textures not assigned to faces | Medium |

### Missing Features

1. **Multiple vertex formats** - Need to detect and handle:
   - Different vertex strides (8, 12, 16, 20, 24, 32 bytes)
   - Different position encoding (float16, int16 scaled, float32)
   - Compressed normals, tangents, UVs

2. **Texture conversion** - Export to PNG instead of raw:
   - Implement RGBA8 → PNG
   - Implement ETC1/ETC1A4 decompression
   - Handle 3DS GPU swizzling

3. **Material/UV export** - OBJ with MTL files:
   - Parse material definitions from modelgdb
   - Export UV coordinates
   - Create .mtl files linking textures

4. **Skeletal animation** - If present in other file types

---

## 4. Optimizations - Prime Suspects

### 1. Vertex Format Detection (HIGH PRIORITY)

**Problem**: Assuming 20-byte stride for all models fails for most.

**Solution**: Detect format from modelgdb geometry descriptors:
```python
# Look for IndexStreamOffset, IndexStreamSize, IndexNum,
# VertexStreamOffset, VertexStreamSize, VertexNum fields
# These tell us exact offsets and strides

# Key fields to find:
- IndexStreamOffset  # Where indices start in modelbin
- IndexStreamSize    # Byte size of index buffer
- IndexNum           # Number of indices
- VertexStreamOffset # Where vertices start
- VertexStreamSize   # Byte size of vertex buffer
- VertexNum          # Number of vertices
- VertexStride       # Bytes per vertex (8, 12, 16, 20, 24, 32)
```

### 2. GDB1 Field Hash Reverse Engineering (MEDIUM PRIORITY)

**Problem**: Field names use CRC32 hashes, can't directly read structure.

**Solution**:
```python
# Known field names from string table:
fields = ['Root', 'Materials', 'Geometries', 'Nodes',
          'IndexStreamOffset', 'VertexStreamOffset', ...]

# Compute CRC32 for each
import zlib
for name in fields:
    hash = zlib.crc32(name.encode()) & 0xFFFFFFFF
    print(f'{name}: 0x{hash:08x}')

# Then search for these hashes in the GDB data
```

### 3. Texture Swizzle Pattern (MEDIUM PRIORITY)

**Problem**: 3DS/Wii U textures use Morton order (Z-order curve), not linear.

**Solution**:
```python
def deswizzle_morton(data, width, height, bpp):
    """Convert Morton order to linear."""
    output = bytearray(len(data))
    for y in range(height):
        for x in range(width):
            # Morton encoding
            morton = 0
            for i in range(16):
                morton |= ((x >> i) & 1) << (2 * i)
                morton |= ((y >> i) & 1) << (2 * i + 1)
            # Copy pixel
            src = morton * (bpp // 8)
            dst = (y * width + x) * (bpp // 8)
            output[dst:dst+bpp//8] = data[src:src+bpp//8]
    return output
```

### 4. Parallel Extraction (LOW PRIORITY)

**Problem**: Serial extraction of 1893 models is slow.

**Solution**: Use multiprocessing:
```python
from multiprocessing import Pool

def extract_model(model_id):
    # ... extraction logic
    return result

with Pool(8) as p:
    results = p.map(extract_model, model_ids)
```

---

## 5. Step-by-Step Approach to Full Working Extraction

### Phase 1: Fix Vertex Format Detection (2-3 hours)

1. **Analyze multiple modelgdb files** with different geometry:
   ```bash
   python -c "
   # Compare working model (zoness_skydome) vs broken ones
   # Find what differs in the GDB structure
   "
   ```

2. **Find geometry descriptor offsets** in GDB:
   - Search for IndexNum, VertexNum values that match actual data
   - Reverse engineer the offset to these fields

3. **Implement format detection**:
   ```python
   def detect_vertex_format(gdb_data, bin_data):
       # Find actual vertex count and stride
       # Return (offset, stride, count)
   ```

### Phase 2: Implement PNG Texture Export (2 hours)

1. **Add PIL/Pillow dependency**
2. **Implement RGBA8 export** (most common)
3. **Implement deswizzling** for 3DS format
4. **Test with known textures**

### Phase 3: Add UV Coordinates (1-2 hours)

1. **Find UV data in vertex records** (likely bytes 6-9 as half-floats)
2. **Export UVs to OBJ** (`vt u v`)
3. **Create MTL files** linking textures

### Phase 4: Full Validation (1 hour)

1. **Load in Blender**, verify mesh looks correct
2. **Check texture mapping**
3. **Compare to original game screenshots**

---

## 6. How to Test/Run

### Basic Usage

```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.Tests\starfox-dump

# Scan resources
python gdb1_extractor.py scan Resources

# Extract single model
python gdb1_extractor.py Resources/00b1486b.modelgdb

# Batch extract all
python gdb1_extractor.py extract Resources output_folder

# Extract animations
python gdb1_animation_extractor.py extract Resources animations_output
```

### Test Specific Model

```bash
# The zoness_skydome model is known to work
python gdb1_extractor.py Resources/00b1486b.modelgdb

# Check output
cat extracted/zoness_skydome/manifest.json
```

### Validate OBJ

```python
# Quick validation script
with open('model.obj') as f:
    lines = f.readlines()
vertices = sum(1 for l in lines if l.startswith('v '))
faces = sum(1 for l in lines if l.startswith('f '))
print(f'Vertices: {vertices}, Faces: {faces}')
```

---

## 7. Known Issues & Strategies

### Issue 1: Most Models Have 0 Triangles

**Symptom**: Only 24/1893 models extract with geometry.

**Root Cause**: Vertex format varies - 20-byte stride assumption fails.

**Strategies**:
1. **Parse geometry descriptors from GDB** - Find actual offsets/strides
2. **Heuristic detection** - Try multiple strides, validate results
3. **Cluster by format** - Group models by file structure similarity
4. **Compare with working models** - What makes zoness_skydome different?

### Issue 2: Textures Are Raw/Unviewable

**Symptom**: .raw files can't be opened in image viewers.

**Root Cause**: No PNG conversion, may need deswizzling.

**Strategies**:
1. **Implement PIL export** - Convert RGBA8 to PNG
2. **Add deswizzle step** - Morton order to linear
3. **Export with metadata** - Filename includes dimensions/format
4. **Create preview tool** - Quick raw-to-png converter

### Issue 3: No UV/Material Assignment

**Symptom**: Models are untextured in Blender.

**Root Cause**: Not exporting UV coords or MTL files.

**Strategies**:
1. **Find UVs in vertex data** - Likely packed in bytes 6-11
2. **Export MTL files** - Link textures by name
3. **Parse material section of GDB** - Get proper assignments
4. **Use texture filenames** - Match by ResourceID

### Issue 4: Animation Format Limited

**Symptom**: Only ConstColorAnim (color keyframes) extracted.

**Root Cause**: No skeletal animation files found yet.

**Strategies**:
1. **Check other file types** - .ptclgdb might have transform data
2. **Look for .skelgdb** or similar - Might be separate
3. **Analyze particle files** - May contain position animations
4. **Check if retail format differs** - Current files may be debug

---

## 8. Architecture

```
starfox-dump/
├── Resources/                    # Source files
│   ├── *.modelgdb + .modelbin    # 3D models
│   ├── *.texturegdb + .texturebin # Textures
│   ├── *.constcoloranimgdb       # Color animations
│   ├── *.ptclgdb                 # Particles
│   └── *.resourcemetadata        # Build metadata
│
├── gdb1_extractor.py             # Main model+texture extractor
│   ├── GDB1Parser                # Format parser
│   ├── ModelExtractor            # .modelgdb + .modelbin → .obj
│   ├── TextureExtractor          # .texturegdb + .texturebin → .raw
│   └── ResourceDatabase          # Track all resources
│
├── gdb1_animation_extractor.py   # Animation extractor
│   ├── AnimationExtractor        # .constcoloranimgdb → .json
│   └── ColorTrack/Keyframe       # Data structures
│
├── analyze_starfox_model.py      # Analysis/debug tool
│
└── extracted/                    # Output
    └── model_name/
        ├── model.obj
        ├── manifest.json
        ├── textures/*.raw
        └── clips/*.json
```

---

## 9. Quick Wins

### 1. PNG Export for RGBA8 Textures (30 min)

```python
# Add to TextureExtractor
from PIL import Image

def export_png(self, output_path):
    tex = self.extract()
    if tex.format == 0 and len(tex.data) >= tex.width * tex.height * 4:
        img = Image.frombytes('RGBA', (tex.width, tex.height), tex.data)
        img.save(output_path)
        return True
    return False
```

### 2. Better Model Names from Metadata (15 min)

Already implemented - uses `.resourcemetadata` to get original names like "zoness_skydome" instead of hex IDs.

### 3. Skip Empty Models (10 min)

```python
# In batch extraction, skip models with 0 triangles
if result['model']['triangles'] == 0:
    shutil.rmtree(package_folder)  # Clean up empty folder
    continue
```

### 4. Progress Bar (15 min)

```python
from tqdm import tqdm
for model_id in tqdm(resource_db.models.keys(), desc="Extracting"):
    extract_model_package(model_id, resource_db, output)
```

---

## 10. New Features to Consider

### Feature 1: Format Auto-Detection

Automatically detect vertex format by trying multiple configurations:
```python
def auto_detect_format(bin_data):
    for stride in [8, 12, 16, 20, 24, 32]:
        vertices = try_parse_vertices(bin_data, stride)
        if validate_vertices(vertices):
            return stride
    return None
```

### Feature 2: Blender Import Script

Generate a Python script that imports directly into Blender with textures:
```python
# Output: import_model.py
import bpy
bpy.ops.import_scene.obj(filepath='model.obj')
# Set up materials and textures...
```

### Feature 3: Web Viewer

Simple HTML viewer using Three.js to preview models:
```html
<script src="three.js"></script>
<script>
  new THREE.OBJLoader().load('model.obj', obj => scene.add(obj));
</script>
```

### Feature 4: Batch Analysis Report

Generate summary of all models/textures:
```json
{
  "total_models": 1893,
  "working_models": 24,
  "formats_found": ["20-byte", "32-byte", "unknown"],
  "total_textures": 1835,
  "texture_formats": {"RGBA8": 1500, "ETC1": 300, "L8": 35}
}
```

---

## 11. Files Reference

| File | Purpose |
|------|---------|
| `gdb1_extractor.py` | Main extraction tool |
| `gdb1_animation_extractor.py` | Animation extraction |
| `analyze_starfox_model.py` | Debug/analysis tool |
| `Resources/00b1486b.*` | Known working test model (zoness_skydome) |

---

## 12. Test Data

```
D:\Projects\PokemonGreen\src\PokemonGreen.Tests\starfox-dump\
├── Resources/              # 1893 models, 1835 textures, 14 animations
├── extracted_test/         # Batch extraction output
├── extracted_animations/   # Animation JSON output
└── *.py                    # Extraction scripts
```

### Known Working Models

| ID | Name | Triangles | Textures |
|----|------|-----------|----------|
| 00b1486b | zoness_skydome | 129 | 3 |
| 00405a76 | ? | 536 | ? |
| 01c7c771 | ? | 663 | ? |

---

*Last Updated: 2026-02-19*
*Next Step: Fix vertex format detection to increase model extraction success rate*
