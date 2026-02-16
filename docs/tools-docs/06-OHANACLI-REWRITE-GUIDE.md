# OhanaCli Rewrite Guide

> This document is the sole reference for the complete rewrite of the OhanaCli tool.
> The current implementation is SCRAPPED. This guide documents what existed, what works,
> what is broken, and how to rebuild it from scratch on .NET 8.

---

## Table of Contents

1. [Project Structure Tree](#1-project-structure-tree)
2. [API Reference for Every Class](#2-api-reference-for-every-class)
3. [Files That Can Be Directly Copied from Ohana3DS-Rebirth](#3-files-that-can-be-directly-copied-from-ohana3ds-rebirth)
4. [Phased Rewrite Plan](#4-phased-rewrite-plan)
5. [Known Bugs in Current Implementation](#5-known-bugs-in-current-implementation)
6. [Optimizations and Quick Wins](#6-optimizations-and-quick-wins)
7. [Build and Test Commands](#7-build-and-test-commands)

---

## 1. Project Structure Tree

### 1.1 Current (Broken) CLI Layout

```
src/PokemonGreen.OhanaCli/
  src/
    OhanaCli.App/                          # .NET 8 console app (entry point)
      OhanaCli.App.csproj                  # Depends: OhanaCli.Formats, System.CommandLine 2.0-beta4
      Program.cs                           # 996 lines -- all CLI commands + export logic

    OhanaCli.Formats/                      # .NET 8 class library (all parsers)
      OhanaCli.Formats.csproj              # Depends: SixLabors.ImageSharp 3.1.6, System.Drawing.Common 8.0.12

      Core/
        FileIO.cs                          # Format dispatcher (magic-byte detection)
        IOUtils.cs                         # Binary read helpers
        PatriciaTree.cs                    # PATRICIA tree for BCH name tables
        RenderBase.cs                      # ALL internal data structures (2296 lines)

      Compressions/
        BLZ.cs                             # Backward LZ77
        LZSS.cs                            # LZSS
        LZSS_Ninty.cs                      # Nintendo LZ11

      Containers/
        GARC.cs                            # Game ARChive reader
        OContainer.cs                      # Container data structure
        PkmnContainer.cs                   # Generic 2-letter Pokemon container

      Models/
        BCH/
          BCH.cs                           # BCH format loader (2171 lines)
        GenericFormats/
          DAE.cs                           # Collada XML exporter (1219 lines)
          OBJ.cs                           # Wavefront OBJ exporter/importer
        Mesh/
          MeshUtils.cs                     # Mesh optimization, bounds
        PICA200/
          PICACommand.cs                   # PICA200 GPU register constants
          PICACommandReader.cs             # PICA200 command buffer parser
        PocketMonsters/
          CM.cs                            # CM container loader
          CP.cs                            # CP container loader
          GfModel.cs                       # Sun/Moon model loader (552 lines)
          GfMotion.cs                      # Animation loader
          GR.cs                            # GR map model loader
          MM.cs                            # MM chibi model loader
          PC.cs                            # PC container loader

      Textures/
        Codecs/
          TextureCodec.cs                  # Full texture decoder (all PICA200 formats)
        PocketMonsters/
          AD.cs                            # AD map texture container
          GfTexture.cs                     # Game Freak texture loader
          PT.cs                            # PT monster texture container
        TextureUtils.cs                    # Bitmap <-> byte[] conversion
```

### 1.2 Original Ohana3DS-Rebirth Layout (Reference)

```
Ohana3DS Rebirth/
  Ohana/
    FileIO.cs                              # Format dispatcher (superset of CLI)
    IOUtils.cs
    PatriciaTree.cs
    RenderBase.cs
    RenderEngine.cs                        # OpenGL renderer (NOT needed for CLI)
    TextureCodec.cs
    TextureUtils.cs
    AnimationUtils.cs                      # Animation interpolation helpers

    Animations/
      BS.cs                                # BS animation format
      GfMotion.cs                          # Game Freak animation

    Compressions/
      BLZ.cs
      LZSS.cs
      LZSS_Ninty.cs
      Yaz0.cs                              # Yaz0 decompression (not ported)

    Containers/
      DARC.cs                              # DARC container (not ported)
      DQVIIPack.cs                         # DQ VII container (not ported)
      FPT0.cs                              # FPT0 container (not ported)
      GARC.cs
      OContainer.cs
      PkmnContainer.cs
      SARC.cs                              # SARC container (not ported)

    Models/
      BCH.cs
      CGFX.cs                              # CGFX format (not ported)
      MBN.cs                               # MBN format (not ported)
      NLP.cs                               # New Love Plus (not ported)
      ZMDL.cs                              # Zelda model (not ported)
      MeshUtils.cs
      GenericFormats/
        CMDL.cs                            # CMDL exporter (not ported)
        DAE.cs
        OBJ.cs
        SMD.cs                             # SMD exporter (not ported)
      NewLovePlus/                         # (not ported)
        Mesh.cs
        Model.cs
        Serialization.cs
      PICA200/
        PICACommand.cs
        PICACommandReader.cs
        PICACommandWriter.cs               # Command writer (not ported)
      PocketMonsters/
        CM.cs
        CP.cs
        GfModel.cs
        GR.cs
        MM.cs
        PC.cs

    Textures/
      3DST.cs                              # 3DST texture (not ported)
      BCLIM.cs                             # BCLIM texture (not ported)
      DMP.cs                               # DMP texture (not ported)
      ZTEX.cs                              # Zelda texture (not ported)
      PocketMonsters/
        AD.cs
        GfTexture.cs
        PT.cs
```

---

## 2. API Reference for Every Class

> For each class: full qualified name, namespace, accessibility, all public/internal method
> signatures with parameter types and return types, all fields/properties, purpose, and
> dependencies. NO implementation code.

---

### 2.1 OhanaCli.App -- Program.cs

**Namespace:** (top-level / global)
**Purpose:** CLI entry point. Defines all commands, orchestrates GARC iteration, model/texture grouping, and export to DAE/OBJ/PNG.

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `Main(string[] args)` | `Task<int>` | Entry point, builds System.CommandLine root command |
| `InfoHandler(FileInfo file)` | `void` | Prints format info for a single file |
| `ConvertHandler(FileInfo file, DirectoryInfo outputDir, string format)` | `void` | Converts a single file (GARC/PC/BCH) to DAE/OBJ |
| `BatchHandler(DirectoryInfo inputDir, DirectoryInfo outputDir, string format)` | `void` | Batch converts a directory of files |
| `ConvertAllHandler(DirectoryInfo garcDir, DirectoryInfo outputDir, string format)` | `void` | Mass-converts all GARCs, groups model+texture entries |
| `DiagnoseHandler(FileInfo file, int entryStart, int entryEnd)` | `void` | Hex dumps GARC entries with header analysis |
| `ReadEntryData(OContainer container, OContainer.fileEntry entry)` | `byte[]` | Reads lazy-loaded GARC entry data from disk |
| `ExportModel(RenderBase.OModelGroup model, string outputDir, string baseName, string format)` | `void` | Exports a single model group to DAE/OBJ + textures as PNG |
| `DeriveFolderName(RenderBase.OModelGroup model, List<RenderBase.OModelGroup> texGroups, int garcIndex)` | `string` | Derives output folder name from model/texture names |
| `ExportModelGrouped(RenderBase.OModelGroup model, List<RenderBase.OModelGroup> texGroups, string outputDir, string baseName, string format)` | `void` | Merges texture groups into model, then calls ExportModel |
| `DumpPcSections(byte[] data, string label)` | `void` | Debug: dumps PC container section headers |
| `DumpGfModelHeader(byte[] data, string label)` | `void` | Debug: dumps GfModel container header |
| `DumpLoadModelHeader(byte[] data, string label)` | `void` | Debug: dumps loadModel sub-header |
| `DumpGfModelDirectHeader(byte[] data, string label)` | `void` | Debug: dumps direct GfModel header |

#### Inner Class: GarcStats

| Field | Type | Purpose |
|-------|------|---------|
| `Name` | `string` | GARC file name |
| `Converted` | `int` | Successful conversions |
| `Textures` | `int` | Textures extracted |
| `Errors` | `int` | Error count |
| `ErrorMessages` | `List<string>` | Error details |
| `SkipReasons` | `List<string>` | Skip reasons |
| `AddError(string)` | `void` | Adds error + increments count |
| `AddSkip(string)` | `void` | Adds skip reason |

#### Dependencies
- `System.CommandLine` (2.0-beta4)
- `OhanaCli.Formats` (all format classes)

---

### 2.2 Core/FileIO.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `FileIO` (public)
**Purpose:** Central format dispatcher. Detects file format by magic bytes and routes to the appropriate loader.

#### Enums

| Name | Type | Values |
|------|------|--------|
| `formatType` | `[Flags] uint` | `unsupported=0`, `compression=1<<0`, `container=1<<1`, `image=1<<2`, `model=1<<3`, `texture=1<<4`, `anims=1<<5`, `all=0xffffffff` |
| `fileType` | `enum` | `none`, `model`, `texture`, `skeletalAnimation`, `materialAnimation`, `visibilityAnimation` |

#### Structs

| Name | Fields | Purpose |
|------|--------|---------|
| `LoadedFile` | `object data`, `formatType type` | Result of loading any file |

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `load(string fileName)` | `LoadedFile` | Load by file name (dispatches by extension, then stream) |
| `load(Stream data)` | `LoadedFile` | Load by stream (magic-byte detection chain) |
| `getExtension(byte[] data, int startIndex = 0)` | `string` | Determines file extension from magic bytes |
| `getMagic(byte[] data, int length, int startIndex = 0)` | `string` | Reads ASCII magic from byte array |

#### Private Static Methods

| Signature | Returns |
|-----------|---------|
| `peek(BinaryReader input)` | `uint` |
| `getMagic(BinaryReader input, uint length)` | `string` |

#### Format Detection Chain (in order)
1. Extension dispatch: `.mbn`, `.xml` (commented out)
2. 4-byte uint magic: `0x00010000` -> GfModel, `0x00060000` -> GfMotion, `0x15041213` -> GfTexture, `0x15122117` -> GfModel (single)
3. 7-char magic: `"texture"` (commented out)
4. 5-char magic: `"MODEL"` (commented out)
5. 4-char magic: `"CRAG"` -> GARC, `"IECP"` -> LZSS decompress (many others commented out)
6. 3-char magic: `"BCH"` -> BCH
7. 2-char magic: `"AD"`, `"BM"`, `"CM"`, `"CP"`, `"GR"`, `"MM"`, `"PC"`, `"PT"` -> respective loaders
8. Uppercase 2-char fallback -> PkmnContainer
9. Compression byte: `0x11` -> LZSS_Ninty, `0x90` -> BLZ

---

### 2.3 Core/IOUtils.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `IOUtils` (internal)
**Purpose:** Low-level binary reading helpers.

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `readString(BinaryReader input, uint address, bool movePosition = false)` | `string` | Read null-terminated ASCII string at address |
| `readString(BinaryReader input, uint address, uint length)` | `string` | Read fixed-length ASCII string |
| `readStringWithLength(BinaryReader input, uint maxLength)` | `string` | Read null-terminated string up to maxLength |
| `signExtend(uint value, int bits)` | `int` | Sign-extend unsigned value |
| `signExtend(int value, int bits)` | `int` | Sign-extend signed value |
| `endianSwap(uint value)` | `uint` | Byte-swap 32-bit value |

---

### 2.4 Core/PatriciaTree.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `PatriciaTree` (internal)
**Purpose:** Builds a PATRICIA trie from a list of strings. Used by BCH for name table serialization.

#### Inner Class: `node`

| Field | Type |
|-------|------|
| `index` | `int` |
| `referenceBit` | `int` |
| `name` | `string` |
| `left` | `node` |
| `right` | `node` |

#### Fields

| Name | Type |
|------|------|
| `nodeCount` | `int` |
| `maxLength` | `int` |
| `nodes` | `List<node>` |
| `rootNode` | `node` |

#### Constructor
- `PatriciaTree(List<string> names)`

---

### 2.5 Core/RenderBase.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `RenderBase` (public)
**Purpose:** ALL internal data structures used throughout the pipeline. This is the central data model.

#### Vector/Matrix Types

| Class | Fields | Notes |
|-------|--------|-------|
| `OVector2` | `float x, y` | 2D vector |
| `OVector3` | `float x, y, z` | 3D vector with `transform(OVector3, OMatrix)` static |
| `OVector4` | `float x, y, z, w` | 4D vector |
| `OMatrix` | `float M11..M44` (4x4) | Operators: `*`, methods: `invert()`, statics: `scale()`, `rotateX/Y/Z()`, `translate()` |

#### Vertex/Mesh Types

| Class | Key Fields |
|-------|------------|
| `OVertex` (IEquatable) | `position: OVector3`, `normal: OVector3`, `tangent: OVector3`, `texture0/1/2: OVector2`, `diffuseColor: uint`, `node: List<int>`, `weight: List<float>` |
| `OMesh` | `vertices: List<OVertex>`, `name: string`, `materialId: ushort`, `renderPriority: ushort`, `hasNormal/hasTangent/hasColor/hasNode/hasWeight: bool`, `texUVCount: byte`, `isVisible: bool`, `boundingBox: OOrientedBoundingBox` |
| `OOrientedBoundingBox` | `name: string`, `centerPosition: OVector3`, `orientationMatrix: OMatrix`, `size: OVector3` |

#### Skeleton

| Class | Key Fields |
|-------|------------|
| `OBone` | `name: string`, `parentId: short`, `scale/rotation/translation: OVector3`, `absoluteScale: OVector3`, `invTransform: OMatrix`, `billboardMode: OBillboardMode`, `isSegmentScaleCompensate: bool`, `userData: List<OMetaData>` |

#### Material System (extensive)

| Class | Key Fields |
|-------|------------|
| `OMaterial` | `name/name0/name1/name2: string`, `textureMapper[3]: OTextureMapper`, `textureCoordinator[3]: OTextureCoordinator`, `materialColor: OMaterialColor`, `rasterization: ORasterization`, `fragmentShader: OFragmentShader`, `fragmentOperation: OFragmentOperation`, `lightSetIndex/fogIndex: ushort`, flags, `shaderReference/modelReference: OReference`, `userData: List<OMetaData>` |
| `OMaterialColor` | `emission/ambient/diffuse/specular0/specular1/constant0..5: Color`, `colorScale: float` |
| `OTextureMapper` | `wrapU/wrapV: OTextureWrap`, `magFilter: OTextureMagFilter`, `minFilter: OTextureMinFilter`, `minLOD: uint`, `LODBias: float`, `borderColor: Color` |
| `OTextureCoordinator` | `projection: OTextureProjection`, `referenceCamera: uint`, `scaleU/scaleV/rotate/translateU/translateV: float` |
| `OFragmentShader` | `textureCombiner[6]: OTextureCombiner`, `bufferColor: Color`, `bump: OFragmentBump`, `lighting: OFragmentLighting`, `alphaTest: OAlphaTest`, `layerConfig: uint` |
| `OFragmentOperation` | `blend: OBlendOperation`, `stencil: OStencilOperation`, `depth: ODepthOperation` |

#### Texture

| Class | Key Fields |
|-------|------------|
| `OTexture` | `texture: Bitmap`, `name: string` |
| `OTextureFormat` (enum) | `rgba8`, `rgb8`, `rgba5551`, `rgb565`, `rgba4`, `la8`, `hilo8`, `l8`, `a8`, `la4`, `l4`, `a4`, `etc1`, `etc1a4`, `dontCare` |

#### Model/Group

| Class | Key Fields |
|-------|------------|
| `OModel` | `mesh: List<OMesh>`, `material: List<OMaterial>`, `skeleton: List<OBone>`, `name: string`, `transform: OMatrix`, `minVector/maxVector: OVector3`, `userData: List<OMetaData>` |
| `OModelGroup` | `model: List<OModel>`, `texture: List<OTexture>`, `lookUpTable: List<OLookUpTable>`, `light: List<OLight>`, `camera: List<OCamera>`, `fog: List<OFog>`, `skeletalAnimation/materialAnimation/visibilityAnimation/lightAnimation/cameraAnimation/fogAnimation: OAnimationListBase`, `scene: List<OScene>` |
| `OModelGroup.merge(OModelGroup)` | Merges models, textures, animations from another group |

#### Animation Types

| Class | Key Fields |
|-------|------------|
| `OAnimationKeyFrame` | `frame: float`, `value: float`, `inSlope: float`, `outSlope: float`, `bValue: bool` |
| `OAnimationKeyFrameGroup` | `keyFrames: List<OAnimationKeyFrame>`, `interpolation: OInterpolationMode`, `exists: bool`, `startFrame/endFrame: float`, `defaultValue: bool` |
| `OSkeletalAnimation` | `name: string`, `frameSize: float`, `loopMode: OLoopMode`, `bone: List<OSkeletalAnimationBone>` |
| `OSkeletalAnimationBone` | `name: string`, `rotationX/Y/Z: OAnimationKeyFrameGroup`, `translationX/Y/Z: OAnimationKeyFrameGroup`, `isFrameFormat/isFullBakedFormat: bool`, `scale/rotationQuaternion/translation: OAnimationFrame`, `transform: List<OMatrix>` |
| `OMaterialAnimation` | `data: List<OMaterialAnimationData>`, `textureName: List<string>` |
| `OVisibilityAnimation` | `data: List<OVisibilityAnimationData>` |
| `OLightAnimation` | `data: List<OLightAnimationData>`, `lightType/lightUse` |
| `OCameraAnimation` | `data: List<OCameraAnimationData>`, `viewMode/projectionMode` |
| `OFogAnimation` | `data: List<OFogAnimationData>` |

#### Key Enums (partial list)

- `OTranslucencyKind`, `OSkinningMode` (none, rigidSkinning, smoothSkinning)
- `OBillboardMode`, `OCullMode`, `OTextureWrap`, `OBlendMode`, `OBlendFunction`
- `OTestFunction`, `OStencilOp`, `OSegmentType`, `OInterpolationMode`
- `OSegmentQuantization` (hermite128/64/48, unifiedHermite96/48/32, stepLinear64/32)
- `ORepeatMethod`, `OLoopMode`, `OMetaDataValueType`

---

### 2.6 Compressions/BLZ.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Compressions`
**Class:** `BLZ` (internal)
**Purpose:** Backward LZ77 decompression (used in some 3DS containers).

| Signature | Returns |
|-----------|---------|
| `decompress(Stream data)` | `static byte[]` |

---

### 2.7 Compressions/LZSS.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Compressions`
**Class:** `LZSS` (internal)
**Purpose:** Standard LZSS decompression (IECP containers).

| Signature | Returns |
|-----------|---------|
| `decompress(Stream data, uint decompressedLength)` | `static byte[]` |

---

### 2.8 Compressions/LZSS_Ninty.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Compressions`
**Class:** `LZSS_Ninty` (public)
**Purpose:** Nintendo LZ11 decompression.

| Signature | Returns |
|-----------|---------|
| `decompress(byte[] data)` | `static byte[]` |
| `decompress(Stream data, uint decompressedLength)` | `static byte[]` |

---

### 2.9 Containers/OContainer.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Containers`
**Class:** `OContainer` (public)
**Purpose:** Generic container data structure for archive contents.

#### Inner Struct: `fileEntry`

| Field | Type | Purpose |
|-------|------|---------|
| `name` | `string` | Entry name |
| `data` | `byte[]` | Entry data (null if lazy-loaded) |
| `loadFromDisk` | `bool` | True if entry data is lazy |
| `fileOffset` | `uint` | Offset in archive stream |
| `fileLength` | `uint` | Length in archive stream |
| `doDecompression` | `bool` | Whether to decompress on read |

#### Fields

| Name | Type |
|------|------|
| `data` | `Stream` |
| `content` | `List<fileEntry>` |

---

### 2.10 Containers/GARC.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Containers`
**Class:** `GARC` (internal)
**Purpose:** Reads GARC (Game ARChive) files. Magic: "CRAG" (little-endian). Contains FATO (offsets) and FATB (metadata) sections.

| Signature | Returns | Notes |
|-----------|---------|-------|
| `load(string fileName)` | `static OContainer` | |
| `load(Stream data)` | `static OContainer` | Sets `loadFromDisk=true` for lazy loading |

---

### 2.11 Containers/PkmnContainer.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Containers`
**Class:** `PkmnContainer` (public)
**Purpose:** Generic Pokemon 2-letter container reader. Header: 2-byte magic, section count, section offsets/lengths.

| Signature | Returns |
|-----------|---------|
| `load(string fileName)` | `static OContainer` |
| `load(Stream data)` | `static OContainer` |

---

### 2.12 Models/BCH/BCH.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models`
**Class:** `BCH` (public)
**Purpose:** BCH format loader (2171 lines). The standard 3DS binary container format for models, textures, animations, materials, skeletons. Used by Pokemon X/Y and OR/AS.

#### Private Header Structs

- `bchHeader` -- primary header (magic, version, section offsets/lengths, relocation table)
- `bchContentHeader` -- content table (pointers+entries for models, materials, shaders, textures, LUTs, lights, cameras, fogs, 7 animation types, scenes)
- `bchModelHeader` -- per-model header (world transform, materials, vertices, skeleton, node visibility, metadata)
- `bchObjectEntry` -- per-mesh entry (materialId, nodeId, VSH command buffer offsets, face header, bounding box)

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `load(string fileName)` | `OModelGroup` | Load BCH from file |
| `load(MemoryStream data)` | `OModelGroup` | Load BCH from stream |

#### Private Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `peek(BinaryReader)` | `uint` | Read uint without advancing |
| `scaleSkeleton(List<OBone>, int, int)` | `void` | Scale child bones |
| `transformSkeleton(List<OBone>, int, ref OMatrix)` | `void` | Build absolute transforms |
| `getVector(BinaryReader, attributeFormat)` | `OVector4` | Read vertex attribute |
| `readString(BinaryReader)` | `string` | Read offset-based string |
| `getAnimationKeyFrame(BinaryReader, OAnimationKeyFrameGroup)` | `void` | Parse animation key frame |
| `getAnimationKeyFrameBool(BinaryReader)` | `OAnimationKeyFrameGroup` | Parse boolean key frame |
| `getMetaData(BinaryReader)` | `List<OMetaData>` | Parse user metadata |

#### Key Implementation Details
- Performs relocation table patching (transforms relative offsets to absolute)
- Handles 4 BCH version ranges with different relocation flags
- Loads: textures, LUTs, lights, cameras, fogs, 7 animation types, scenes, models
- Model loading includes: materials (with PICA200 GPU commands), skeleton, vertex data
- Vertex reading uses PICA200 VSH attribute buffer format for position/normal/tangent/color/UV/bone

---

### 2.13 Models/GenericFormats/DAE.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.GenericFormats`
**Class:** `DAE` (public)
**Purpose:** Collada XML exporter. Converts OModelGroup to .dae XML. 1219 lines in CLI version (original: 1058 lines).

#### XML Serialization Inner Classes (30+)

- `COLLADA` (root), `daeAsset`, `daeImage`, `daeMaterial`, `daeEffect`, `daeProfile`
- `daeParam`, `daeParamSurfaceElement`, `daeParamSampler2DElement`
- `daeGeometry`, `daeMesh`, `daeSource`, `daeFloatArray`, `daeNameArray`
- `daeAccessor`, `daeInput`, `daeTriangles`, `daeVertices`
- `daeController`, `daeSkin`, `daeMatrix`, `daeNode`, `daeTransform`
- `daeVisualScene`, `daeInstanceController`, `daeBindMaterial`
- `daeAnimationSampler`, `daeChannel`, `daeAnimation` (CLI additions)

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `export(OModelGroup model, string fileName, int modelIndex, int skeletalAnimationIndex)` | `void` | Export to Collada DAE file |
| `exportAnimation(COLLADA dae, OModelGroup model, OModel mdl, int animIndex)` | `void` | Export skeletal animation channels (CLI addition) |
| `transformSkeleton(List<OBone>, int, ref OMatrix)` | `void` | Build absolute bone transforms |
| `writeSkeleton(List<OBone>, int, ref List<daeNode>)` | `void` | Write bone hierarchy nodes |

#### CLI Modifications vs Original
- Added `daeAnimation`, `daeAnimationSampler`, `daeChannel` classes for animation export
- Added `library_animations` list to COLLADA root
- Added `up_axis = "Y_UP"` to asset
- Flipped UV Y coordinates: `1.0f - vertex.texture0.y` (all 3 UV channels)
- Added `exportAnimation()` method with per-bone per-axis channel export
- Changed file output to use `using` statement with explicit encoding

---

### 2.14 Models/GenericFormats/OBJ.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.GenericFormats`
**Class:** `OBJ` (public)
**Purpose:** Wavefront OBJ exporter and importer.

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `export(OModelGroup model, string fileName, int modelIndex)` | `void` | Export model to .obj |
| `import(string fileName)` | `OModelGroup` | Import model from .obj |

#### CLI Modification vs Original
- UV Y flip: `1.0f - vertex.texture0.y` in export
- Class made `public` (was package-private)

---

### 2.15 Models/Mesh/MeshUtils.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models`
**Class:** `MeshUtils` (internal)
**Purpose:** Mesh optimization and utility functions.

#### Inner Class: `optimizedMesh`

| Field | Type |
|-------|------|
| `vertices` | `List<OVertex>` |
| `indices` | `List<uint>` |
| `hasNormal/hasTangent/hasColor/hasNode/hasWeight` | `bool` |
| `texUVCount` | `byte` |

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `calculateBounds(OModel model, OVertex vertex)` | `void` | Update model min/max vectors |
| `getColor(BinaryReader input)` | `Color` | Read RGBA color (byte) |
| `getColorFloat(BinaryReader input)` | `Color` | Read RGBA color (float) |
| `saturate(float value)` | `byte` | Clamp float to 0-255 |
| `optimizeMesh(OMesh mesh)` | `optimizedMesh` | Deduplicate vertices, build index buffer |
| `getOptimizedVertCount(List<OMesh> meshes)` | `uint` | Count optimized vertices |

---

### 2.16 Models/PICA200/PICACommand.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PICA200`
**Class:** `PICACommand` (internal)
**Purpose:** PICA200 GPU command register constants and types. 100+ register address constants.

#### Inner Enums

| Name | Values (count) |
|------|----------------|
| `vshAttribute` | 23 attributes (position, normal, tangent, color, texCoord0/1/2, boneIndex, boneWeight, etc.) |
| `attributeFormatType` | signedByte, unsignedByte, signedShort, single |
| `indexBufferFormat` | unsignedByte, unsignedShort |

#### Inner Structs

| Name | Fields |
|------|--------|
| `attributeFormat` | `type: attributeFormatType`, `attributeLength: uint` |
| `fragmentSamplerAbsolute` | `r, g, b, d0, d1, fresnel: bool` |
| `fragmentSamplerInput` | `r, g, b, d0, d1, fresnel: OFragmentSamplerInput` |
| `fragmentSamplerScale` | `r, g, b, d0, d1, fresnel: OFragmentSamplerScale` |

---

### 2.17 Models/PICA200/PICACommandReader.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PICA200`
**Class:** `PICACommandReader` (internal)
**Purpose:** Parses PICA200 GPU command buffers and exposes decoded state.

#### Constructor
- `PICACommandReader(Stream data, uint wordCount, bool ignoreAlign = false)`

#### Key Getter Methods (~30)

| Category | Methods |
|----------|---------|
| VSH Attributes | `getVSHAttributesBufferAddress(int)`, `getVSHAttributesBufferStride(int)`, `getVSHTotalAttributes(int)`, `getVSHAttributesBufferPermutation()`, `getVSHAttributesBufferFormat()`, `getVSHFloatUniformData(int)` |
| Index Buffer | `getIndexBufferAddress()`, `getIndexBufferFormat()`, `getIndexBufferTotalVertices()` |
| Texture | `getTexUnit0Address()`, `getTexUnit0Size()`, `getTexUnit0Format()` |
| TEV/Fragment | `getTevStage(byte)`, `getFragmentBufferColor()`, `getAlphaTest()`, `getBlendOperation()`, `getColorLogicOperation()` |
| Stencil/Depth | `getStencilTest()`, `getDepthTest()` |
| Cull | `getCullMode()` |
| LUT | `getFSHLookUpTable()` |
| Fragment Lighting | `getReflectanceSamplerAbsolute()`, `getReflectanceSamplerInput()`, `getReflectanceSamplerScale()` |

---

### 2.18 Models/PocketMonsters/GfModel.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `GfModel` (public)
**Purpose:** Sun/Moon Game Freak model format loader. Magic: `0x00010000` (container), `0x15122117` (single model). 552 lines in CLI (original: 522).

#### Static Field

| Name | Type | Purpose |
|------|------|---------|
| `DiagnosticLogging` | `bool` | When true, prints detailed parse diagnostics to stderr |

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `load(string fileName)` | `OModelGroup` | Load GfModel from file |
| `load(Stream data)` | `OModelGroup` | Load GfModel container (iterates sub-sections for model + textures) |
| `loadModel(Stream data, bool keepOpen = false)` | `OModel` | Load single model from stream |

#### Private Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `getVector(BinaryReader, attributeFormat)` | `OVector4` | Read vertex attribute vector |
| `getStrTable(BinaryReader input)` | `string[]` | Read string table |
| `getSubMeshInfo(BinaryReader input)` | `subMeshInfo` | Read sub-mesh descriptor |
| `addNode(List<int>, ushort[], int)` | `void` | Add bone node to vertex |

#### Private Struct: `subMeshInfo`

| Field | Type |
|-------|------|
| `cmdBuffers` | `uint[]` |
| `nodeLists` | `ushort[][]` |
| `vtxLengths` | `uint[]` |
| `idxLengths` | `uint[]` |
| `names` | `string[]` |
| `count` | `int` |

#### CLI Modifications vs Original
- Class visibility: `public` (was internal)
- Added `DiagnosticLogging` static field
- Added verbose `Console.Error.WriteLine` calls throughout `loadModel()`
- Modified header parsing to explicitly read `preHeaderMagic` and `preHeaderSectionsCount`

---

### 2.19 Models/PocketMonsters/GfMotion.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters` (was `Ohana3DS_Rebirth.Ohana.Animations` in original)
**Class:** `GfMotion` (public)
**Purpose:** Game Freak skeletal animation loader. Magic: `0x00060000`.

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `load(Stream data)` | `List<OSkeletalAnimation>` | Load animation list from stream |
| `loadAnim(BinaryReader input, int index = 0)` | `OSkeletalAnimation` | Load single animation |

#### Private Static Methods

| Signature | Returns |
|-----------|---------|
| `addFrame(OSkeletalAnimationBone bone, bool isRotation, int axis, float val, float frame, float lastVal)` | `void` |

---

### 2.20 Models/PocketMonsters/CM.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `CM` (internal)
**Purpose:** CM container loader. Reads PkmnContainer, passes first entry to GfModel.load. Animation loading commented out in CLI.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

---

### 2.21 Models/PocketMonsters/CP.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `CP` (internal)
**Purpose:** CP container loader. Reads PkmnContainer, passes second entry (index 1) to CM.load.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

---

### 2.22 Models/PocketMonsters/GR.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `GR` (internal)
**Purpose:** GR map model loader. Reads PkmnContainer, passes second entry (index 1) to BCH.load.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

---

### 2.23 Models/PocketMonsters/MM.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `MM` (internal)
**Purpose:** MM chibi model loader. Reads PkmnContainer, passes first entry (index 0) to BCH.load.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

---

### 2.24 Models/PocketMonsters/PC.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Models.PocketMonsters`
**Class:** `PC` (internal)
**Purpose:** PC container loader. Iterates all PkmnContainer entries through FileIO.load, merges model results.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

#### CLI Modification: Changed `FileIO.file` to `FileIO.LoadedFile`.

---

### 2.25 Textures/Codecs/TextureCodec.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `TextureCodec` (internal)
**Purpose:** Full texture decoder for all 14 PICA200 texture formats. 575 lines.

#### Static Methods

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `decode(byte[] data, int width, int height, OTextureFormat format)` | `Bitmap` | Decode texture buffer to Bitmap |
| `encode(Bitmap img, OTextureFormat format)` | `byte[]` | Encode Bitmap (only RGBA8 implemented) |

#### Supported Formats
`rgba8`, `rgb8`, `rgba5551`, `rgb565`, `rgba4`, `la8`, `hilo8`, `l8`, `a8`, `la4`, `l4`, `a4`, `etc1`, `etc1a4`

#### Private ETC1 Methods
- `etc1Decode(byte[] data, int width, int height, bool alpha)` -> `Bitmap`
- `etc1DecodeBlock(ulong data)` -> `int[4,4,3]`
- `etc1Pixel(int, int, int, int)` -> `int`
- `etc1Scramble(int, int)` -> `int`

#### Key Detail: Uses Morton/Z-order 8x8 pixel tiling for all non-ETC1 formats.

---

### 2.26 Textures/PocketMonsters/AD.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters`
**Class:** `AD` (internal)
**Purpose:** Loads AD map texture containers. Iterates PkmnContainer entries starting from index 1, passes to FileIO.load, merges model groups.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static OModelGroup` |

---

### 2.27 Textures/PocketMonsters/GfTexture.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters`
**Class:** `GfTexture` (internal)
**Purpose:** Game Freak texture loader. Magic: `0x15041213` + validates `"texture"` string at offset 8.

| Signature | Returns |
|-----------|---------|
| `load(Stream data, bool keepOpen = false)` | `static OTexture` |

#### GfTexture Format Codes -> PICA200 Mapping

| Code | Format |
|------|--------|
| 0x02 | rgb565 |
| 0x03 | rgb8 |
| 0x04 | rgba8 |
| 0x16 | rgba4 |
| 0x17 | rgba5551 |
| 0x23 | la8 |
| 0x24 | hilo8 |
| 0x25 | l8 |
| 0x26 | a8 |
| 0x27 | la4 |
| 0x28 | l4 |
| 0x29 | a4 |
| 0x2a | etc1 |
| 0x2b | etc1a4 |

---

### 2.28 Textures/PocketMonsters/PT.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters`
**Class:** `PT` (internal)
**Purpose:** PT monster texture container. Iterates PkmnContainer entries, passes to FileIO.load, collects textures from model groups.

| Signature | Returns |
|-----------|---------|
| `load(Stream data)` | `static List<OTexture>` |

---

### 2.29 Textures/TextureUtils.cs

**Namespace:** `Ohana3DS_Rebirth.Ohana`
**Class:** `TextureUtils` (internal)
**Purpose:** Bitmap <-> byte array conversion using System.Drawing GDI+.

| Signature | Returns | Purpose |
|-----------|---------|---------|
| `getBitmap(byte[] array, int width, int height)` | `static Bitmap` | RGBA8 buffer -> Bitmap |
| `getArray(Bitmap img)` | `static byte[]` | Bitmap -> RGBA8 buffer |

---

## 3. Files That Can Be Directly Copied from Ohana3DS-Rebirth

### 3.1 Byte-for-Byte Identical Files

These files in the CLI are exact copies of the original. They can be taken directly from
either source (identical content):

| CLI Path | Original Path | Lines |
|----------|---------------|-------|
| `Compressions/BLZ.cs` | `Compressions/BLZ.cs` | 108 |
| `Compressions/LZSS.cs` | `Compressions/LZSS.cs` | 58 |
| `Containers/GARC.cs` | `Containers/GARC.cs` | 97 |
| `Containers/OContainer.cs` | `Containers/OContainer.cs` | 27 |
| `Containers/PkmnContainer.cs` | `Containers/PkmnContainer.cs` | 61 |
| `Models/PocketMonsters/CP.cs` | `Models/PocketMonsters/CP.cs` | 24 |
| `Models/PocketMonsters/GR.cs` | `Models/PocketMonsters/GR.cs` | 24 |
| `Models/PocketMonsters/MM.cs` | `Models/PocketMonsters/MM.cs` | 24 |
| `Models/PICA200/PICACommand.cs` | `Models/PICA200/PICACommand.cs` | 226 |
| `Models/PICA200/PICACommandReader.cs` | `Models/PICA200/PICACommandReader.cs` | 665 |
| `Textures/PocketMonsters/GfTexture.cs` | `Textures/PocketMonsters/GfTexture.cs` | 66 |
| `Textures/TextureUtils.cs` | `TextureUtils.cs` | 39 |
| `Core/IOUtils.cs` | `IOUtils.cs` | 107 |
| `Core/PatriciaTree.cs` | `PatriciaTree.cs` | 79 |
| `Core/RenderBase.cs` | `RenderBase.cs` | 2296 |
| `Models/Mesh/MeshUtils.cs` | `Models/MeshUtils.cs` | 130 |
| `Models/BCH/BCH.cs` | `Models/BCH.cs` | 2171 |
| `Textures/Codecs/TextureCodec.cs` | `TextureCodec.cs` | 575 |

**Total: 18 files (6751 lines) can be directly copied.**

### 3.2 Files With Trivial Modifications

These files differ only in visibility changes (`class` -> `public class`) or `FileIO.file` -> `FileIO.LoadedFile` renames:

| File | Modification |
|------|-------------|
| `Compressions/LZSS_Ninty.cs` | `class` -> `public class` |
| `Models/PocketMonsters/PC.cs` | `FileIO.file` -> `FileIO.LoadedFile` |
| `Textures/PocketMonsters/AD.cs` | `FileIO.file` -> `FileIO.LoadedFile` |
| `Textures/PocketMonsters/PT.cs` | `FileIO.file` -> `FileIO.LoadedFile` |

### 3.3 Files With Substantive Modifications

| File | CLI Lines | Orig Lines | Changes |
|------|-----------|------------|---------|
| `Models/GenericFormats/DAE.cs` | 1219 | 1058 | +Animation export, UV Y-flip, up_axis, daeAnimation classes |
| `Models/GenericFormats/OBJ.cs` | 171 | 171 | UV Y-flip in export, `public` visibility |
| `Models/PocketMonsters/GfModel.cs` | 552 | 522 | DiagnosticLogging, verbose stderr output, public visibility |
| `Models/PocketMonsters/GfMotion.cs` | 175 | 176 | Moved namespace, public visibility |
| `Models/PocketMonsters/CM.cs` | 32 | 32 | Animation loading commented out |
| `Core/FileIO.cs` | 253 | 381 | Renamed struct, many formats commented out |

### 3.4 Original Files NOT Ported (Available if Needed)

| File | Purpose | Relevance |
|------|---------|-----------|
| `Animations/BS.cs` | BS animation format | Low -- not used by Sun/Moon |
| `AnimationUtils.cs` | Animation interpolation | Medium -- useful for animation playback |
| `Compressions/Yaz0.cs` | Yaz0 decompression | Low -- not used by Pokemon |
| `Containers/DARC.cs` | DARC container | Low |
| `Containers/DQVIIPack.cs` | DQ VII container | None |
| `Containers/FPT0.cs` | FPT0 container | None |
| `Containers/SARC.cs` | SARC container | Low |
| `Models/CGFX.cs` | CGFX format | Medium -- used by some 3DS games |
| `Models/GenericFormats/CMDL.cs` | CMDL exporter | Low |
| `Models/GenericFormats/SMD.cs` | SMD exporter | Low -- could add as export format |
| `Models/MBN.cs` | MBN format | Low |
| `Models/NLP.cs` | New Love Plus | None |
| `Models/NewLovePlus/*` | NLP sub-formats | None |
| `Models/PICA200/PICACommandWriter.cs` | Command writer | None for CLI |
| `Models/ZMDL.cs` | Zelda model | None |
| `RenderEngine.cs` | OpenGL renderer | None for CLI |
| `Textures/3DST.cs` | 3DST texture | Low |
| `Textures/BCLIM.cs` | BCLIM texture | Low |
| `Textures/DMP.cs` | DMP texture | Low |
| `Textures/ZTEX.cs` | Zelda texture | None |

---

## 4. Phased Rewrite Plan

### Phase 0: Project Scaffold (Day 1)

**Goal:** Clean .NET 8 solution with two projects, building and running.

1. Create solution `OhanaCli.sln`
2. Create `OhanaCli.Formats` class library (net8.0)
   - Add `System.Drawing.Common` package (for Bitmap compatibility, or replace with ImageSharp)
3. Create `OhanaCli.App` console app (net8.0)
   - Add `System.CommandLine` package
   - Add project reference to `OhanaCli.Formats`
4. Decide on Bitmap strategy:
   - **Option A:** Keep `System.Drawing.Common` (Windows-only, but minimal changes to copied code)
   - **Option B:** Replace all `Bitmap`/`Color` usage with `SixLabors.ImageSharp` (cross-platform, requires refactoring TextureCodec, TextureUtils, MeshUtils.getColor, BCH texture loading)
   - **Recommendation:** Option A for now, migrate to ImageSharp in a later phase
5. Create minimal `Program.cs` with a `--version` command to verify build

### Phase 1: Core Infrastructure (Days 2-3)

**Goal:** Copy all battle-tested parsers from original source. Verify they compile.

**Copy directly (18 files from Section 3.1):**
- `Core/`: RenderBase.cs, IOUtils.cs, PatriciaTree.cs
- `Compressions/`: BLZ.cs, LZSS.cs
- `Containers/`: GARC.cs, OContainer.cs, PkmnContainer.cs
- `Models/BCH/`: BCH.cs
- `Models/PICA200/`: PICACommand.cs, PICACommandReader.cs
- `Models/Mesh/`: MeshUtils.cs
- `Models/PocketMonsters/`: CP.cs, GR.cs, MM.cs
- `Textures/`: TextureCodec.cs, TextureUtils.cs
- `Textures/PocketMonsters/`: GfTexture.cs

**Copy with visibility fix (4 files from Section 3.2):**
- `Compressions/LZSS_Ninty.cs` -- make `public`
- `Models/PocketMonsters/PC.cs` -- rename `file` -> `LoadedFile`
- `Textures/PocketMonsters/AD.cs` -- rename `file` -> `LoadedFile`
- `Textures/PocketMonsters/PT.cs` -- rename `file` -> `LoadedFile`

**Copy with modifications (6 files from Section 3.3):**
- `Core/FileIO.cs` -- use CLI version (stripped-down, only Pokemon formats)
- `Models/PocketMonsters/GfModel.cs` -- use CLI version (has DiagnosticLogging)
- `Models/PocketMonsters/GfMotion.cs` -- use CLI version (namespace fix)
- `Models/PocketMonsters/CM.cs` -- use CLI version (animation loading commented out)
- `Models/GenericFormats/OBJ.cs` -- use CLI version (UV flip, public)
- `Models/GenericFormats/DAE.cs` -- use CLI version BUT with SID bug fix (see Phase 2)

**Verification:** Build the Formats project. All 28 .cs files should compile with zero errors.

### Phase 2: Fix the DAE SID Bug (Day 3)

**Goal:** Fix the critical DAE export bug that causes all materials to reference the same texture.

**The bug:** In `DAE.cs`, every material effect creates params with identical SIDs:
```
surface.sid = "img_surface"     // same for ALL materials
sampler.sid = "img_sampler"     // same for ALL materials
```

When an importer (Blender, Unity, Godot) processes the DAE:
- It sees `<newparam sid="img_surface">` in every effect
- It sees `<texture texture="img_sampler">` in every technique
- Since SIDs are effect-scoped in Collada, this SHOULD work
- But many importers resolve SIDs globally, causing last-texture-wins

**The fix:** Make SIDs unique per material:
- `surface.sid = "img_surface_" + mat.name`
- `sampler.sid = "img_sampler_" + mat.name`
- Update `sampler.sampler2D.source` to reference the new surface SID
- Update `technique.phong.diffuse.texture.texture` to reference the new sampler SID

**This is a 4-line change** with massive impact on output quality.

### Phase 3: CLI Commands (Days 4-5)

**Goal:** Implement the CLI entry point with all commands.

#### Command: `info <file>`
- Load file via FileIO
- Print: format type, model count, mesh count, texture count, bone count, animation count
- For GARC: print entry count, estimated content types

#### Command: `convert <file> -o <dir> -f <format>`
- Supported formats: `dae` (default), `obj`
- Load file via FileIO
- If model: export to specified format + extract textures as PNG
- If GARC: iterate entries, group model+textures, export each group

#### Command: `batch <inputDir> -o <dir> -f <format>`
- Iterate all files in inputDir
- Call convert for each

#### Command: `convert-all <garcDir> -o <dir> -f <format>`
- The mass-extraction command
- Iterate all .garc files in garcDir
- For each GARC, use streaming single-pass grouping:
  - Track current model group + trailing texture groups
  - When a new model is encountered, flush previous group
  - At end of GARC, flush final group
- Derive folder names from model/texture names
- Print summary statistics (converted, textures, errors)

#### Command: `diagnose <file> --start <n> --end <n>`
- Hex dump specific GARC entries with header analysis
- Print magic bytes, section headers, format detection results

### Phase 4: Testing and Validation (Day 6)

**Goal:** Verify the rewrite produces correct output for known-good inputs.

1. **Unit test: DAE SID uniqueness**
   - Export a model with 3+ materials
   - Parse output XML
   - Verify all surface/sampler SIDs are unique across effects

2. **Integration test: Known Pokemon models**
   - Export Pikachu (known-good GARC) -> verify mesh count, texture count, file size
   - Export a multi-material model -> verify all textures bind correctly in Blender

3. **Regression test: 79 missing Pokemon**
   - Run GfModel.load on Pokemon IDs 650-700, 775-800
   - Document which ones produce 0 meshes (expected: all of them)
   - These use a different GfModel version -- fix is out of scope for rewrite

4. **Batch test: Full GARC extraction**
   - Run `convert-all` on the ROM dump
   - Compare statistics against known results: ~3570 models, ~32280 textures
   - Document any new errors vs old error count (was 11641)

---

## 5. Known Bugs in Current Implementation

### 5.1 Critical Bugs

#### Bug: DAE Material SID Collision (AFFECTS ALL MULTI-MATERIAL EXPORTS)
- **Location:** `DAE.cs` lines 622/630 (original), lines 671/679 (CLI)
- **Description:** All materials share `surface.sid = "img_surface"` and `sampler.sid = "img_sampler"`. In multi-material models, importers may bind the wrong texture to materials.
- **Impact:** Every Pokemon model with 2+ materials exports with incorrect texture assignments in many 3D tools.
- **Origin:** Bug exists in original Ohana3DS-Rebirth, was carried over unchanged.
- **Fix:** Make SIDs unique per material (see Phase 2).

#### Bug: 79 Missing Pokemon Models (IDs 650-700, 775-800)
- **Location:** `GfModel.cs` -- `loadModel()`
- **Description:** These Pokemon use a different GfModel sub-version that produces 0 meshes when parsed. The model loads without error but `mesh.Count == 0`.
- **Impact:** 79 out of 957 Pokemon have no model output.
- **Origin:** GfModel format has version differences across game updates. The parser handles the common version but not these variants.
- **Fix:** Requires reverse engineering the variant format. Out of scope for initial rewrite.

### 5.2 Non-Critical Bugs

#### Bug: CM Animation Loading Disabled
- **Location:** `CM.cs` -- animation loading is commented out
- **Description:** The original loads animations from CM container entry index 1 via `GfMotion.load()`. The CLI version comments this out with `// TODO: animation support`.
- **Impact:** CM-sourced models have no animations attached.
- **Fix:** Uncomment the animation loading code in Phase 1.

#### Bug: Program.cs Monolithic Design
- **Location:** `Program.cs` -- 996 lines
- **Description:** All CLI logic, export logic, GARC iteration, grouping heuristics, and debug dump functions are in a single file.
- **Impact:** Unmaintainable, hard to test, hard to extend.
- **Fix:** Split into: `Commands/InfoCommand.cs`, `Commands/ConvertCommand.cs`, etc.

### 5.3 Design Issues

#### Issue: `System.Drawing.Bitmap` Windows Dependency
- **Location:** `TextureCodec.cs`, `TextureUtils.cs`, `BCH.cs` (texture loading), `GfTexture.cs`
- **Description:** All texture handling uses `System.Drawing.Bitmap` which requires GDI+ (Windows-only on .NET 8 without the compatibility pack).
- **Impact:** CLI cannot run on Linux/macOS without `System.Drawing.Common` package.
- **Future fix:** Replace with `SixLabors.ImageSharp` for cross-platform support.

#### Issue: Untyped Data Container (`object data`)
- **Location:** `FileIO.LoadedFile.data` is `object`
- **Description:** The `LoadedFile` struct stores parsed data as `object`, requiring runtime casts everywhere.
- **Impact:** No compile-time type safety. Easy to cast to wrong type.
- **Future fix:** Use generic result type or discriminated union pattern.

#### Issue: Stream Lifecycle Management
- **Location:** Multiple loaders close streams they didn't open
- **Description:** `FileIO.load(Stream)`, `BCH.load(MemoryStream)`, and others call `data.Close()` on streams passed to them. This violates the "creator closes" principle.
- **Impact:** Risk of double-close, use-after-close, and makes streaming difficult.
- **Future fix:** Use `using` statements at call sites, remove `Close()` from loaders.

---

## 6. Optimizations and Quick Wins

### 6.1 Quick Wins

1. **Fix DAE SIDs (4 lines)** -- Biggest bang for buck. Fixes all multi-material exports.

2. **Re-enable CM animation loading (uncomment 5 lines)** -- Restores animations for CM-sourced models.

3. **Add MTL file output to OBJ exporter** -- Currently exports .obj without .mtl material library. Adding a companion .mtl write is ~30 lines and makes OBJ exports useful in 3D tools.

4. **Texture PNG naming** -- Use the texture's `.name` property instead of index-based naming for output PNG files. Makes output self-documenting.

5. **Error messages** -- Replace bare `Console.Error.WriteLine` with structured logging. Makes batch diagnostics parseable.

### 6.2 Architecture Optimizations

1. **Separate export logic from CLI commands** -- Create an `ExportService` class that handles model -> file export. Makes it testable independently of CLI argument parsing.

2. **Replace `object data` with typed results** -- Use pattern:
   ```
   interface ILoadResult { formatType Type { get; } }
   class ModelResult : ILoadResult { OModelGroup Model; }
   class TextureResult : ILoadResult { OTexture Texture; }
   ```

3. **Lazy GARC decompression** -- Current `ReadEntryData()` reads the entire entry into memory. For large GARCs, this is wasteful. Consider streaming decompression.

4. **Parallel GARC processing** -- In `convert-all`, each GARC is independent. Process GARCs in parallel with `Parallel.ForEach` for significant speedup on multi-core systems.

5. **Memory pooling for texture decode** -- `TextureCodec.decode()` allocates a new `byte[width * height * 4]` for every texture. Use `ArrayPool<byte>` for large textures.

### 6.3 Future Enhancements

1. **ImageSharp migration** -- Replace `System.Drawing.Bitmap` with `SixLabors.ImageSharp.Image<Rgba32>` for cross-platform support and better PNG encoding options.

2. **glTF export** -- Add glTF 2.0 (.gltf/.glb) as an export format. More widely supported than DAE in modern tools (Unity, Godot, Blender).

3. **SMD export** -- Port `SMD.cs` from original for Source Engine compatibility.

4. **CGFX support** -- Port `CGFX.cs` from original for X/Y format support.

5. **Progress reporting** -- Add `IProgress<T>` callbacks to long operations for real-time progress bars in the CLI.

---

## 7. Build and Test Commands

### 7.1 Build

```bash
# Build the entire solution
dotnet build src/PokemonGreen.OhanaCli/OhanaCli.sln

# Build individual projects
dotnet build src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/OhanaCli.Formats.csproj
dotnet build src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj

# Build in Release mode
dotnet build src/PokemonGreen.OhanaCli/OhanaCli.sln -c Release

# Publish self-contained (Windows x64)
dotnet publish src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj \
  -c Release -r win-x64 --self-contained -o dist/
```

### 7.2 Run CLI

```bash
# Run with dotnet
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- <command> <args>

# Examples
dotnet run --project ... -- info path/to/file.garc
dotnet run --project ... -- convert path/to/file.garc -o output/ -f dae
dotnet run --project ... -- batch path/to/garcdir/ -o output/ -f dae
dotnet run --project ... -- convert-all path/to/garcdir/ -o output/ -f dae
dotnet run --project ... -- diagnose path/to/file.garc --start 0 --end 5
```

### 7.3 Test Commands (Post-Rewrite Validation)

```bash
# Single model conversion test
dotnet run --project ... -- convert "path/to/pikachu.garc" -o test-output/ -f dae
# Verify: test-output/ should contain .dae + .png files
# Verify: open .dae in Blender, confirm textures bind correctly

# Batch test on known-good directory
dotnet run --project ... -- batch "path/to/pokemon-garcs/" -o batch-output/ -f dae
# Verify: count output folders, compare against expected model count

# Full extraction test
dotnet run --project ... -- convert-all "path/to/all-garcs/" -o full-output/ -f dae
# Expected: ~3570 models, ~32280 textures
# Check: error count should be <= 11641 (parity with old CLI)

# DAE SID validation (manual)
# Open any multi-material .dae in a text editor
# Search for sid="img_surface" -- should NOT appear
# Each effect should have unique surface/sampler SIDs

# Diagnose a problematic GARC
dotnet run --project ... -- diagnose "path/to/problem.garc" --start 0 --end 10
```

### 7.4 NuGet Dependencies

| Package | Version | Project | Purpose |
|---------|---------|---------|---------|
| `System.CommandLine` | 2.0.0-beta4.22272.1 | OhanaCli.App | CLI argument parsing |
| `System.Drawing.Common` | 8.0.12 | OhanaCli.Formats | Bitmap/Color types |
| `SixLabors.ImageSharp` | 3.1.6 | OhanaCli.Formats | Currently unused (future migration target) |

### 7.5 .NET SDK Requirement

- .NET 8.0 SDK or later
- Target framework: `net8.0`
- `ImplicitUsings` enabled
- `Nullable` enabled

---

## Appendix A: Format Magic Bytes Quick Reference

| Magic (hex) | Magic (ASCII) | Format | Loader |
|-------------|---------------|--------|--------|
| `0x00010000` | -- | GfModel container | `GfModel.load` |
| `0x00060000` | -- | GfMotion animation | `GfMotion.loadAnim` |
| `0x15041213` | -- | GfTexture | `GfTexture.load` |
| `0x15122117` | -- | GfModel single | `GfModel.loadModel` |
| `43 52 41 47` | `CRAG` | GARC | `GARC.load` |
| `49 45 43 50` | `IECP` | LZSS compressed | `LZSS.decompress` -> recurse |
| `42 43 48 00` | `BCH\0` | BCH | `BCH.load` |
| `41 44` | `AD` | AD texture container | `AD.load` |
| `42 4D` | `BM` | BM model (alias MM) | `MM.load` |
| `43 4D` | `CM` | CM model container | `CM.load` |
| `43 50` | `CP` | CP container | `CP.load` |
| `47 52` | `GR` | GR map model | `GR.load` |
| `4D 4D` | `MM` | MM chibi model | `MM.load` |
| `50 43` | `PC` | PC container | `PC.load` |
| `50 54` | `PT` | PT texture container | `PT.load` |
| `[A-Z][A-Z]` | (any 2 uppercase) | Generic Pokemon container | `PkmnContainer.load` |
| `xx xx xx 11` | -- | LZ11 compressed | `LZSS_Ninty.decompress` |
| `xx xx xx 90` | -- | BLZ compressed | `BLZ.decompress` |

## Appendix B: Material-to-Texture Binding Chain

```
OMesh
  .materialId (ushort index)
    -> OModel.material[materialId]
      -> OMaterial.name0  (primary texture name)
      -> OMaterial.name1  (secondary texture name)
      -> OMaterial.name2  (tertiary texture name)
        -> match against OModelGroup.texture[].name
          -> OTexture.texture (Bitmap)
```

## Appendix C: GARC Entry Grouping Heuristic

The `convert-all` command uses a streaming single-pass heuristic to group
GARC entries into (model + trailing textures):

1. Iterate entries 0..N in order
2. Load each entry via `FileIO.load()`
3. If result is `formatType.model`:
   - Flush any pending (model + textures) group
   - Start new group with this model
4. If result is `formatType.image` or `formatType.texture`:
   - Add to current group's texture list
5. If result is `formatType.container`:
   - Load container, extract model if present
6. At end of GARC, flush final group

This heuristic works because Sun/Moon GARCs consistently store models
before their associated textures.
