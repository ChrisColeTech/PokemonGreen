# Ohana3DS CLI Rewrite Plan: Binary Format Specifications and Modernization

> **This is a REWRITE plan, not a port.** Ohana3DS-Rebirth is a .NET 3.5 WinForms application.
> We use it solely as a **reference for understanding the binary formats**, then build a clean
> .NET 8 CLI tool from scratch. No old code is copied.

**Goal:** A modern CLI that reads Pokemon Sun/Moon 3DS model files (GARC archives containing
GfModel format data) and converts them to DAE (Collada) with proper multi-material support
and extracted PNG textures.

---

## Part 1: Binary Format Specifications

### 1.1 GARC Container Format

**Source reference:** `Ohana3DS Rebirth/Ohana/Containers/GARC.cs`

GARC (Game ARChive) is Nintendo's archive format used across 3DS titles to bundle multiple
files. Pokemon Sun/Moon stores all game assets inside numbered GARC archives located at
`RomFS/a/{group}/{index}/{file}` (already pre-extracted in our sun-moon dump).

#### Header (0x00 - variable)

| Offset | Size   | Type     | Description                                        |
|--------|--------|----------|----------------------------------------------------|
| 0x00   | 4      | char[4]  | Magic: `"GARC"` (ASCII)                            |
| 0x04   | 4      | uint32   | GARC header length (bytes) -- just this header block |
| 0x08   | 2      | uint16   | Endianness marker (0xFEFF = little-endian)          |
| 0x0A   | 2      | uint16   | Version (observed: 0x0400)                          |
| 0x0C   | 4      | uint32   | Section count (number of sub-sections)              |
| 0x10   | 4      | uint32   | Data offset (absolute offset to raw file data)      |
| 0x14   | 4      | uint32   | Decompressed total length                           |
| 0x18   | 4      | uint32   | Compressed total length                             |

#### FATO Section (File Allocation Table Offsets)

Immediately follows the GARC header (seek to `garcLength` from start).

| Offset | Size   | Type     | Description                                    |
|--------|--------|----------|------------------------------------------------|
| 0x00   | 4      | char[4]  | Magic: `"OTAF"` (FATO reversed, little-endian) |
| 0x04   | 4      | uint32   | FATO section length                             |
| 0x08   | 2      | uint16   | Number of FATO entries                          |
| 0x0A   | 2      | uint16   | Padding (0xFFFF)                                |
| 0x0C   | N*4    | uint32[] | Offset for each entry (relative to FATB+0x0C)  |

#### FATB Section (File Allocation Table Block)

Located at `fatoPosition + fatoLength`.

For each FATO entry, seek to `FATO_entry_offset + fatbPosition + 0x0C`:

| Offset | Size   | Type     | Description                                       |
|--------|--------|----------|---------------------------------------------------|
| 0x00   | 4      | uint32   | Flags bitmask (which sub-files exist)              |
| ...    | 12*N   | ...      | Per bit set: startOffset(4), endOffset(4), length(4) |

**File data location:** Each sub-file is at absolute position `startOffset + dataOffset`.

**Compression detection:** If the first byte of file data is `0x11`, the file uses LZ11
compression (skip 5 bytes for magic detection).

#### Data Flow

```
GARC Header --> FATO (entry offsets) --> FATB (file locations per entry)
                                              |
                                              v
                                         Raw file data at dataOffset + startOffset
```

#### Sun/Moon Dump Structure

The sun-moon dump at `D:\Projects\Ohana3DS-Rebirth\sun-moon-dump\` is a pre-extracted 3DS
ROM with the following structure:
- `RomFS/a/{group}/{subgroup}/{file_index}` -- numbered files, no extensions
- Group `a/0/3/` contains large files (~3MB each) -- these are the Pokemon model GARCs
- Group `a/0/7/` and similar contain model/texture data
- Files are either raw GfModel/BCH data or GARC archives containing them

**Important:** Since our dump is already pre-extracted from the GARC containers (the
`RomFS/a/` tree is the result of GARC extraction by the 3DS dumping tool), the individual
numbered files within each subfolder ARE the raw content files. We still need a GARC reader
for cases where the dumped files are themselves GARC archives containing sub-files (nested
archives), but the primary extraction is already done.

---

### 1.2 GfModel Format (Game Freak Model)

**Source reference:** `Ohana3DS Rebirth/Ohana/Models/PocketMonsters/GfModel.cs`

This is the proprietary format Game Freak uses for Pokemon Sun/Moon models. It wraps
model geometry, materials, textures, and skeleton data in a custom container that is
distinct from the generic BCH format used in earlier 3DS Pokemon games.

#### Top-Level Container

| Offset | Size   | Type     | Description                                   |
|--------|--------|----------|-----------------------------------------------|
| 0x00   | 4      | uint32   | Unknown (skipped, possibly version/flags)      |
| 0x04   | 20     | uint32[5]| Section counts: [models, textures, sect2, sect3, sect4] |
| 0x18   | N*4    | uint32[] | Offset table for section entries               |

**Section indices:**
- Section 0: Models (GfModel sub-sections)
- Section 1: Textures (GfTexture sub-sections)
- Sections 2-4: Unknown/unused for our purposes

Each section entry begins with:

| Offset | Size   | Type     | Description                          |
|--------|--------|----------|--------------------------------------|
| 0x00   | 1      | byte     | Name string length                   |
| 0x01   | N      | char[N]  | Name string (ASCII, length-prefixed) |
| N+1    | 4      | uint32   | Absolute offset to descriptor data   |

#### Model Descriptor

After seeking to `descAddress`, the model data begins:

| Offset       | Size | Type      | Description                                    |
|--------------|------|-----------|------------------------------------------------|
| 0x00         | 16   | ...       | Unknown/padding (skipped)                       |
| 0x10         | 8    | char[8]   | Magic string: `"gfmodel\0"` (or similar)        |
| 0x18         | 4    | uint32    | Model data length                               |
| 0x1C         | 4    | uint32    | Padding (0xFFFFFFFF)                            |

#### String Tables

Four string tables follow in sequence, each structured as:

| Offset | Size     | Type      | Description                                   |
|--------|----------|-----------|-----------------------------------------------|
| 0x00   | 4        | uint32    | Count of strings                              |
| 0x04   | N*0x44   | entries[] | Array of {uint32 hash, char[0x40] name}       |

The four tables are, in order:
1. **Effect names** (shader effects)
2. **Texture names** (referenced texture assets)
3. **Material names** (material definitions)
4. **Mesh names** (geometry sub-meshes)

Each entry is exactly 0x44 bytes: 4-byte hash + 0x40-byte null-terminated string.

#### Transform Matrix

Following the string tables:

| Offset | Size | Type      | Description                         |
|--------|------|-----------|-------------------------------------|
| 0x00   | 32   | float[8]  | Unknown (2x float4, possibly quaternions) |
| 0x20   | 64   | float[16] | 4x4 transform matrix (M11-M44, row-major) |

#### Unknown Data Section

| Offset | Size | Type   | Description                              |
|--------|------|--------|------------------------------------------|
| 0x00   | 4    | uint32 | Unknown data length                       |
| 0x04   | 4    | uint32 | Relative start offset of unknown data     |
| 0x08   | 8    | ...    | Padding                                   |

Skip forward by `unkDataRelStart + unkDataLen`.

#### Skeleton

| Offset | Size | Type   | Description                   |
|--------|------|--------|-------------------------------|
| 0x00   | 4    | uint32 | Bone count                     |
| 0x04   | 12   | ...    | Unknown (skipped)              |

Per bone:

| Offset | Size | Type    | Description                                    |
|--------|------|---------|------------------------------------------------|
| 0x00   | 1    | byte    | Bone name length                                |
| 0x01   | N    | char[N] | Bone name                                       |
| N+1    | 1    | byte    | Parent name length                              |
| N+2    | M    | char[M] | Parent name (empty string = root)               |
| N+M+2  | 1    | byte    | Flags                                           |
| N+M+3  | 12   | float[3]| Scale (X, Y, Z)                                 |
| ...    | 12   | float[3]| Rotation (X, Y, Z) -- Euler angles in radians   |
| ...    | 12   | float[3]| Translation (X, Y, Z)                           |

Parent bone is resolved by name lookup against previously read bones (index via `IndexOf`).

#### Materials

Materials are located at `mdlStart + mdlLength + 0x20`.

For each material (count = `materialNames.Length`):

| Offset | Size | Type    | Description                                   |
|--------|------|---------|-----------------------------------------------|
| 0x00   | 8    | char[8] | Magic: `"material"` (8-byte string)            |
| 0x08   | 4    | uint32  | Material section length                         |
| 0x0C   | 4    | uint32  | Padding (0xFFFFFFFF)                            |

**Material header fields (4 name slots):**

For each of 4 name slots:

| Offset | Size | Type    | Description                        |
|--------|------|---------|------------------------------------|
| 0x00   | 4    | uint32  | Hash (possibly CRC or FNV)          |
| 0x04   | 1    | byte    | Name length                         |
| 0x05   | N    | char[N] | Name string                         |

- `unkNames[0]` = the mesh name this material binds to (used for material-to-mesh mapping)
- Other names: unknown purpose

**Material-to-mesh binding:** `matMeshBinding.Add(unkNames[0])` -- the first name slot in
each material specifies which mesh this material is associated with. When meshes are loaded,
`obj.materialId = matMeshBinding.IndexOf(obj.name)` resolves the binding.

After the name slots, skip `0xAC` bytes of material parameters.

**Texture coordinate bindings (up to 3 texture units):**

Each texture unit entry is `0x42` bytes:

| Offset | Size | Type    | Description                              |
|--------|------|---------|------------------------------------------|
| 0x00   | 4    | uint32  | Hash                                      |
| 0x04   | 1    | byte    | Texture name length                       |
| 0x05   | N    | char[N] | Texture name (mat.name0/name1/name2)      |
| ...    | 2    | uint16  | Unit index                                |
| ...    | 4    | float   | Scale U                                   |
| ...    | 4    | float   | Scale V                                   |
| ...    | 4    | float   | Rotation                                  |
| ...    | 4    | float   | Translate U                               |
| ...    | 4    | float   | Translate V                               |
| ...    | 4    | uint32  | Texture wrap U (low 3 bits)               |
| ...    | 4    | uint32  | Texture wrap V (low 3 bits)               |

An empty texture name signals end of texture units for this material.

#### Meshes

For each mesh (count = `meshNames.Length`):

| Offset | Size | Type    | Description                            |
|--------|------|---------|----------------------------------------|
| 0x00   | 8    | char[8] | Magic: `"mesh\0\0\0\0"` (8-byte)       |
| 0x08   | 4    | uint32  | Mesh section length                     |
| 0x0C   | 4    | uint32  | Padding (0xFFFFFFFF)                    |

Skip `0x80` bytes of mesh header data, then parse sub-mesh info.

**Sub-mesh info parsing (`getSubMeshInfo`):**

PICA200 GPU command buffers are read in a loop:

| Offset | Size | Type   | Description                        |
|--------|------|--------|------------------------------------|
| 0x00   | 4    | uint32 | Command buffer length (in bytes)    |
| 0x04   | 4    | int32  | Current command index               |
| 0x08   | 4    | int32  | Total command count                 |
| 0x0C   | 4    | int32  | Unknown                             |

The loop continues while `currCmdIdx + 1 < totalCmds` (or first iteration).
The command buffer data follows immediately (length/4 words).

After all command buffers, `count = totalCmds / 3` sub-meshes exist.

Per sub-mesh:

| Offset | Size | Type    | Description                         |
|--------|------|---------|-------------------------------------|
| 0x00   | 4    | uint32  | Hash                                 |
| 0x04   | 4    | uint32  | Sub-mesh name length                 |
| 0x08   | N    | char[N] | Sub-mesh name                        |
| ...    | 1    | byte    | Node list length                     |
| ...    | N    | byte[N] | Node list (bone indices)             |
| pad to | 0x20 | ...     | (seek to nodeListStart + 0x20)       |
| ...    | 4    | uint32  | Vertex count                         |
| ...    | 4    | uint32  | Index count                          |
| ...    | 4    | uint32  | Vertex buffer byte length            |
| ...    | 4    | uint32  | Index buffer byte length             |

**Vertex parsing:** For each sub-mesh, 3 command buffers are used:
- `cmdBuffers[sm * 3 + 0]` = vertex shader attributes commands
- `cmdBuffers[sm * 3 + 2]` = index buffer commands

From the vertex command buffer, extract:
- `vshAttributesBufferStride` = stride between vertices
- `vshTotalAttributes` = number of attributes per vertex
- `vshMainAttributesBufferPermutation` = which attribute is at each slot
- `vshAttributesBufferPermutation` = slot ordering for this buffer
- `vshAttributesBufferFormat` = type and component count for each attribute

**Vertex attribute format types (PICA200):**

| Value | Type          | Size per component |
|-------|---------------|--------------------|
| 0     | signed byte   | 1                  |
| 1     | unsigned byte | 1                  |
| 2     | signed short  | 2                  |
| 3     | float         | 4                  |

`attributeLength` = number of components minus 1 (0=1 comp, 1=2, 2=3, 3=4).

**Vertex attributes (PICA200 VSH attribute enum):**

| Value | Attribute             | Components |
|-------|-----------------------|------------|
| 0     | position              | 3 (xyz)    |
| 1     | normal                | 3 (xyz)    |
| 2     | tangent               | 3 (xyz)    |
| 3     | color                 | 4 (rgba)   |
| 4     | textureCoordinate0    | 2 (uv)     |
| 5     | textureCoordinate1    | 2 (uv)     |
| 6     | textureCoordinate2    | 2 (uv)     |
| 7     | boneIndex             | 1-4        |
| 8     | boneWeight            | 1-4        |

**Index buffer:** Immediately follows vertex buffer data. Format is either:
- `unsignedByte` (1 byte per index)
- `unsignedShort` (2 bytes per index)

Determined by `idxCmdReader.getIndexBufferFormat()`.

**Bone weight special case:** Bone weights use `unsignedByte` format regardless of what the
format descriptor says (hardcoded override in the parser). Values are divided by 255.0 to
normalize to [0, 1].

**Node list fallback:** If a vertex has no bone indices AND the node list has 4 or fewer
bones, the entire node list is assigned to the vertex with weight 1.0.

#### Material-to-Mesh-to-Texture Data Flow

```
GfModel Container
  |
  +-- String Tables: textureNames[], materialNames[], meshNames[]
  |
  +-- Materials (ordered by materialNames[])
  |     |-- name = materialNames[m]
  |     |-- unkNames[0] = mesh name this material binds to
  |     |-- name0 = texture name for unit 0  ---> references textureNames[]
  |     |-- name1 = texture name for unit 1
  |     |-- name2 = texture name for unit 2
  |     +-- texture coordinators (scale, rotate, translate per unit)
  |
  +-- Meshes (ordered by meshNames[])
  |     |-- Sub-meshes within each mesh
  |     |     |-- name = sub-mesh name
  |     |     |-- materialId = matMeshBinding.IndexOf(sub_mesh_name)
  |     |     |     ^ This links the sub-mesh to a material by matching its name
  |     |     |       against the first name slot of each material
  |     |     +-- vertex/index data
  |     +-- ...
  |
  +-- Textures (separate section, Section 1)
        |-- GfTexture data (name, pixels, format)
        +-- Referenced by material.name0/name1/name2 strings
```

---

### 1.3 BCH Format

**Source reference:** `Ohana3DS Rebirth/Ohana/Models/BCH.cs` (2171 lines)

BCH is the standard binary container format used by many 3DS games (developed by Nintendo).
It was used in Pokemon X/Y and OR/AS. Sun/Moon primarily uses GfModel, but BCH knowledge
is included here for completeness and potential fallback support.

**Note for our CLI:** BCH support is secondary priority. Sun/Moon models use GfModel. BCH
may be needed for compatibility with X/Y and OR/AS models.

#### Header (0x00 - 0x44+)

| Offset | Size | Type    | Description                                    |
|--------|------|---------|------------------------------------------------|
| 0x00   | 4    | char[4] | Magic: `"BCH\0"` (ASCII)                       |
| 0x04   | 1    | byte    | Backward compatibility version                  |
| 0x05   | 1    | byte    | Forward compatibility version                   |
| 0x06   | 2    | uint16  | Version                                          |
| 0x08   | 4    | uint32  | Main header offset                               |
| 0x0C   | 4    | uint32  | String table offset                              |
| 0x10   | 4    | uint32  | GPU commands offset                              |
| 0x14   | 4    | uint32  | Data offset                                      |
| 0x18   | 4    | uint32  | Data extended offset (bc > 0x20 only)            |
| 0x1C   | 4    | uint32  | Relocation table offset                          |
| 0x20   | 4    | uint32  | Main header length                               |
| 0x24   | 4    | uint32  | String table length                              |
| 0x28   | 4    | uint32  | GPU commands length                              |
| 0x2C   | 4    | uint32  | Data length                                      |
| 0x30   | 4    | uint32  | Data extended length (bc > 0x20 only)            |
| 0x34   | 4    | uint32  | Relocation table length                          |
| 0x38   | 4    | uint32  | Uninitialized data section length                |
| 0x3C   | 4    | uint32  | Uninitialized description section length         |
| 0x40   | 2    | uint16  | Flags (bc > 7 only)                              |
| 0x42   | 2    | uint16  | Address count (bc > 7 only)                      |

**Version history:**
- bc 0x05: Kirby Triple Deluxe
- bc 0x07: Pokemon X/Y
- bc 0x20: Some Senran Kagura models
- bc 0x21: Pokemon OR/AS, SSB3DS, Zelda ALBW
- bc 0x22/0x23: Codename S.T.E.A.M.

#### Relocation Table

The relocation table patches relative offsets to absolute offsets. The game does this in RAM
after loading. The BCH parser must do this before reading any content.

Each 4-byte entry in the relocation table encodes:
- `offset = value & 0x1FFFFFF` (25-bit offset)
- `flags = value >> 25` (7-bit flags)

Flag meanings:
- `0`: Main header relative offset (multiply offset by 4, add mainHeaderOffset)
- `1`: String table relative offset (add stringTableOffset)
- `2`: GPU commands relative offset (multiply offset by 4, add gpuCommandsOffset)
- `7`, `0xC`: Data relative offset (multiply offset by 4, add dataOffset)
- GPU commands section has version-dependent flags for textures, vertices, and indices

#### Content Header (at mainHeaderOffset)

Contains 17 sections, each with {pointerTableOffset, pointerTableEntries, nameOffset}:
1. Models
2. Materials
3. Shaders
4. **Textures** (key for us)
5. Materials LUT
6. Lights
7. Cameras
8. Fogs
9-14. Various animations (skeletal, material, visibility, light, camera, fog)
15. Scene

#### BCH Texture Section

For each texture entry:

| Offset | Size | Type   | Description                           |
|--------|------|--------|---------------------------------------|
| 0x00   | 4    | uint32 | Texture unit 0 commands offset         |
| 0x04   | 4    | uint32 | Texture unit 0 commands word count     |
| 0x08   | 4    | uint32 | Texture unit 1 commands offset         |
| 0x0C   | 4    | uint32 | Texture unit 1 commands word count     |
| 0x10   | 4    | uint32 | Texture unit 2 commands offset         |
| 0x14   | 4    | uint32 | Texture unit 2 commands word count     |
| 0x18   | 4    | uint32 | Padding                                |
| 0x1C   | 4    | uint32 | Texture name (string table pointer)    |

The PICA command buffer at unit 0 commands offset provides:
- Texture address (where pixel data lives)
- Texture size (width/height)
- Texture format (PICA200 format enum)

#### BCH Material Section (Key Material Fields)

Material entry size varies by version:
- bc < 0x21: entry size = 0x58
- bc >= 0x21: entry size = 0x2C

Per material:

| Offset | Size | Type   | Description                              |
|--------|------|--------|------------------------------------------|
| 0x00   | 4    | uint32 | Material parameters offset                |
| 0x04   | 4    | uint32 | Unknown                                   |
| 0x08   | 4    | uint32 | Unknown                                   |
| 0x0C   | 4    | uint32 | Unknown                                   |
| 0x10   | 4    | uint32 | Texture commands offset                   |
| 0x14   | 4    | uint32 | Texture commands word count               |
| 0x18   | 4    | uint32 | Material mapper offset (bc >= 0x21)       |
| 0x1C   | 4    | uint32 | **name0** -- texture name for unit 0 (string pointer) |
| 0x20   | 4    | uint32 | **name1** -- texture name for unit 1 (string pointer) |
| 0x24   | 4    | uint32 | **name2** -- texture name for unit 2 (string pointer) |
| 0x28   | 4    | uint32 | **material name** (string pointer)        |

**This is the critical texture binding:** `material.name0`, `material.name1`, `material.name2`
reference texture names that match entries in the textures section. This is how the renderer
knows which texture image to apply to a given material.

#### BCH Mesh/Object Entry

Each mesh object entry (0x38 bytes):

| Offset | Size | Type   | Description                               |
|--------|------|--------|-------------------------------------------|
| 0x00   | 2    | uint16 | **materialId** -- index into materials table |
| 0x02   | 2    | uint16 | Flags (bit 0 = isSilhouette for bc != 8)  |
| 0x04   | 2    | uint16 | nodeId (for visibility and naming)         |
| 0x06   | 2    | uint16 | Render priority                            |
| 0x08   | 4    | uint32 | VSH attributes buffer commands offset      |
| 0x0C   | 4    | uint32 | VSH attributes buffer commands word count  |
| 0x10   | 4    | uint32 | Faces header offset                        |
| 0x14   | 4    | uint32 | Faces header entries                       |
| 0x18   | 4    | uint32 | VSH extra attributes buffer commands offset |
| 0x1C   | 4    | uint32 | VSH extra attributes buffer commands word count |
| 0x20   | 12   | float3 | Center vector (bounding)                   |
| 0x2C   | 4    | uint32 | Flags offset                               |
| 0x30   | 4    | uint32 | Padding (0)                                |
| 0x34   | 4    | uint32 | Bounding box offset                        |

**Key insight:** `materialId` directly indexes the materials table. This is the material-to-mesh binding in BCH (simpler than GfModel's name-based binding).

#### BCH Vertex Parsing (Uniform Scaling)

BCH uses VSH (Vertex Shader) float uniforms for scaling vertex attributes:
- **Register 6:** Position offset (float4: x, y, z, w)
- **Register 7:** Attribute scales (in pop order):
  - texture0Scale, texture1Scale, texture2Scale
  - boneWeightScale, positionScale, normalScale
  - tangentScale, colorScale

All vertex positions are computed as: `(raw_value * positionScale) + positionOffset`
All normals: `raw_value * normalScale`
All UVs: `raw_value * textureNScale`
Colors: `raw_value * colorScale * 255`
Bone weights: `raw_value * boneWeightScale`

---

### 1.4 GfTexture Format

**Source reference:** `Ohana3DS Rebirth/Ohana/Textures/PocketMonsters/GfTexture.cs`

#### Header

| Offset | Size | Type    | Description                                     |
|--------|------|---------|-------------------------------------------------|
| 0x00   | 8    | ...     | Unknown (skipped)                                |
| 0x08   | 7    | char[7] | Magic: `"texture"` (7 ASCII bytes)               |
| 0x18   | 4    | int32   | Texture data length (pixel data size in bytes)   |
| 0x28   | 0x40 | char[64]| Texture name (null-terminated, 0x40 bytes)       |
| 0x68   | 2    | uint16  | Width (pixels)                                   |
| 0x6A   | 2    | uint16  | Height (pixels)                                  |
| 0x6C   | 2    | uint16  | Texture format code (see below)                  |
| 0x6E   | 2    | uint16  | Mipmap count                                     |
| 0x70   | 16   | ...     | Unknown (0x10 bytes, skipped)                    |
| 0x80   | N    | byte[]  | Raw pixel data (N = texLength bytes)             |

#### GfTexture Format Codes

GfTexture uses its own format numbering that maps to PICA200 standard formats:

| GfTex Code | PICA Format   | Description                           |
|------------|---------------|---------------------------------------|
| 0x02       | rgb565        | 16-bit RGB (5-6-5)                    |
| 0x03       | rgb8          | 24-bit RGB                            |
| 0x04       | rgba8         | 32-bit RGBA                           |
| 0x16       | rgba4         | 16-bit RGBA (4-4-4-4)                 |
| 0x17       | rgba5551      | 16-bit RGBA (5-5-5-1)                 |
| 0x23       | la8           | 16-bit Luminance+Alpha                |
| 0x24       | hilo8         | 16-bit HiLo (normal map)             |
| 0x25       | l8            | 8-bit Luminance                       |
| 0x26       | a8            | 8-bit Alpha                           |
| 0x27       | la4           | 8-bit Luminance+Alpha (4+4)          |
| 0x28       | l4            | 4-bit Luminance                       |
| 0x29       | a4            | 4-bit Alpha                           |
| 0x2A       | etc1          | ETC1 compressed                       |
| 0x2B       | etc1a4        | ETC1 with separate 4-bit alpha        |

**Pokemon Sun/Moon commonly uses:** ETC1, ETC1A4, RGBA8, and RGB8 for model textures.

---

### 1.5 PICA200 GPU Command Buffer

**Source reference:** `Ohana3DS Rebirth/Ohana/Models/PICA200/PICACommandReader.cs`

The PICA200 is the 3DS GPU. Both BCH and GfModel formats embed PICA200 command buffers that
define vertex attribute layouts, texture configurations, and rendering state.

#### Command Buffer Structure

Commands are read as pairs of 32-bit words:

| Word   | Bits      | Description                                |
|--------|----------|--------------------------------------------|
| Word 0 | [31:0]   | Parameter value                             |
| Word 1 | [15:0]   | Command ID (register address)               |
| Word 1 | [19:16]  | Write mask                                  |
| Word 1 | [30:20]  | Extra parameters count                      |
| Word 1 | [31]     | Consecutive writing mode                    |

If consecutive writing is set, the command ID increments for each extra parameter.
Otherwise, all extra parameters write to the same register.

Commands are 8-byte aligned (padded with 0x00000000 words).

#### Key PICA200 Registers

| Register | Name                              | Purpose                         |
|----------|-----------------------------------|---------------------------------|
| 0x0082   | texUnit0Size                      | Texture 0 dimensions            |
| 0x0085   | texUnit0Address                   | Texture 0 data address          |
| 0x008E   | texUnit0Type                      | Texture 0 pixel format          |
| 0x0200   | vshAttributesBufferAddress        | Base vertex buffer address      |
| 0x0201   | vshAttributesBufferFormatLow      | Attribute formats (low 32 bits) |
| 0x0202   | vshAttributesBufferFormatHigh     | Attribute formats (high 32 bits)|
| 0x0203   | vshAttributesBuffer0Address       | Buffer 0 offset                 |
| 0x0204   | vshAttributesBuffer0Permutation   | Buffer 0 attribute ordering     |
| 0x0205   | vshAttributesBuffer0Stride        | Buffer 0 stride + total attrs   |
| 0x0227   | indexBufferConfig                 | Index buffer address + format   |
| 0x0228   | indexBufferTotalVertices           | Total indexed vertices          |
| 0x0242   | vshTotalAttributes                | Total vertex attributes - 1     |
| 0x02BB   | vshAttributesPermutationLow       | Main permutation (low 32 bits)  |
| 0x02BC   | vshAttributesPermutationHigh      | Main permutation (high 32 bits) |
| 0x02C0   | vshFloatUniformConfig             | Float uniform register select   |
| 0x02C1   | vshFloatUniformData               | Float uniform data              |

#### Attribute Format Encoding

The format registers encode 23 attribute formats in a 64-bit value (4 bits each):
- Bits [1:0] = format type (0=sbyte, 1=ubyte, 2=short, 3=float)
- Bits [3:2] = component count minus 1 (0=1, 1=2, 2=3, 3=4)

#### Buffer Stride Encoding

Register `vshAttributesBuffer0Stride` encodes:
- Bits [7:0]: unused
- Bits [15:8]: stride in bytes
- Bits [47:16]: buffer permutation (low 32 bits, combined with Permutation register)
- Bits [31:28]: total attributes for this buffer

#### Index Buffer Config

Register `indexBufferConfig`:
- Bit [31]: format (0 = unsigned byte, 1 = unsigned short)
- Bits [30:0]: buffer address

---

## Part 2: Texture Codec Reference

**Source reference:** `Ohana3DS Rebirth/Ohana/TextureCodec.cs`

### 2.1 PICA200 Tile Order (Morton/Z-Order)

All PICA200 textures use 8x8 pixel tiles arranged in Z-order (Morton curve). The tile
ordering table maps linear pixel indices (0-63) to positions within an 8x8 block:

```
tileOrder = [
    0,  1,  8,  9,  2,  3, 10, 11,
   16, 17, 24, 25, 18, 19, 26, 27,
    4,  5, 12, 13,  6,  7, 14, 15,
   20, 21, 28, 29, 22, 23, 30, 31,
   32, 33, 40, 41, 34, 35, 42, 43,
   48, 49, 56, 57, 50, 51, 58, 59,
   36, 37, 44, 45, 38, 39, 46, 47,
   52, 53, 60, 61, 54, 55, 62, 63
]
```

For every format, the pixel position within the tile is:
```
x = tileOrder[pixel] % 8
y = tileOrder[pixel] / 8
output_offset = ((tileX * 8 + x) + (tileY * 8 + y) * width) * 4
```

The image is divided into 8x8 tiles, iterated left-to-right, top-to-bottom.

### 2.2 Format Decoders

#### RGBA8 (format 0x00 / GfTex 0x04)

- 4 bytes per pixel per tile element
- Byte order: A, R, G, B (input) -> R, G, B, A (output RGBA)
- `output[offset+0..2] = data[dataOffset+1..3]` (RGB)
- `output[offset+3] = data[dataOffset]` (Alpha)

#### RGB8 (format 0x01 / GfTex 0x03)

- 3 bytes per pixel
- Byte order: B, G, R (input) -> R, G, B (output)
- Copy 3 bytes directly, set alpha = 0xFF

#### RGBA5551 (format 0x02 / GfTex 0x17)

- 2 bytes per pixel (little-endian uint16)
- Bit layout: `A BBBBB GGGGG RRRRR` (bit 0 = alpha)
- R = bits[1:5] << 3, expand: R | (R >> 5)
- G = bits[6:10] << 3, expand: G | (G >> 5)
- B = bits[11:15] << 3, expand: B | (B >> 5)
- A = bit[0] * 0xFF

#### RGB565 (format 0x03 / GfTex 0x02)

- 2 bytes per pixel (little-endian uint16)
- Bit layout: `BBBBB GGGGGG RRRRR`
- R = bits[0:4] << 3, expand: R | (R >> 5)
- G = bits[5:10] << 2, expand: G | (G >> 6)
- B = bits[11:15] << 3, expand: B | (B >> 5)
- A = 0xFF

#### RGBA4 (format 0x04 / GfTex 0x16)

- 2 bytes per pixel (little-endian uint16)
- Bit layout: `BBBB GGGG RRRR AAAA`
- R = bits[4:7], expand: R | (R << 4)
- G = bits[8:11], expand: G | (G << 4)
- B = bits[12:15], expand: B | (B << 4)
- A = bits[0:3], expand: A | (A << 4)

#### LA8 / HiLo8 (format 0x05/0x06 / GfTex 0x23/0x24)

- 2 bytes per pixel
- Byte 0 = luminance (replicated to R, G, B)
- Byte 1 = alpha
- Both LA8 and HiLo8 are decoded identically in Ohana

#### L8 (format 0x07 / GfTex 0x25)

- 1 byte per pixel
- Value replicated to R, G, B; alpha = 0xFF

#### A8 (format 0x08 / GfTex 0x26)

- 1 byte per pixel
- R = G = B = 0xFF; alpha = byte value

#### LA4 (format 0x09 / GfTex 0x27)

- 1 byte per pixel
- High nibble = luminance (replicated to R, G, B)
- Low nibble = alpha

#### L4 (format 0x0A / GfTex 0x28)

- 4 bits per pixel (2 pixels per byte)
- Alternating nibbles: low nibble first, then high nibble
- Value expanded: `(nibble << 4) | nibble`
- Alpha = 0xFF

#### A4 (format 0x0B / GfTex 0x29)

- 4 bits per pixel (2 pixels per byte)
- R = G = B = 0xFF
- Alpha from nibble, expanded: `(nibble << 4) | nibble`

#### ETC1 (format 0x0C / GfTex 0x2A)

ETC1 (Ericsson Texture Compression 1) is a block-based compressed format.

- 4x4 pixel blocks, 8 bytes per block (64 bits)
- Blocks are first decoded, then unscrambled using a tile reordering pattern

**ETC1 block decoding:**

The 8-byte block is stored reversed (byte 7 first, byte 0 last).

Block structure (64 bits, after byte reversal):
- `blockTop` (uint32, first 4 bytes) - color data and config
- `blockBottom` (uint32, last 4 bytes) - pixel indices

`blockTop` fields:
- Bit 24: flip (0 = 2x4 vertical split, 1 = 4x2 horizontal split)
- Bit 25: difference mode (1 = differential, 0 = individual)

**Individual mode (difference=0):**
- R1 = bits[4:7] << 4, expand: R1 | (R1 >> 4)
- G1 = bits[12:15] << 4, expand: G1 | (G1 >> 4)
- B1 = bits[20:23] << 4, expand: B1 | (B1 >> 4)
- R2 = bits[0:3] << 4, expand: R2 | (R2 >> 4)
- G2 = bits[8:11] << 4, expand: G2 | (G2 >> 4)
- B2 = bits[16:19] << 4, expand: B2 | (B2 >> 4)

**Differential mode (difference=1):**
- R1 = bits[3:7] (5 bits), expand: R1 | (R1 >> 5)
- G1, B1 similarly
- R2 = R1 + sign_extend_3bit(bits[0:2]), then: (R2 << 3) | (R2 >> 2)
- G2, B2 similarly

**Intensity modifier table (ETC1 LUT):**

| Index | Modifier values         |
|-------|------------------------|
| 0     | +2, +8, -2, -8         |
| 1     | +5, +17, -5, -17       |
| 2     | +9, +29, -9, -29       |
| 3     | +13, +42, -13, -42     |
| 4     | +18, +60, -18, -60     |
| 5     | +24, +80, -24, -80     |
| 6     | +33, +106, -33, -106   |
| 7     | +47, +183, -47, -183   |

Table selection:
- table1 = bits[29:31] of blockTop
- table2 = bits[26:28] of blockTop

Per pixel, the modifier index comes from 2 bits spread across blockBottom:
- MSB from one position, LSB from another (see pixel indexing formula in code)
- Modifier is added to base R, G, B and clamped to [0, 255]

**ETC1 tile scramble:** After decoding all 4x4 blocks linearly, they must be rearranged
using a Z-order-like scramble pattern that groups blocks into pairs and rows.

#### ETC1A4 (format 0x0D / GfTex 0x2B)

Same as ETC1 but with a separate 8-byte alpha block prepended:
- 16 bytes total per 4x4 block
- First 8 bytes: 4-bit alpha per pixel (same nibble toggle pattern as A4/L4)
- Last 8 bytes: ETC1 color block (byte-reversed as above)
- Alpha nibble expanded: `(nibble << 4) | nibble`

### 2.3 Formats Used by Pokemon Sun/Moon

Based on the GfTexture format codes observed in Pokemon Sun/Moon models, the most commonly
encountered texture formats are:

1. **ETC1A4 (0x2B)** -- Most common; diffuse textures with alpha transparency
2. **ETC1 (0x2A)** -- Opaque diffuse textures
3. **RGBA8 (0x04)** -- High-quality textures (rare, large)
4. **RGB8 (0x03)** -- Opaque high-quality textures
5. **A8 (0x26)** -- Alpha masks
6. **L8 (0x25)** -- Grayscale maps (normal map channels, etc.)

**Minimum viable decoder set:** ETC1, ETC1A4, RGBA8, and A8 cover the vast majority of
Pokemon Sun/Moon model textures.

---

## Part 3: Modernization Plan

### Technology Choice: .NET 8 + Modern C#

Using .NET 8 with modern C# (12+) because:
- Same language family as the reference code makes format translation easier
- Modern features: `Span<byte>`, `BinaryPrimitives`, records, pattern matching
- Cross-platform (Windows/Linux/Mac)
- `System.IO.Pipelines` for efficient binary I/O
- No WinForms dependency -- pure console app
- `SkiaSharp` or `ImageSharp` for PNG encoding (no System.Drawing)

---

### Phase 1: Project Scaffold + GARC Extraction

**Goal:** Standalone .NET 8 console app that reads GARC archives and extracts sub-files.

#### Deliverables
1. Solution file `Ohana3DS-CLI.sln`
2. Console project `Ohana3DS.CLI` (entry point)
3. Library project `Ohana3DS.Formats` (all format parsers)
4. GARC reader that extracts individual files from a GARC archive
5. Auto-detection of file type (GARC vs raw model) by magic bytes
6. Unit test project `Ohana3DS.Tests`

#### Key Technical Decisions
- Use `Span<byte>` and `BinaryPrimitives` instead of `BinaryReader` for performance
- Use `record` types for parsed headers (immutable by default)
- Use `IAsyncEnumerable<FileEntry>` for streaming extraction
- No LZ11 decompression initially (sun-moon dump files are pre-extracted)
- File type detection: check first 4 bytes for "GARC" magic, or check for GfModel/BCH signatures

#### Files to Create
```
src/
  Ohana3DS.CLI/
    Program.cs              -- CLI entry point (System.CommandLine)
    Ohana3DS.CLI.csproj
  Ohana3DS.Formats/
    Containers/
      GarcReader.cs          -- GARC archive parser
      GarcHeader.cs          -- GARC header records
    Detection/
      FormatDetector.cs      -- Magic byte detection
    Ohana3DS.Formats.csproj
tests/
  Ohana3DS.Tests/
    Containers/
      GarcReaderTests.cs
    Ohana3DS.Tests.csproj
Ohana3DS-CLI.sln
```

#### Testing
- Extract files from a known GARC archive in the sun-moon dump
- Verify extracted file count matches expected
- Verify extracted bytes match direct file reads
- Verify magic byte detection correctly identifies GfModel vs BCH vs unknown

---

### Phase 2: GfModel Parser + Texture Decoder

**Goal:** Parse GfModel binary format into a clean intermediate model representation.
Decode GfTexture pixel data to PNG.

#### Deliverables
1. GfModel parser that reads all sections (skeleton, materials, meshes, textures)
2. Clean intermediate model types (not coupled to any format)
3. GfTexture decoder for all relevant pixel formats
4. PNG export for decoded textures
5. PICA200 command buffer reader (simplified for GfModel needs)
6. CLI command: `parse <file>` -- dump model info (mesh count, material names, texture list)

#### Key Technical Decisions
- Intermediate model representation uses records:
  ```csharp
  record ModelData(string Name, IReadOnlyList<MeshData> Meshes,
                    IReadOnlyList<MaterialData> Materials,
                    IReadOnlyList<TextureData> Textures,
                    IReadOnlyList<BoneData> Skeleton,
                    Matrix4x4 Transform);
  ```
- Use `System.Numerics.Matrix4x4` and `Vector3`/`Vector4` instead of custom types
- Texture decoding outputs `byte[]` RGBA pixel data (not Bitmap)
- Use `ImageSharp` for PNG encoding (cross-platform, no GDI+ dependency)
- PICA command reader simplified: only parse vertex attributes, index buffer, and
  texture info registers. Skip TEV stages, blending, stencil, etc.

#### Files to Create
```
src/Ohana3DS.Formats/
  Models/
    ModelData.cs             -- Intermediate model records
    MeshData.cs              -- Mesh/vertex/index records
    MaterialData.cs          -- Material with texture name bindings
    BoneData.cs              -- Skeleton bone data
  Models/GfModel/
    GfModelReader.cs         -- Top-level GfModel parser
    GfModelHeader.cs         -- GfModel header records
    GfStringTable.cs         -- String table parser
    GfMaterialParser.cs      -- Material section parser
    GfMeshParser.cs          -- Mesh section parser with PICA vertex parsing
    GfSubMeshInfo.cs         -- Sub-mesh descriptor
  Models/Pica/
    PicaCommandReader.cs     -- PICA200 GPU command buffer parser
    PicaRegisters.cs         -- Register ID constants
    PicaAttributeFormat.cs   -- Attribute format types
  Textures/
    GfTextureReader.cs       -- GfTexture parser
    TextureData.cs           -- Decoded texture record
    Codecs/
      PicaTextureDecoder.cs  -- Dispatch to format-specific decoder
      TileOrder.cs           -- Morton/Z-order tile table
      Etc1Decoder.cs         -- ETC1/ETC1A4 decoder
      RgbaDecoder.cs         -- RGBA8, RGB8, RGBA4, RGBA5551, RGB565
      GrayscaleDecoder.cs    -- L8, A8, LA8, LA4, L4, A4, HiLo8
  Textures/Export/
    PngExporter.cs           -- Write RGBA byte[] to PNG via ImageSharp
```

#### Testing
- Parse a known GfModel file from the sun-moon dump
- Assert: correct mesh count, material names match expected
- Assert: material-to-mesh bindings are resolved correctly
- Assert: texture names in materials match available textures
- Decode each texture format, compare output dimensions
- Export textures as PNG, verify they open in an image viewer
- Round-trip test: known pixel values for each codec

---

### Phase 3: DAE Exporter with Proper Multi-Material

**Goal:** Generate Collada (DAE) XML files with correct per-mesh material assignment and
texture references. Export textures as PNG files alongside the DAE.

#### Deliverables
1. DAE writer that outputs valid Collada 1.4.1 XML
2. Proper multi-material support: each mesh references its assigned material
3. Material-to-texture binding via library_images and library_effects
4. Texture PNG files exported alongside DAE
5. Skeleton export (joint hierarchy)
6. Fix for the SID uniqueness bug from Ohana's exporter
7. CLI command: `convert <input> <output_dir>` -- full model-to-DAE pipeline

#### Key Technical Decisions
- Use `XmlWriter` directly instead of XML serialization (more control, no attribute quirks)
- Each mesh gets its own `<geometry>` element with a `<triangles>` referencing the correct material
- Material SIDs must be unique across the entire DAE (Ohana's bug: it used `"img_surface"` and
  `"img_sampler"` for every material, which causes parsers to use only the last definition).
  Fix: use `"{materialName}_surface"` and `"{materialName}_sampler"` as SIDs
- Texture file references use relative paths: `"./{textureName}.png"`
- Vertex data: positions, normals, UVs (up to 3 sets), vertex colors
- Index data: triangle lists (no strips)
- Skeleton: `<node type="JOINT">` hierarchy with transform matrices
- Controllers: skin deformation with bind poses and vertex weights

#### DAE Material Binding Structure (Correct)
```xml
<library_images>
  <image id="tex_Body_id"><init_from>./Body.png</init_from></image>
  <image id="tex_Eye_id"><init_from>./Eye.png</init_from></image>
</library_images>

<library_effects>
  <effect id="eff_BodyMaterial_id">
    <profile_COMMON>
      <newparam sid="BodyMaterial_surface">  <!-- UNIQUE per material -->
        <surface type="2D"><init_from>tex_Body_id</init_from></surface>
      </newparam>
      <newparam sid="BodyMaterial_sampler">  <!-- UNIQUE per material -->
        <sampler2D><source>BodyMaterial_surface</source></sampler2D>
      </newparam>
      <technique sid="BodyMaterial_technique">
        <phong>
          <diffuse><texture texture="BodyMaterial_sampler" texcoord="uv"/></diffuse>
        </phong>
      </technique>
    </profile_COMMON>
  </effect>
</library_effects>

<library_materials>
  <material id="BodyMaterial_id" name="BodyMaterial">
    <instance_effect url="#eff_BodyMaterial_id"/>
  </material>
</library_materials>

<!-- In visual_scene, each mesh node binds to its material: -->
<instance_geometry url="#mesh_0_body_id">
  <bind_material>
    <technique_common>
      <instance_material symbol="BodyMaterial" target="#BodyMaterial_id"/>
    </technique_common>
  </bind_material>
</instance_geometry>
```

**The Ohana SID bug explained:** In the original DAE.cs (lines 622-623), every material
effect uses the same `sid="img_surface"` and `sid="img_sampler"`. This means in the
output XML, all `<newparam>` elements have duplicate SIDs. Collada parsers (Blender,
Three.js, Assimp) interpret this as only the last-defined surface/sampler being valid,
so all meshes end up with the same texture. The fix is to make SIDs unique per material.

#### Files to Create
```
src/Ohana3DS.Formats/
  Export/
    DaeWriter.cs             -- Collada XML writer
    DaeGeometryWriter.cs     -- Geometry source/triangles sections
    DaeMaterialWriter.cs     -- Materials/effects/images sections
    DaeSkeletonWriter.cs     -- Joint hierarchy
    DaeControllerWriter.cs   -- Skin controllers (weights/bind poses)
```

#### Testing
- Convert a known model, load resulting DAE in Blender (manual verification)
- Verify each mesh has distinct material assignments
- Verify textures are correctly referenced and display
- Validate DAE against Collada 1.4.1 schema (xmllint or online validator)
- Load in Three.js ColladaLoader to verify web compatibility
- Test multi-material model (Pokemon with body + eyes + mouth materials)
- Compare vertex positions to known values from direct binary parsing

---

### Phase 4: Bulk Pipeline + Polish

**Goal:** Production-ready CLI with batch processing, error handling, and output organization.

#### Deliverables
1. Recursive folder processing (walk sun-moon dump, find all model files)
2. Pokemon species name mapping (dex number to species name)
3. Organized output directory structure
4. Progress reporting with estimated time
5. Error handling and recovery (skip bad files, continue processing)
6. Parallel processing for throughput
7. BCH format support as fallback (optional, for X/Y compatibility)
8. OBJ export as alternative to DAE

#### Key Technical Decisions
- CLI framework: `System.CommandLine` for argument parsing
- Commands:
  ```
  ohana3ds extract <garc-file> <output-dir>     -- extract GARC contents
  ohana3ds info <model-file>                     -- dump model metadata
  ohana3ds convert <input> <output-dir>          -- convert single model to DAE+PNG
  ohana3ds batch <input-dir> <output-dir>        -- batch convert all models
  ```
- Pokemon name mapping: embedded JSON resource `{ "001": "Bulbasaur", ... }`
- Output structure: `output/{dex_number}_{species_name}/model.dae + textures/*.png`
- Parallelism: `Parallel.ForEachAsync` with configurable concurrency
- Progress: `IProgress<T>` with console progress bar
- Logging: `Microsoft.Extensions.Logging` with console and file sinks

#### Files to Create
```
src/Ohana3DS.CLI/
  Commands/
    ExtractCommand.cs
    InfoCommand.cs
    ConvertCommand.cs
    BatchCommand.cs
  Pokemon/
    SpeciesNameMap.cs         -- Dex number to name lookup
    pokemon_names.json        -- Embedded resource
  Pipeline/
    BatchProcessor.cs         -- Parallel batch conversion
    ProgressReporter.cs       -- Console progress output
src/Ohana3DS.Formats/
  Models/Bch/                 -- (Optional) BCH parser
    BchReader.cs
    BchHeader.cs
    BchMaterialParser.cs
    BchMeshParser.cs
    BchTextureParser.cs
  Export/
    ObjWriter.cs              -- OBJ/MTL export (simpler alternative)
```

#### Testing
- Batch convert 10+ models, verify all succeed
- Test error recovery: corrupt file in batch doesn't stop processing
- Verify output directory structure matches expected layout
- Performance benchmark: measure models/second, compare sequential vs parallel
- Test OBJ export loads correctly in Blender/MeshLab

---

## Part 4: Architecture

### Project Structure

```
Ohana3DS-CLI/
|
+-- src/
|   +-- Ohana3DS.CLI/                    -- Console application (entry point)
|   |   +-- Program.cs
|   |   +-- Commands/                    -- CLI command handlers
|   |   +-- Pipeline/                    -- Batch processing
|   |   +-- Pokemon/                     -- Species name mapping
|   |   +-- Ohana3DS.CLI.csproj
|   |
|   +-- Ohana3DS.Formats/               -- All format parsers (no UI dependency)
|       +-- Containers/
|       |   +-- GarcReader.cs
|       |   +-- GarcHeader.cs
|       +-- Detection/
|       |   +-- FormatDetector.cs
|       +-- Models/
|       |   +-- ModelData.cs             -- Intermediate representation
|       |   +-- MeshData.cs
|       |   +-- MaterialData.cs
|       |   +-- BoneData.cs
|       |   +-- VertexData.cs
|       |   +-- GfModel/
|       |   |   +-- GfModelReader.cs
|       |   |   +-- GfMaterialParser.cs
|       |   |   +-- GfMeshParser.cs
|       |   +-- Pica/
|       |   |   +-- PicaCommandReader.cs
|       |   |   +-- PicaRegisters.cs
|       |   +-- Bch/                     -- Phase 4 optional
|       |       +-- BchReader.cs
|       +-- Textures/
|       |   +-- GfTextureReader.cs
|       |   +-- TextureData.cs
|       |   +-- Codecs/
|       |       +-- PicaTextureDecoder.cs
|       |       +-- Etc1Decoder.cs
|       |       +-- RgbaDecoder.cs
|       |       +-- GrayscaleDecoder.cs
|       +-- Export/
|       |   +-- DaeWriter.cs
|       |   +-- ObjWriter.cs
|       |   +-- PngExporter.cs
|       +-- Ohana3DS.Formats.csproj
|
+-- tests/
|   +-- Ohana3DS.Tests/
|       +-- Containers/
|       +-- Models/
|       +-- Textures/
|       +-- Export/
|       +-- TestData/                    -- Small test fixture files
|       +-- Ohana3DS.Tests.csproj
|
+-- Ohana3DS-CLI.sln
```

### Key Types and Interfaces

```csharp
// Intermediate model representation (format-agnostic)
public record ModelData(
    string Name,
    IReadOnlyList<MeshData> Meshes,
    IReadOnlyList<MaterialData> Materials,
    IReadOnlyList<TextureData> Textures,
    IReadOnlyList<BoneData> Skeleton,
    Matrix4x4 Transform);

public record MeshData(
    string Name,
    int MaterialIndex,
    IReadOnlyList<VertexData> Vertices,
    IReadOnlyList<int> Indices,
    bool HasNormals,
    bool HasColors,
    int TexCoordCount);

public record MaterialData(
    string Name,
    string? TextureName0,
    string? TextureName1,
    string? TextureName2,
    TextureCoordinator[] Coordinators,
    TextureWrap[] WrapModes);

public record TextureData(
    string Name,
    int Width,
    int Height,
    byte[] RgbaPixels);  // Decoded to RGBA8

public record BoneData(
    string Name,
    int ParentIndex,
    Vector3 Scale,
    Vector3 Rotation,
    Vector3 Translation);

public record VertexData(
    Vector3 Position,
    Vector3? Normal,
    Vector3? Tangent,
    Vector2? TexCoord0,
    Vector2? TexCoord1,
    Vector2? TexCoord2,
    uint DiffuseColor,
    IReadOnlyList<int> BoneIndices,
    IReadOnlyList<float> BoneWeights);

// Reader interfaces
public interface IModelReader
{
    ModelData Read(ReadOnlySpan<byte> data);
    ModelData Read(Stream stream);
}

public interface IModelExporter
{
    void Export(ModelData model, string outputPath);
}

// Format detection
public static class FormatDetector
{
    public static FileFormat Detect(ReadOnlySpan<byte> header);
}

public enum FileFormat { Unknown, Garc, GfModel, Bch, GfTexture }
```

### Dependency Choices

| Dependency                | Purpose                          | Version |
|--------------------------|----------------------------------|---------|
| `System.CommandLine`     | CLI argument parsing              | 2.0+    |
| `SixLabors.ImageSharp`   | PNG encoding (cross-platform)     | 3.x     |
| `Microsoft.Extensions.Logging` | Structured logging          | 8.x     |
| `xunit` + `FluentAssertions` | Testing                      | Latest  |

**Explicitly NOT using:**
- `System.Drawing` (Windows-only, GDI+ dependency)
- Any WinForms or WPF libraries
- Newtonsoft.Json (use System.Text.Json)
- Any Ohana3DS code as a dependency

---

## Part 5: Risks and Open Questions

### Format Edge Cases

1. **Unknown sections in GfModel:** The format has unknown data blocks (0x20 bytes of
   "maybe quaternions", the unkData section, 0x80 bytes of mesh header). These are skipped
   in Ohana but may contain useful data for edge-case models. Risk: low, since Ohana
   successfully parses all observed Sun/Moon models without this data.

2. **GfModel magic detection:** Unlike BCH (which has "BCH\0"), GfModel detection relies on
   checking bytes at offset 0x10 for the "gfmodel" string. The top-level container has no
   obvious magic -- the first uint32 is skipped. Need to determine reliable detection:
   potentially check if first uint32 is 0x00010000 or 0x15122117 (noted in the task).

3. **PICA command buffer in GfModel vs BCH:** GfModel embeds raw PICA command buffers at
   fixed positions with addresses set to 0x99999999 (game engine relocates them). BCH uses
   a relocation table to patch addresses. Our GfModel parser must handle the 0x99999999
   placeholder addresses -- vertex/index data is inline after the command buffers, not at
   the addresses specified in the commands.

4. **Bone weight format override:** Both GfModel and BCH parsers override the PICA format
   type for bone weights to `unsignedByte` regardless of what the format descriptor says.
   This is a known quirk that must be preserved in the rewrite.

5. **ETC1 tile scramble:** The ETC1 decoder has a custom unscramble pass that reorders
   decoded 4x4 blocks. The scramble pattern is computed dynamically based on image
   dimensions. This is non-trivial and must be implemented exactly.

6. **Version-dependent BCH parsing:** BCH has at least 4 different relocation flag schemes
   based on backward compatibility version. If we add BCH support, we need to handle all
   variants (bc < 6, bc < 8, bc < 0x21, bc >= 0x21).

### Testing Strategy

1. **Golden file tests:** Parse known model files, compare extracted data against
   reference values obtained by running the original Ohana3DS on the same files.

2. **Texture round-trip:** For each texture codec, create small test textures with known
   pixel values, encode them (using Ohana's encoder where available, or manually construct
   binary data), then verify our decoder produces the correct output.

3. **DAE validation:** Use automated Collada schema validation plus manual Blender import
   for visual verification. Create a small test suite of models with:
   - Single material
   - Multiple materials
   - With/without skeleton
   - With/without vertex colors
   - Different texture formats

4. **Regression testing:** Keep a set of known-good DAE outputs. After changes, compare
   new outputs to golden files (ignoring timestamps and formatting whitespace).

5. **Integration test with real data:** Process 5-10 models from the sun-moon dump end-to-end
   (model file -> DAE + PNG), verify results load correctly.

### Open Questions

1. **Do we need LZ11 decompression?** The sun-moon dump appears pre-extracted, but some
   GARC sub-files may still be LZ11-compressed (detected by first byte = 0x11). Need to
   check actual files. If needed, implement a simple LZ11 decompressor.

2. **What format are the files at `a/0/3/`?** These are ~3MB files that could be the main
   Pokemon model GARCs. Need to check magic bytes to confirm.

3. **BCH support scope:** Should we support BCH for X/Y and OR/AS models in Phase 4, or
   focus solely on Sun/Moon GfModel? BCH is significantly more complex (2171 lines vs ~520
   for GfModel) and may not be needed for the current project scope.

4. **Animation support:** The current plan excludes skeletal/material animations. Ohana
   parses them extensively in BCH. GfModel may also have animation data in its sections 2-4.
   Should this be a future phase?

5. **OBJ multi-material limitations:** OBJ format supports multiple materials per file via
   `usemtl` directives, but some importers handle this poorly. DAE is the primary export
   target. OBJ is provided as a simpler fallback for tools that prefer it.

6. **Normal map handling:** HiLo8 textures appear to be normal map channels. Should the
   exporter flag materials that use normal maps differently in the DAE output? This affects
   downstream rendering in Blender/Three.js.
