# SPICA CLI Rewrite Guide

> This document is the sole reference for turning SPICA into a headless CLI tool.
> SPICA is the modernized successor to Ohana3DS-Rebirth. It has a cleaner architecture,
> uses System.Numerics instead of custom math types, supports more formats (CGFX, GFL/GFL2,
> GFLX, MT Framework), and most critically — **its DAE exporter produces correct skeletal
> animations** using per-component Euler channels with frame baking.
>
> The SPICA core library (`SPICA.dll`) is already GUI-free. The CLI rewrite is primarily
> about replacing `SPICA.WinForms` with a console app and adding GARC iteration logic.

---

## Table of Contents

1. [Project Structure Tree](#1-project-structure-tree)
2. [Architecture Overview — How SPICA Differs from Ohana3DS](#2-architecture-overview)
3. [API Reference for Key Classes](#3-api-reference-for-key-classes)
4. [The DAE Export Pipeline (Critical Path)](#4-the-dae-export-pipeline)
5. [Files That Can Be Directly Used](#5-files-that-can-be-directly-used)
6. [Phased Rewrite Plan](#6-phased-rewrite-plan)
7. [Known Bugs and Limitations](#7-known-bugs-and-limitations)
8. [Optimizations and Quick Wins](#8-optimizations-and-quick-wins)
9. [Build and Test Commands](#9-build-and-test-commands)

---

## 1. Project Structure Tree

### 1.1 Solution Layout

```
D:\Projects\SPICA\
  SPICA.sln                                    # .NET Framework 4.7.1 solution
  Libraries/
    OpenTK.dll                                 # OpenGL bindings (rendering only)
    OpenTK.GLControl.dll                       # WinForms GL widget (rendering only)

  SPICA/                                       # Core library (GUI-FREE) — 441 .cs files, 19,431 lines
    SPICA.csproj                               # Class library, net471, refs: System.Numerics, System.Drawing, System.Xml

    Compression/
      LZ4.cs                                   # LZ4 decompression

    Formats/
      Common/                                  # Shared infrastructure
        BitUtils.cs                            # Bit manipulation helpers
        CRC32Hash.cs                           # CRC32 hashing
        Exceptions.cs                          # Common exceptions
        FNV1a.cs                               # FNV-1a hash
        INamed.cs                              # Interface: string Name { get; set; }
        INameIndexed.cs                        # Interface: named + indexed
        InvalidMagicException.cs               # Format validation exception
        IOUtils.cs                             # Binary read/write extensions
        IPatriciaDict.cs                       # Patricia tree dictionary interface
        IPatriciaTreeNode.cs                   # Patricia tree node interface
        KeyFrame.cs                            # KeyFrame struct (Frame, Value, InSlope, OutSlope)
        KeyFrameQuantization.cs                # Quantized keyframe decoding
        KeyFrameQuantizationHelper.cs          # Quantization helper methods
        MeshTransform.cs                       # Transforms mesh vertices to world space
        PatriciaTree.cs                        # Patricia trie implementation
        StringUtils.cs                         # String read/write extensions
        TextureTransform.cs                    # Texture coordinate transform
        TextureTransformType.cs                # Transform type enum

      CtrGfx/                                  # CGFX format (X/Y, OR/AS) — 108 files
        Gfx.cs                                 # CGFX loader/saver (666 lines, largest file)
        GfxDict.cs                             # Generic dictionary
        GfxDictionary.cs                       # Patricia dictionary
        GfxHeader.cs                           # CGFX header
        GfxNode.cs                             # Scene graph node
        GfxObject.cs                           # Base object
        Animation/                             # 16 animation classes
        AnimGroup/                             # 13 animation group classes
        Camera/                                # 12 camera classes
        Emitter/                               # Particle emitter
        Fog/                                   # Fog effect
        Light/                                 # 9 light classes
        LUT/                                   # Lookup table
        Model/                                 # 9 model classes + Material/ (30) + Mesh/ (14)
        Scene/                                 # Scene container
        Shader/                                # Shader reference
        Texture/                               # 4 texture classes

      CtrH3D/                                  # H3D format — THE CENTRAL DATA MODEL
        H3D.cs                                 # Root container (256 lines) — all models, textures, anims
        H3DDict.cs                             # Typed dictionary with PatriciaTree
        H3DHeader.cs                           # Binary header
        H3DRelocator.cs                        # Pointer relocation (absolute ↔ relative)
        H3DPatriciaTree.cs                     # Name tree for fast lookup
        H3DVertexData.cs                       # Vertex buffer descriptor
        Animation/                             # 22 animation classes
          H3DAnimation.cs                      # Base animation (elements, frame count, type, flags)
          H3DAnimationElement.cs               # Element (name, content, target, primitive type)
          H3DAnimTransform.cs                  # Euler transform (9 float keyframe groups: SRT × XYZ)
          H3DAnimQuatTransform.cs              # Quaternion transform (scale[], rotation[], translation[])
          H3DAnimMtxTransform.cs               # Baked matrix transform
          H3DFloatKeyFrameGroup.cs             # Keyframe group with interpolation (243 lines)
          H3DPrimitiveType.cs                  # Float, Vector2D/3D, Transform, QuatTransform, MtxTransform, RGBA, Boolean
          H3DTargetType.cs                     # Bone, Material, MeshNodeVisibility, Camera, Light, Fog
          H3DMaterialAnim.cs                   # Material animation
        Camera/                                # 8 camera types
        Fog/                                   # Fog effect
        Light/                                 # 7 light types
        LUT/                                   # 3 LUT types
        Model/
          H3DBone.cs                           # Bone (Scale, Rotation, Translation, InverseTransform, Transform property)
          H3DBoneFlags.cs                      # IsSegmentScaleCompensate, IsScaleUniform, etc.
          H3DModel.cs                          # Model (skeleton, materials, meshes, mesh nodes tree)
          Material/                            # 17 material classes
            H3DMaterialParams.cs               # Full material params (532 lines)
            H3DMaterial.cs                     # Material (params + texture names + mappers)
          Mesh/                                # 6 mesh classes
            H3DMesh.cs                         # Mesh (424 lines, vertex data, attributes, submeshes)
            H3DSubMesh.cs                      # SubMesh (bone indices, indices, skinning)
        Scene/                                 # Scene container
        Shader/                                # Shader reference
        Texture/
          H3DTexture.cs                        # Texture (raw buffer, format, width/height, ToBitmap)

      Generic/                                 # Export formats
        COLLADA/                               # DAE exporter (21 files)
          DAE.cs                               # Main exporter (639 lines) — model + animation
          DAENode.cs                           # Bone node with SetBoneEuler()
          DAEUtils.cs                          # RadToDeg, VectorStr, MatrixStr helpers
          DAEAnimation.cs                      # Animation XML structure
          DAEController.cs                     # Skin controller
          DAEGeometry.cs                       # Geometry source
          DAEEffect.cs                         # Material effect
          DAEImage.cs                          # Image reference
          DAEMaterial.cs                       # Material
          DAEMesh.cs                           # Mesh triangles
          DAESkin.cs                           # Skin weights/joints
          DAESource.cs                         # Data source (float/name arrays)
          DAEAccessor.cs                       # Array accessor
          DAEInput.cs                          # Input semantic
          DAEInputOffset.cs                    # Input with offset
          DAEMatrix.cs                         # 4x4 matrix
          DAEVector3.cs                        # 3D vector with SID
          DAEVector4.cs                        # 4D vector with SID (for rotations)
          DAEVisualScene.cs                    # Visual scene + scene reference
          DAEArray.cs                          # Float/name array
          DAEAsset.cs                          # Asset metadata (up_axis, contributor)
        StudioMdl/                             # SMD exporter (4 files, 472 lines)
          SMD.cs                               # SMD import/export
        WavefrontOBJ/                          # OBJ exporter (2 files, 393 lines)
          OBJ.cs                               # OBJ import/export

      GFL/                                     # Game Freak Library v1 (X/Y)
        GF1MotionPack.cs                       # Motion pack container
        Motion/
          GF1Motion.cs                         # GFL1 animation loader (392 lines)
          GF1MotBone.cs                        # Bone animation
          GF1MotBoneTransform.cs               # Bone transform keyframes
          GF1MotKeyFrame.cs                    # Keyframe

      GFL2/                                    # Game Freak Library v2 (Sun/Moon) — KEY FORMAT
        GFModelPack.cs                         # Model pack → H3D converter
        GFMotionPack.cs                        # Motion pack container
        GFNV1.cs                               # FNV-1 hash (name lookup)
        GFSection.cs                           # Section header reader
        Model/
          GFModel.cs                           # Sun/Moon model loader (549 lines) → ToH3DModel()
          GFBone.cs                            # Bone (Name, Parent, Translation, Rotation, Scale, Flags)
          GFHashName.cs                        # Hash-to-name mapping
          GFLUT.cs                             # Lookup table
          Material/                            # 6 material classes
            GFMaterial.cs                      # Full material (487 lines)
          Mesh/
            GFMesh.cs                          # Mesh (495 lines)
            GFSubMesh.cs                       # SubMesh
        Motion/
          GFMotion.cs                          # Animation container (skeletal + material + visibility)
          GFSkeletonMot.cs                     # Skeletal animation → H3DAnimQuatTransform
          GFMotBoneTransform.cs                # Per-bone keyframes (9 channels + isAxisAngle)
          GFMotKeyFrame.cs                     # Keyframe
          GFMaterialMot.cs                     # Material animation
          GFMotUVTransform.cs                  # UV animation
          GFMotBoolean.cs                      # Boolean animation
          GFVisibilityMot.cs                   # Visibility animation
        Shader/
          GFShader.cs                          # Shader (339 lines)
        Texture/
          GFTexture.cs                         # GF texture → H3DTexture
          GFTextureFormat.cs                   # Format enum

      GFLX/                                    # Game Freak Library X (Switch, Sword/Shield)
        GFbmdl.cs                              # Switch model format
        GFLXPack.cs                            # GFLX package

      ModelBinary/                             # MBN format (5 files)
        MBn.cs                                 # Model binary loader

      MTFramework/                             # MT Framework (Monster Hunter, etc.)
        Model/                                 # 5 model classes
          MTModel.cs                           # MT model loader (383 lines)
        Shader/                                # 8 shader classes
        Texture/                               # 2 texture classes

      Packages/
        GFL/
          GFPackage.cs                         # GF package reader (different from WinForms version)

      SceneState.cs                            # Current model/animation/texture indices

    Math3D/                                    # Math types
      Matrix.cs                                # Matrix4x4 extensions
      Matrix3x3.cs                             # 3x3 matrix
      Matrix3x4.cs                             # 3x4 matrix (for bone inverse transforms)
      RGBA.cs                                  # Color type
      Vector.cs                                # Vector extensions + ToEuler() quaternion conversion
      Interpolation.cs                         # Lerp, Hermite interpolation

    PICA/                                      # PICA200 GPU (3DS graphics hardware)
      PICACommand.cs                           # Command structure
      PICACommandReader.cs                     # Command buffer parser
      PICACommandWriter.cs                     # Command buffer writer
      PICARegister.cs                          # Register constants (402 lines)
      Commands/                                # 33 command type enums/structs
        PICATextureFormat.cs                   # RGBA8, RGB8, ETC1, etc.
        PICAAttributeName.cs                   # Position, Normal, TexCoord, BoneIndex, BoneWeight
      Converters/
        PICAVertex.cs                          # Vertex struct (Position, Normal, Color, TexCoord, Indices, Weights)
        VerticesConverter.cs                   # Raw buffer → PICAVertex[]
        TextureConverter.cs                    # Raw buffer → Bitmap (328 lines)
        TextureCompression.cs                  # ETC1 decompression
        BoneIndices.cs                         # Bone index type
        BoneWeights.cs                         # Bone weight type
      Shader/                                  # 13 shader binary classes
        ShaderBinary.cs                        # Shader program parser (315 lines)

    Serialization/                             # Custom binary serializer (attribute-driven)
      BinaryDeserializer.cs                    # Attribute-driven deserializer (378 lines)
      BinarySerializer.cs                      # Attribute-driven serializer (484 lines)
      BinarySerialization.cs                   # Shared logic
      BitReader.cs / BitWriter.cs              # Bit-level I/O
      ICustomSerialization.cs                  # Custom serialize hook
      Attributes/                              # 12 serialization attribute classes
        InlineAttribute.cs                     # Inline struct (no pointer)
        IgnoreAttribute.cs                     # Skip field
        FixedLengthAttribute.cs                # Fixed-size array
        SectionAttribute.cs                    # Section routing
        VersionAttribute.cs                    # Version-conditional

  SPICA.WinForms/                              # GUI app — 71 files, 5,353 lines
    SPICA.WinForms.csproj                      # WinExe, net471, refs: OpenTK, System.Windows.Forms
    Program.cs                                 # Entry point
    FrmMain.cs                                 # Main window (model viewer, animation player)
    FrmExport.cs                               # Export dialog
    FrmLoading.cs                              # Loading progress
    Formats/
      FileIO.cs                                # File open/save orchestrator (uses WinForms dialogs)
      FormatIdentifier.cs                      # Magic-byte format detection → H3D
      GFPackage.cs                             # GF package header reader (2-byte magic, offsets)
      GFPkmnModel.cs                           # Pokemon model loader (PC package → GFModelPack → H3D)
      GFPkmnSklAnim.cs                         # Pokemon skeletal animation loader (PK/PB packages)
      GFBtlSklAnim.cs                          # Battle skeletal animation (BS package)
      GFCharaModel.cs                          # Character model (CM package)
      GFOWCharaModel.cs                        # Overworld character (MM package)
      GFOWMapModel.cs                          # Overworld map (GR package)
      GFL2OverWorld.cs                         # Overworld loader (BG package)
      GFPackedTexture.cs                       # Packed texture (AD/PT packages)
    GUI/                                       # Custom WinForms controls (NOT needed for CLI)
    TextureManager.cs                          # Texture cache for GUI

  SPICA.Rendering/                             # OpenGL renderer — 37 files, 5,198 lines (NOT needed for CLI)
    Renderer.cs                                # OpenGL scene renderer
    Model.cs / Mesh.cs / Texture.cs            # GPU resources
    Animation/                                 # Realtime animation playback
      SkeletalAnimation.cs                     # Bone transform evaluation
    Shaders/                                   # GLSL shader generation
```

### 1.2 File Count Summary

| Project | .cs Files | Lines | GUI-Free? |
|---------|-----------|-------|-----------|
| SPICA (core) | 441 | 19,431 | **Yes** |
| SPICA.WinForms | 71 | 5,353 | No (WinForms) |
| SPICA.Rendering | 37 | 5,198 | No (OpenTK/OpenGL) |
| **Total** | **508** | **29,982** | |

---

## 2. Architecture Overview

### 2.1 Key Differences from Ohana3DS

| Feature | Ohana3DS | SPICA |
|---------|----------|-------|
| Math types | Custom `OVector3`, `OMatrix` (col-major, `[col,row]`) | `System.Numerics` (Vector3, Matrix4x4, Quaternion) |
| Data model | `RenderBase` megaclass (2296 lines, all types in one file) | `CtrH3D` namespace (100+ small typed files) |
| Binary I/O | Manual `BinaryReader` everywhere | Attribute-driven serialization framework |
| BCH loading | Single monolithic `BCH.cs` (2171 lines) | Split across `H3D.Open()` + `BinaryDeserializer` + per-type `ICustomSerialization` |
| Animation types | 3 types in `OSkeletalAnimationBone` | `H3DAnimTransform` (Euler), `H3DAnimQuatTransform` (Quat), `H3DAnimMtxTransform` (Matrix) |
| GF model format | `GfModel.cs` + `GfMotion.cs` (manual parsing) | `GFModel.cs` + `GFMotion.cs` + `GFSkeletonMot.cs` (structured, converts to H3D) |
| DAE animation | Matrix animation (broken) → per-component (still broken) | Per-component Euler with frame baking at 30fps (**working**) |
| Quaternion handling | Not implemented | `ToEuler()` conversion, `isAxisAngle` → Quaternion at import |
| Export formats | DAE, OBJ | DAE, SMD, OBJ |
| Additional formats | — | CGFX, MBN, MT Framework, GFL1, GFLX |
| Segment scale compensate | Not handled in export | Handled: `InvScale /= parentScale` per frame |
| Framework | .NET 8 (our CLI) / .NET Framework (original) | .NET Framework 4.7.1 |

### 2.2 Data Flow

```
Input File
    ↓
FormatIdentifier.IdentifyAndOpen()          # Magic-byte detection
    ↓
Format-specific loader                       # BCH → H3D.Open(), GFModel → .ToH3DModel(), etc.
    ↓
H3D (central data model)                    # Models, Textures, SkeletalAnimations, Materials, ...
    ↓
Export format                                # DAE, SMD, OBJ
    ↓
Output files (.dae + .png)
```

All format loaders convert to `H3D`. All exporters consume `H3D`. This is SPICA's key design advantage — a single intermediate representation.

### 2.3 The H3D Central Data Model

```csharp
public class H3D {
    H3DDict<H3DModel>          Models;
    H3DDict<H3DMaterialParams> Materials;
    H3DDict<H3DShader>         Shaders;
    H3DDict<H3DTexture>        Textures;
    H3DDict<H3DLUT>            LUTs;
    H3DDict<H3DLight>          Lights;
    H3DDict<H3DCamera>         Cameras;
    H3DDict<H3DFog>            Fogs;
    H3DDict<H3DAnimation>      SkeletalAnimations;
    H3DDict<H3DMaterialAnim>   MaterialAnimations;
    H3DDict<H3DAnimation>      VisibilityAnimations;
    H3DDict<H3DAnimation>      LightAnimations;
    H3DDict<H3DAnimation>      CameraAnimations;
    H3DDict<H3DAnimation>      FogAnimations;
    H3DDict<H3DScene>          Scenes;

    static H3D Open(byte[] Data);              // Deserialize BCH from buffer
    static void Save(string FileName, H3D);    // Serialize to BCH
    void Merge(H3D other);                     // Merge another H3D into this
}
```

---

## 3. API Reference for Key Classes

### 3.1 H3D (Central Container)

**Namespace:** `SPICA.Formats.CtrH3D`
**Purpose:** Root container for all 3D scene data. Serializable to/from BCH binary format.

| Method | Returns | Purpose |
|--------|---------|---------|
| `H3D.Open(byte[] Data)` | `H3D` | Deserialize BCH from memory buffer |
| `H3D.Open(MemoryStream MS)` | `H3D` | Deserialize BCH from stream |
| `H3D.Save(string FileName, H3D Scene)` | `void` | Serialize H3D to BCH file |
| `Merge(H3D SceneData)` | `void` | Merge models/textures/animations from another H3D |
| `CopyMaterials()` | `void` | Copy materials from models to top-level Materials dict |

### 3.2 H3DBone

**Namespace:** `SPICA.Formats.CtrH3D.Model`
**Purpose:** Skeleton bone with local SRT and cached inverse transform.

| Field | Type | Purpose |
|-------|------|---------|
| `Flags` | `H3DBoneFlags` | IsSegmentScaleCompensate, IsScaleUniform, etc. |
| `BillboardMode` | `H3DBillboardMode` | Billboard rendering mode |
| `ParentIndex` | `short` | -1 for root |
| `Scale` | `Vector3` | Local scale |
| `Rotation` | `Vector3` | Local rotation (Euler radians) |
| `Translation` | `Vector3` | Local translation |
| `InverseTransform` | `Matrix3x4` | Cached inverse world transform |
| `Name` | `string` | Bone name |
| `MetaData` | `H3DMetaData` | User metadata |

| Property/Method | Returns | Purpose |
|-----------------|---------|---------|
| `Transform` (get) | `Matrix4x4` | `S * Rx * Ry * Rz * T` (local transform) |
| `GetWorldTransform(H3DDict<H3DBone>)` | `Matrix4x4` | Walk parent chain, accumulate `bone.Transform * parent` |
| `CalculateTransform(H3DDict<H3DBone>)` | `void` | Compute + cache `InverseTransform`, set flag bits |

**Transform property order:**
```csharp
Transform  = CreateScale(Scale);
Transform *= CreateRotationX(Rotation.X);
Transform *= CreateRotationY(Rotation.Y);
Transform *= CreateRotationZ(Rotation.Z);
Transform *= CreateTranslation(Translation);
```
System.Numerics uses row-major multiplication, so this is `S * Rx * Ry * Rz * T` in row-vector convention = `T * Rz * Ry * Rx * S` in column-vector convention.

### 3.3 H3DAnimation & Elements

**Namespace:** `SPICA.Formats.CtrH3D.Animation`

**H3DAnimation:**
| Field | Type |
|-------|------|
| `Name` | `string` |
| `FramesCount` | `float` |
| `AnimationType` | `H3DAnimationType` (Skeletal, Material, Visibility, Camera, Light, Fog) |
| `AnimationFlags` | `H3DAnimationFlags` (IsLooping) |
| `Elements` | `List<H3DAnimationElement>` |

**H3DAnimationElement:**
| Field | Type |
|-------|------|
| `Name` | `string` (bone name) |
| `Content` | `object` (H3DAnimTransform, H3DAnimQuatTransform, or H3DAnimMtxTransform) |
| `TargetType` | `H3DTargetType` (Bone, Material, etc.) |
| `PrimitiveType` | `H3DPrimitiveType` (Transform, QuatTransform, MtxTransform, etc.) |

**H3DPrimitiveType enum:**
```
Float, Integer, Vector2D, Vector3D, Transform, RGBA, Texture, QuatTransform, Boolean, MtxTransform
```

### 3.4 H3DAnimTransform (Euler)

9 independent `H3DFloatKeyFrameGroup` channels:
- ScaleX, ScaleY, ScaleZ
- RotationX, RotationY, RotationZ
- TranslationX, TranslationY, TranslationZ

Each group has `Exists` bool and `GetFrameValue(float frame)` with hermite interpolation.

### 3.5 H3DAnimQuatTransform (Quaternion)

Per-frame baked lists:
- `List<Vector3> Scales` — one per frame
- `List<Quaternion> Rotations` — one per frame
- `List<Vector3> Translations` — one per frame

| Method | Returns |
|--------|---------|
| `GetScaleValue(int Frame)` | `Vector3` |
| `GetRotationValue(int Frame)` | `Quaternion` |
| `GetTranslationValue(int Frame)` | `Vector3` |

### 3.6 DAE (COLLADA Exporter)

**Namespace:** `SPICA.Formats.Generic.COLLADA`
**Purpose:** Exports H3D scene to COLLADA 1.4.1 XML.

| Constructor | Purpose |
|-------------|---------|
| `DAE()` | Empty |
| `DAE(H3D Scene, int MdlIndex, int AnimIndex = -1)` | Build full DAE from H3D |

| Method | Returns | Purpose |
|--------|---------|---------|
| `Save(string FileName)` | `void` | Serialize to XML file |

### 3.7 DAENode

| Method | Purpose |
|--------|---------|
| `SetBoneEuler(Vector3 T, Vector3 R, Vector3 S)` | Set per-component rest pose |

**SetBoneEuler output order:**
```xml
<translate sid="translate">x y z</translate>
<rotate sid="rotateZ">0 0 1 angle_deg</rotate>
<rotate sid="rotateY">0 1 0 angle_deg</rotate>
<rotate sid="rotateX">1 0 0 angle_deg</rotate>
<scale sid="scale">x y z</scale>
```

### 3.8 GFModel (Sun/Moon Model)

**Namespace:** `SPICA.Formats.GFL2.Model`
**Magic:** `0x15122117`

| Field | Type |
|-------|------|
| `Name` | `string` |
| `Skeleton` | `List<GFBone>` |
| `LUTs` | `List<GFLUT>` |
| `Materials` | `List<GFMaterial>` |
| `Meshes` | `List<GFMesh>` |

| Method | Returns | Purpose |
|--------|---------|---------|
| `GFModel(BinaryReader, string)` | — | Parse from stream |
| `ToH3DModel()` | `H3DModel` | Convert to H3D (bones, materials, meshes) |
| `Write(BinaryWriter)` | `void` | Serialize back |

### 3.9 GFMotion (Sun/Moon Animation)

**Namespace:** `SPICA.Formats.GFL2.Motion`
**Magic:** `0x00060000`

| Field | Type |
|-------|------|
| `FramesCount` | `uint` |
| `IsLooping` | `bool` |
| `SkeletalAnimation` | `GFSkeletonMot` |
| `MaterialAnimation` | `GFMaterialMot` |
| `VisibilityAnimation` | `GFVisibilityMot` |

| Method | Returns |
|--------|---------|
| `ToH3DSkeletalAnimation(H3DDict<H3DBone>)` | `H3DAnimation` |
| `ToH3DMaterialAnimation()` | `H3DMaterialAnim` |
| `ToH3DVisibilityAnimation()` | `H3DAnimation` |

### 3.10 GFSkeletonMot (Skeletal Animation Converter)

**Namespace:** `SPICA.Formats.GFL2.Motion`
**Purpose:** Converts GFL2 skeletal animation to H3D format.

Key behavior:
- Reads bone names + per-bone keyframe channels (9 channels: SRT × XYZ)
- **isAxisAngle handling** at import time: converts axis-angle rotation to Quaternion
- Non-axis-angle: converts Euler to Quaternion via `CreateFromAxisAngle` chain
- Output: `H3DAnimQuatTransform` elements (baked per-frame Quaternion + Scale + Translation)

```csharp
// isAxisAngle path:
Quaternion = CreateFromAxisAngle(Normalize(rotation), rotation.Length() * 2);

// Normal Euler path:
Quaternion = CreateFromAxisAngle(UnitZ, rz) * CreateFromAxisAngle(UnitY, ry) * CreateFromAxisAngle(UnitX, rx);
```

### 3.11 FormatIdentifier

**Namespace:** `SPICA.WinForms.Formats`
**Purpose:** Identifies file format by extension and magic bytes, returns H3D.

Detection chain:
1. Extension: `.smd` → SMD, `.obj` → OBJ, `.mbn` → MBn
2. 4-char magic: `"BCH\0"` → H3D.Open, `"MOD"` → MTModel, `"TEX"` → MTTexture, `"MFX"` → MTShader, `"CGFX"` → Gfx, `"GFLXPAK"` → GFLXPack
3. GFPackage (2-byte uppercase magic):
   - `"AD"` → GFPackedTexture (start=1)
   - `"BG"` → GFL2OverWorld
   - `"BS"` → GFBtlSklAnim
   - `"CM"` → GFCharaModel
   - `"GR"` → GFOWMapModel
   - `"MM"` → GFOWCharaModel
   - `"PC"` → GFPkmnModel
   - `"PT"` → GFPackedTexture (start=0)
   - `"PK"/"PB"` → GFPkmnSklAnim
4. uint magic: `0x15122117` → GFModel, `0x15041213` → GFTexture, `0x00010000` → GFModelPack, `0x00060000` → GFMotion

### 3.12 GFPackage (Container Reader)

**Namespace:** `SPICA.WinForms.Formats`
**Purpose:** Reads 2-byte-magic Pokemon containers (same as Ohana's PkmnContainer).

| Method | Returns |
|--------|---------|
| `GetPackageHeader(Stream)` | `Header { Magic, Entry[] }` |
| `IsValidPackage(Stream)` | `bool` |

### 3.13 TextureConverter

**Namespace:** `SPICA.PICA.Converters`
**Purpose:** Decodes/encodes PICA200 texture formats.

| Method | Returns | Purpose |
|--------|---------|---------|
| `DecodeBuffer(byte[], int, int, PICATextureFormat)` | `byte[]` | Raw → RGBA8 buffer |
| `DecodeBitmap(byte[], int, int, PICATextureFormat)` | `Bitmap` | Raw → System.Drawing.Bitmap |
| `Encode(Bitmap, PICATextureFormat)` | `byte[]` | Bitmap → raw (RGBA8 only) |

Supports: RGBA8, RGB8, RGBA5551, RGB565, RGBA4, LA8, HiLo8, L8, A8, LA4, L4, A4, ETC1, ETC1A4.

### 3.14 VectorExtensions.ToEuler()

**Namespace:** `SPICA.Math3D`
**Purpose:** Convert Quaternion to Euler angles (XYZ).

```csharp
public static Vector3 ToEuler(this Quaternion q)
{
    return new Vector3(
        (float)Math.Atan2(2 * (q.X * q.W + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y)),
        -(float)Math.Asin(2 * (q.X * q.Z - q.W * q.Y)),
        (float)Math.Atan2(2 * (q.X * q.Y + q.Z * q.W), 1 - 2 * (q.Y * q.Y + q.Z * q.Z)));
}
```

---

## 4. The DAE Export Pipeline

This is the most critical section. SPICA's DAE exporter produces **working skeletal animations** in Blender. Here's exactly how it works.

### 4.1 Skeleton Export

For each bone in the skeleton:
1. Create a `DAENode` with `type = JOINT`
2. Call `SetBoneEuler(bone.Translation, bone.Rotation, bone.Scale)`
3. This outputs per-component XML with SIDs that match animation channels:

```xml
<node id="Bone_bone_id" name="Bone" sid="Bone" type="JOINT">
  <translate sid="translate">0 5.2 0</translate>
  <rotate sid="rotateZ">0 0 1 0</rotate>
  <rotate sid="rotateY">0 1 0 0</rotate>
  <rotate sid="rotateX">1 0 0 0</rotate>
  <scale sid="scale">1 1 1</scale>
  <!-- child nodes -->
</node>
```

### 4.2 Animation Export

For each animation element (each animated bone):

1. **Skip unsupported types:** Only `Transform` and `QuatTransform` are handled. `MtxTransform` is skipped.

2. **For each of 5 channels:** translate, rotateX, rotateY, rotateZ, scale

3. **Check if channel exists:**
   - For `Transform`: check `TranslationExists`, `RotationX/Y/Z.Exists`, `ScaleExists`
   - For `QuatTransform`: check `HasTranslation`, `HasRotation`, `HasScale`
   - Skip channel if not animated

4. **Bake every frame** from 0 to `FramesCount` (inclusive):
   - Time = `Frame / 30.0f` (30fps)
   - Interpolation = `"LINEAR"` for all frames

5. **Per frame, per channel value:**
   - **Translation (i=0):** `Vector3(TranslationX, TranslationY, TranslationZ)` — use keyframe value if exists, else rest pose
   - **RotationX/Y/Z (i=1,2,3):** `RadToDeg(rotationChannel.GetFrameValue(frame))`
   - **Scale (i=4):** `InvScale * Vector3(ScaleX, ScaleY, ScaleZ)` — InvScale compensates parent

6. **For QuatTransform:** Convert quaternion to Euler via `GetRotationValue(frame).ToEuler()`, then extract X/Y/Z component

7. **Segment scale compensation:**
   ```csharp
   if (Parent != null && (SklBone.Flags & H3DBoneFlags.IsSegmentScaleCompensate) != 0)
   {
       if (PElem != null)
           InvScale /= parentAnimScale;      // Animated parent scale
       else
           InvScale /= Parent.Scale;          // Rest pose parent scale
   }
   ```

8. **Output format:**
   - Rotation channels: `source` with stride=1, param `ANGLE` (float)
   - Translation/Scale channels: `source` with stride=3, params `X/Y/Z` (float)
   - Channel target: `{BoneName}_bone_id/{channelSID}` (+ `.ANGLE` for rotations)

### 4.3 Material Export

- Each material gets unique SIDs: `{MaterialName}_surf`, `{MaterialName}_samp`
- Effect references texture by name from `library_images`
- **No SID collision bug** — unlike Ohana's `img_surface` / `img_sampler` for all materials

### 4.4 Mesh Export

- Uses `MeshTransform.GetWorldSpaceVertices()` to transform rigid-skinned vertices
- Smooth-skinned vertices are already in world space
- Bone indices remapped from SubMesh-local to skeleton-global
- Skinning weights exported in `<vertex_weights>` with bone name/weight sources

### 4.5 Texture Export

- `H3DTexture.ToBitmap()` decodes raw PICA200 texture to `System.Drawing.Bitmap`
- Saved as PNG alongside DAE

---

## 5. Files That Can Be Directly Used

### 5.1 The SPICA Core Library (ALL of it)

The entire `SPICA/` project is **GUI-free**. It depends only on:
- `System.Numerics` (vectors, matrices, quaternions)
- `System.Drawing` (Bitmap for texture decode — replaceable with ImageSharp)
- `System.Xml` (DAE XML serialization)

**All 441 .cs files can be used as-is.** No modifications needed for CLI.

### 5.2 WinForms Files — Reusable Logic

These files from `SPICA.WinForms/Formats/` contain format-detection and loading logic that must be ported to the CLI:

| File | Lines | GUI Dependencies | Needed For |
|------|-------|-----------------|------------|
| `FormatIdentifier.cs` | 229 | `MessageBox`, `OpenFileDialog` (MT shader prompt) | Core — format detection |
| `GFPackage.cs` | 81 | None | Core — container reader |
| `GFPkmnModel.cs` | 159 | None | Pokemon model loading |
| `GFPkmnSklAnim.cs` | 59 | None | Pokemon animation loading |
| `GFBtlSklAnim.cs` | ~50 | None | Battle animation loading |
| `GFCharaModel.cs` | ~30 | None | Character model loading |
| `GFOWCharaModel.cs` | ~30 | None | Overworld character loading |
| `GFOWMapModel.cs` | ~30 | None | Overworld map loading |
| `GFL2OverWorld.cs` | ~50 | None | Overworld loader |
| `GFPackedTexture.cs` | ~50 | None | Texture container loading |
| `FileIO.cs` | 140 | `SaveFileDialog`, `FolderBrowserDialog`, `MessageBox`, `Renderer` | Replace entirely |
| `TextureManager.cs` | ~30 | Bitmap cache for GUI | Not needed |

**Strategy:** Copy `GFPackage.cs` and all `GF*.cs` loader files as-is. Rewrite `FormatIdentifier.cs` to remove MessageBox/dialog calls. Replace `FileIO.cs` entirely with CLI command logic.

### 5.3 Files NOT Needed for CLI

| Component | Files | Reason |
|-----------|-------|--------|
| `SPICA.Rendering/` | 37 files | OpenGL renderer — not needed for file conversion |
| `SPICA.WinForms/GUI/` | ~15 files | WinForms custom controls |
| `SPICA.WinForms/FrmMain.cs` | 1 file | Main window |
| `SPICA.WinForms/FrmExport.cs` | 1 file | Export dialog |

---

## 6. Phased Rewrite Plan

### Phase 0: Project Scaffold

**Goal:** .NET 8 solution that references SPICA core and builds.

1. Create `SpicaCli.sln` under `src/PokemonGreen.SpicaCli/`
2. Create `SpicaCli.App` console project (net8.0)
   - Add `System.CommandLine` (2.0-beta4)
   - Add project reference to SPICA core
3. Decide on SPICA integration strategy:
   - **Option A (Recommended):** Copy SPICA source into solution, upgrade to net8.0
   - **Option B:** Reference SPICA as a built DLL
   - **Option C:** Add SPICA as a git submodule
4. **Upgrade SPICA.csproj to net8.0** — change `TargetFrameworkVersion` to `TargetFramework`, remove old-style project structure
5. Replace `System.Drawing.Bitmap` usage:
   - **Option A:** Add `System.Drawing.Common` NuGet (Windows-only, quick)
   - **Option B:** Replace with `SixLabors.ImageSharp` (cross-platform, ~2 hours work)
6. Copy format loader files from `SPICA.WinForms/Formats/` (GFPackage, GFPkmnModel, etc.)
7. Create minimal `Program.cs` with `--version` command

### Phase 1: Format Detection & Loading

**Goal:** Load any Pokemon 3DS file → H3D.

1. Port `FormatIdentifier.cs`:
   - Remove `MessageBox`, `OpenFileDialog`, `Renderer` references
   - MT Framework shader: skip or load from CLI argument
   - Return `H3D?` (null for unsupported)
2. Port `GFPackage.cs` as-is (no GUI deps)
3. Port all `GF*.cs` loaders as-is (no GUI deps)
4. Add GARC support:
   - SPICA doesn't have a GARC reader! Ohana's `GARC.cs` must be ported or rewritten
   - GARC reads "CRAG" magic, FATO/FATB sections → entry offsets/lengths
   - Can copy Ohana's GARC.cs (97 lines, pure binary parsing)
5. Create `LoadFile(string path)` that:
   - Detects GARC → iterates entries
   - For each entry, calls `FormatIdentifier.IdentifyAndOpen()`
   - Merges results into single `H3D`

### Phase 2: Export Pipeline

**Goal:** Export H3D → DAE + PNG files.

1. Use SPICA's `DAE` class directly:
   ```csharp
   var dae = new DAE(h3d, modelIndex: 0, animIndex: 0);
   dae.Save(outputPath);
   ```
2. Export textures:
   ```csharp
   foreach (var tex in h3d.Textures)
   {
       tex.ToBitmap().Save(Path.Combine(outDir, $"{tex.Name}.png"));
   }
   ```
3. Add SMD export option using SPICA's `SMD` class

### Phase 3: CLI Commands

1. **`info <file>`** — Load file, print: format, model count, mesh count, bone count, texture count, animation count
2. **`convert <file> -o <dir> [-f dae|smd|obj] [-n count] [--anim index]`** — Convert file to export format
3. **`batch <dir> -o <dir> [-f dae]`** — Batch convert directory
4. **`convert-all <garcDir> -o <dir>`** — Mass GARC extraction with model+texture grouping

### Phase 4: GARC Pokemon Extraction

**Goal:** Extract Pokemon models from the 1.3GB GARC with proper grouping.

1. Iterate GARC entries 0..10548
2. For each entry, try `FormatIdentifier.IdentifyAndOpen()`:
   - PC container (Pokemon model) → loads model + textures + shaders
   - GFMotion → loads animation (needs skeleton from previous model)
   - GFTexture → loads texture
3. Group: model + following animations + textures
4. Export each group to named folder (e.g., `pm0001_00/`)

### Phase 5: Testing & Validation

1. Export Bulbasaur (entry 1) → verify DAE opens in Blender with correct mesh, textures, skeleton
2. Export with animation → verify idle animation plays correctly (no distortion!)
3. Batch export first 50 entries → verify all Pokemon export without errors
4. Compare output against SPICA WinForms GUI export (should be identical)

---

## 7. Known Bugs and Limitations

### 7.1 Critical: No GARC Reader

SPICA does not include a GARC container reader. The WinForms app loads files via file dialogs — no archive iteration support.

**Fix:** Port Ohana's `GARC.cs` (97 lines) or write a new one.

### 7.2 System.Drawing.Bitmap Dependency

`TextureConverter.cs` and `H3DTexture.cs` use `System.Drawing.Bitmap` for texture decode/encode. This requires the Windows-only `System.Drawing.Common` package on .NET 8.

**Fix:** Replace with `SixLabors.ImageSharp.Image<Rgba32>`. Affected files:
- `SPICA/PICA/Converters/TextureConverter.cs` (DecodeBitmap, Encode, GetBitmap, GetBuffer)
- `SPICA/Formats/CtrH3D/Texture/H3DTexture.cs` (ToBitmap, constructors)

### 7.3 MtxTransform Animation Not Exported

The DAE exporter skips `MtxTransform` (baked matrix) animation elements:
```csharp
if (Elem.PrimitiveType != H3DPrimitiveType.Transform &&
    Elem.PrimitiveType != H3DPrimitiveType.QuatTransform) continue;
```

This is the same limitation as Ohana. No known Pokemon models use MtxTransform for skeletal animation.

### 7.4 .NET Framework 4.7.1

SPICA targets .NET Framework 4.7.1 (old-style .csproj). Must be upgraded to .NET 8 SDK-style project.

**Fix:** Convert `.csproj` to SDK-style:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
  </ItemGroup>
</Project>
```

### 7.5 FormatIdentifier Uses WinForms Dialogs

`FormatIdentifier.cs` shows a `MessageBox` when MT Framework shader is missing and opens an `OpenFileDialog`. Must be removed for CLI.

### 7.6 GFL1 vs GFL2 isAxisAngle Handling

`GFSkeletonMot` (GFL2) handles `isAxisAngle` at import time by converting to Quaternion. `GF1Motion` (GFL1) may handle it differently. Only GFL2 is relevant for Sun/Moon.

### 7.7 Texture Y-Flip

SPICA does NOT flip UV Y coordinates in the DAE exporter. Ohana's CLI version flipped them (`1.0 - y`). The correct behavior depends on the target application:
- Blender: usually expects flipped
- Unity/Godot: may not need flipping

**May need investigation** when testing exports.

---

## 8. Optimizations and Quick Wins

### 8.1 Quick Wins

1. **Upgrade to .NET 8 SDK-style project** — enables modern C# features, NuGet restore, etc.
2. **Add GARC reader** — copy Ohana's 97-line GARC.cs or write minimal reader
3. **Replace System.Drawing with ImageSharp** — enables cross-platform, modern PNG encoding
4. **Add `-n` flag for entry limit** — same as old CLI, useful for testing
5. **Stream large GARCs** — lazy-load entries instead of reading entire 1.3GB into memory

### 8.2 Architecture Improvements

1. **Parallel GARC processing** — each entry is independent, use `Parallel.ForEach`
2. **Progress reporting** — add `IProgress<T>` for CLI progress bars
3. **Structured logging** — replace `Console.Error.WriteLine` with logging framework
4. **Error accumulator** — collect all errors, print summary at end (like old CLI's GarcStats)

### 8.3 Export Enhancements

1. **glTF export** — more widely supported than DAE in modern tools
2. **FBX export** — industry standard for game engines
3. **Animation-only export** — export just animations for models already exported
4. **Batch animation export** — export all animations for a single model

---

## 9. Build and Test Commands

### 9.1 Current SPICA Build (Reference)

```bash
# SPICA currently uses .NET Framework 4.7.1 — builds with MSBuild
# After conversion to .NET 8:

# Build core library
dotnet build src/PokemonGreen.SpicaCli/SPICA/SPICA.csproj

# Build CLI app
dotnet build src/PokemonGreen.SpicaCli/SpicaCli.App/SpicaCli.App.csproj

# Build entire solution
dotnet build src/PokemonGreen.SpicaCli/SpicaCli.sln
```

### 9.2 CLI Usage (Planned)

```bash
# Shorthand
CLI="dotnet run --project src/PokemonGreen.SpicaCli/SpicaCli.App/SpicaCli.App.csproj --"

# Info about a file
$CLI info "path/to/file.bch"

# Convert a single BCH file to DAE
$CLI convert "path/to/file.bch" -o output/ -f dae

# Convert Pokemon GARC with first N entries
$CLI convert "path/to/RomFS/a/0/9/4" -o exports/ -n 20

# Convert with specific animation
$CLI convert "path/to/file.bch" -o output/ -f dae --anim 0

# Batch convert all files in directory
$CLI batch "path/to/dump/" -o output/ -f dae
```

### 9.3 Test Validation

```bash
# Quick test: export Bulbasaur (entry 1 of Pokemon GARC)
$CLI convert "sun-moon-dump/RomFS/a/0/9/4" -o exports/ -n 10

# Verify in Blender:
# 1. Import model.dae
# 2. Check textures bind correctly (body, eyes, iris)
# 3. Play animation — should see idle breathing, NO distortion
# 4. Check bone hierarchy in armature

# Full Pokemon export
$CLI convert "sun-moon-dump/RomFS/a/0/9/4" -o exports/

# Compare against SPICA WinForms export:
# 1. Open GARC entry in SPICA WinForms
# 2. File → Save → COLLADA 1.4.1
# 3. Diff CLI output vs WinForms output — should be byte-identical
```

### 9.4 Dependencies

| Package | Version | Project | Purpose |
|---------|---------|---------|---------|
| `System.CommandLine` | 2.0.0-beta4 | SpicaCli.App | CLI argument parsing |
| `System.Drawing.Common` | 8.0.0 | SPICA | Bitmap/Color (or replace with ImageSharp) |
| `SixLabors.ImageSharp` | 3.1.x | SPICA (optional) | Cross-platform image I/O |

### 9.5 .NET SDK Requirement

- .NET 8.0 SDK or later
- Target framework: `net8.0`

---

## Appendix A: Format Magic Bytes Quick Reference

| Magic (hex) | Magic (ASCII) | Format | Loader |
|-------------|---------------|--------|--------|
| `0x15122117` | — | GFModel (single) | `new GFModel(Reader, name)` |
| `0x15041213` | — | GFTexture | `new GFTexture(Reader)` |
| `0x00010000` | — | GFModelPack | `new GFModelPack(Reader)` |
| `0x00060000` | — | GFMotion | `new GFMotion(Reader, idx)` |
| `42 43 48 00` | `BCH\0` | BCH/H3D | `H3D.Open(bytes)` |
| `43 47 46 58` | `CGFX` | CGFX | `Gfx.Open(stream)` |
| `4D 4F 44` | `MOD` | MT Model | `new MTModel(reader)` |
| `54 45 58` | `TEX` | MT Texture | `new MTTexture(reader)` |
| `4D 46 58` | `MFX` | MT Shader | `new MTShaderEffects(reader)` |
| `47464C58504143` | `GFLXPAC` | GFLX Pack | `new GFLXPack(reader)` |
| `43 52 41 47` | `CRAG` | GARC | **Not in SPICA** — port from Ohana |
| `[A-Z][A-Z]` | 2 uppercase | GF Package | `GFPackage.GetPackageHeader()` |

### GF Package Sub-Types

| Magic | Handler | Content |
|-------|---------|---------|
| `PC` | `GFPkmnModel` | Pokemon model (high+low poly + shaders) |
| `PK`/`PB` | `GFPkmnSklAnim` | Pokemon skeletal animation pack |
| `BS` | `GFBtlSklAnim` | Battle skeletal animation |
| `CM` | `GFCharaModel` | Character model |
| `MM` | `GFOWCharaModel` | Overworld character model |
| `GR` | `GFOWMapModel` | Overworld map model |
| `BG` | `GFL2OverWorld` | Overworld environment |
| `AD` | `GFPackedTexture` | Map texture (start=1) |
| `PT` | `GFPackedTexture` | Pokemon texture (start=0) |

## Appendix B: Animation Type Comparison

| Aspect | Ohana3DS | SPICA |
|--------|----------|-------|
| **Rest pose format** | Matrix node (`<matrix>`) | Per-component (`translate`, `rotateZ/Y/X`, `scale`) |
| **Animation format** | Per-component (after our rewrite) or matrix | Per-component with SID matching |
| **Rotation SIDs** | `rotateX/Y/Z` | `rotateZ`, `rotateY`, `rotateX` (Z first in XML) |
| **Frame baking** | Samples at keyframes only | **Bakes every frame** at 30fps |
| **Quaternion→Euler** | Our `quaternionToEuler()` copy | `ToEuler()` extension on `System.Numerics.Quaternion` |
| **Segment scale comp** | Not handled | `InvScale /= parentScale` per-frame |
| **Channel target** | `{boneName}/{sid}` | `{boneName}_bone_id/{sid}` |
| **Rotation target** | `{sid}` | `{sid}.ANGLE` |
| **Interpolation** | LINEAR | LINEAR |
| **Works in Blender?** | **No** (distortion) | **Yes** |

## Appendix C: Transform Order Reference

SPICA's bone `Transform` property:
```
System.Numerics (row-major, row-vector): S * Rx * Ry * Rz * T
Column-vector equivalent: T * Rz * Ry * Rx * S
```

SPICA's DAE `SetBoneEuler` XML order:
```xml
<translate sid="translate"/>   <!-- Applied last (outermost) -->
<rotate sid="rotateZ"/>        <!-- Applied 4th -->
<rotate sid="rotateY"/>        <!-- Applied 3rd -->
<rotate sid="rotateX"/>        <!-- Applied 2nd -->
<scale sid="scale"/>           <!-- Applied first (innermost) -->
```

COLLADA applies transforms **bottom-to-top** in XML, so the effective order is:
`scale → rotateX → rotateY → rotateZ → translate` = `S * Rx * Ry * Rz * T`

This matches the `H3DBone.Transform` property exactly.
