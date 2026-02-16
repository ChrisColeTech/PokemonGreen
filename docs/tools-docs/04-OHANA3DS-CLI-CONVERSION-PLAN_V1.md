# Ohana3DS CLI Bulk Model Conversion Plan

## 1. Architecture Overview

### 1.1 Project Structure

Ohana3DS-Rebirth is a **single-project C# Windows Forms application** targeting **.NET Framework 3.5**. It uses **Managed DirectX 9** (Microsoft.DirectX, Microsoft.DirectX.Direct3D, Microsoft.DirectX.Direct3DX) for 3D viewport rendering.

**Solution:** `D:\Projects\Ohana3DS-Rebirth\Ohana3DS Rebirth.sln`
**Project:** `D:\Projects\Ohana3DS-Rebirth\Ohana3DS Rebirth\Ohana3DS Rebirth.csproj`

```
Ohana3DS Rebirth/
  Program.cs                     # Entry point (WinForms Application.Run)
  FrmMain.cs                     # Main window, file open/drag-drop handler
  OForm.cs                       # Custom base form

  Ohana/                         # CORE LOGIC (mostly GUI-independent)
    FileIO.cs                    # Master file dispatcher (magic-byte detection)
    IOUtils.cs                   # Binary read helpers
    RenderBase.cs                # ALL internal data structures (2296 lines)
    RenderEngine.cs              # DirectX 9 3D viewport (GUI-bound)
    TextureCodec.cs              # Texture format decoder (RGBA8, ETC1, etc.)
    TextureUtils.cs              # Texture helper utilities
    PatriciaTree.cs              # PATRICIA tree implementation (BCH name tables)

    Models/
      BCH.cs                     # BCH format loader (2171 lines) - PRIMARY FORMAT
      CGFX.cs                    # CGFX/BCRES format loader
      MBN.cs                     # MBN format loader
      MeshUtils.cs               # Mesh optimization, bounds calculation
      ZMDL.cs                    # ZMDL format loader

      GenericFormats/             # EXPORT FORMATS
        DAE.cs                   # Collada DAE exporter (1059 lines)
        OBJ.cs                   # Wavefront OBJ exporter/importer (171 lines)
        SMD.cs                   # Source Model (Valve) exporter/importer
        CMDL.cs                  # Nintendo CMDL (XML) exporter (936 lines)

      PICA200/                   # 3DS GPU command parsing
        PICACommand.cs           # PICA200 GPU command definitions
        PICACommandReader.cs     # PICA200 command stream reader
        PICACommandWriter.cs     # PICA200 command stream writer

      PocketMonsters/            # Pokemon-specific formats
        GfModel.cs               # Sun/Moon GfModel loader (magic 0x00010000, 0x15122117)
        PC.cs                    # PC container loader (PkmnContainer + BCH)
        CM.cs, CP.cs, GR.cs, MM.cs  # Other Pokemon container formats

      NewLovePlus/               # NewLovePlus-specific formats (irrelevant)

    Textures/
      3DST.cs, BCLIM.cs, DMP.cs, ZTEX.cs  # Various texture formats
      PocketMonsters/
        GfTexture.cs             # Sun/Moon texture loader (magic 0x15041213)
        AD.cs, PT.cs             # Other Pokemon texture/data formats

    Animations/
      BS.cs                      # BS animation format
      GfMotion.cs                # Sun/Moon motion/animation loader

    Compressions/
      BLZ.cs, LZSS.cs, LZSS_Ninty.cs, Yaz0.cs  # Decompression algorithms

    Containers/
      GARC.cs                    # GARC archive (Sun/Moon RomFS uses this)
      PkmnContainer.cs           # Generic Pokemon 2-letter container (PC, GR, etc.)
      DARC.cs, SARC.cs, FPT0.cs, DQVIIPack.cs  # Other containers

  GUI/                           # ALL GUI COMPONENTS (WinForms)
    Forms/
      OModelExportForm.cs        # Model export dialog (format selection, batch)
      OTextureExportForm.cs      # Texture export dialog (PNG save)
    Panels/
      OModelsPanel.cs, OTexturesPanel.cs, OViewportPanel.cs, etc.
    (Custom controls: OButton, OList, OTextBox, etc.)

  Tools/
    OBCHTextureReplacer.cs       # BCH texture replacement tool (GUI)
    OSm4shModelCreator.cs        # Smash Bros model creator tool (GUI)
```

### 1.2 Key Dependencies

| Dependency | Usage | CLI Impact |
|---|---|---|
| .NET Framework 3.5 | Target framework | Must target 3.5 or retarget |
| Microsoft.DirectX.Direct3D | 3D viewport rendering (RenderEngine.cs) | **NOT needed for CLI** |
| System.Drawing (GDI+) | Bitmap/texture handling, Color struct | **Required** (texture decode/save) |
| System.Windows.Forms | GUI, dialogs, export forms | **Must eliminate** for CLI |

### 1.3 GUI vs Logic Separation

**GUI-bound code (must NOT be used in CLI):**
- `FrmMain.cs` -- file open dialogs
- `GUI/` -- all panels, forms, custom controls
- `RenderEngine.cs` -- DirectX 3D viewport
- `FileIO.import()` and `FileIO.export()` -- use OpenFileDialog/SaveFileDialog
- `Tools/` -- GUI-based tools

**Pure logic (CAN be used in CLI directly):**
- `FileIO.load(string fileName)` and `FileIO.load(Stream data)` -- file format detection and loading
- `RenderBase.cs` -- all data structures
- `Models/BCH.cs` -- BCH loader
- `Models/PocketMonsters/GfModel.cs` -- Sun/Moon model loader
- `Models/PocketMonsters/PC.cs` -- PC container loader
- `Models/GenericFormats/DAE.cs`, `OBJ.cs`, `SMD.cs`, `CMDL.cs` -- exporters
- `TextureCodec.cs` -- texture decoding
- All compression and container loaders
- `MeshUtils.cs` -- mesh optimization

**Complication:** `FileIO.cs` line 5 has `using System.Windows.Forms;` because the `import()` and `export()` methods use dialogs. The `load()` methods themselves do NOT use WinForms. The CLI can call `FileIO.load()` directly but needs to avoid `import()`/`export()`.

---

## 2. Format Support

### 2.1 Formats Ohana3DS Handles

| Magic / Extension | Format | Handler | Type |
|---|---|---|---|
| `BCH` (3 bytes) | BCH model/material/texture | `BCH.load()` | model |
| `CGFX` | BCRES/CGFX model | `CGFX.load()` | model |
| `0x00010000` | GfModel (Sun/Moon) | `GfModel.load()` | model |
| `0x15122117` | GfModel single model | `GfModel.loadModel()` | model |
| `0x15041213` | GfTexture (Sun/Moon) | `GfTexture.load()` | image |
| `PC` (2 bytes) | Pokemon PC container | `PC.load()` | model |
| `CM` | Pokemon CM container | `CM.load()` | model |
| `GR` | Pokemon GR container | `GR.load()` | model |
| `MM` / `BM` | Pokemon MM container | `MM.load()` | model |
| `CP` | Pokemon CP container | `CP.load()` | model |
| `CRAG` (reversed = GARC) | GARC archive | `GARC.load()` | container |
| `PT` | Pokemon texture container | `PT.load()` | texture |
| `AD` | Pokemon AD format | `AD.load()` | model |
| `.mbn` | MBN model | `MBN.load()` | model |
| `zmdl` | ZMDL model | `ZMDL.load()` | model |

### 2.2 Sun/Moon Dump File Structure

The sun-moon dump at `D:\Projects\Ohana3DS-Rebirth\sun-moon-dump\` contains:

```
RomFS/
  a/              # GARC archives, organized by number
    0/            # Pokemon models GARC directory
      0/          # Sub-archives (files 0-9, some empty)
      1/          # Sub-archives with model data
      2/          # More model data
      3/          # Large model files (100KB+)
      ...
    1/, 2/, 3/    # Other data categories
  data/
    sound/        # Audio files (.bcstm)
  *.cro           # Code modules (irrelevant)
```

**The files in `a/` are GARC archives** (magic bytes: `CRAG` = "GARC" reversed). Each GARC contains multiple sub-files which can be:
- GfModel files (magic `0x00010000`) -- Sun/Moon 3D models
- GfTexture files (magic `0x15041213`) -- Sun/Moon textures
- BCH files (magic `BCH`) -- X/Y and OR/AS models
- Animation files, etc.

**The GARC must first be extracted by Ohana's `GARC.load()`**, then each extracted sub-file can be loaded through `FileIO.load()`.

**Pokemon Sun/Moon model pipeline:** GARC archive -> GfModel file -> contains model sections (meshes, materials, textures) + texture sections.

---

## 3. Model Pipeline Deep Dive

### 3.1 Loading Pipeline

```
User opens file
  -> FrmMain.open(fileName)
    -> FileIO.load(fileName)            [FileIO.cs:42]
      -> Detects format by magic bytes   [FileIO.cs:51-173]
      -> Dispatches to correct loader
      -> Returns FileIO.file { data: OModelGroup, type: formatType.model }
```

**FileIO.load() dispatching logic** (`FileIO.cs` lines 51-173):

1. First checks 4-byte magic as uint32 (lines 64-72):
   - `0x00010000` -> `GfModel.load()` (Sun/Moon model container)
   - `0x15122117` -> `GfModel.loadModel()` (single Sun/Moon model)
   - `0x15041213` -> `GfTexture.load()` (Sun/Moon texture)

2. Then checks string magic at various lengths:
   - 4 bytes: `CGFX`, `CRAG`(GARC), `darc`, `SARC`, `zmdl`, etc.
   - 3 bytes: `BCH` -> `BCH.load()` (X/Y, OR/AS models)
   - 2 bytes: `PC`, `CM`, `GR`, `MM`, `BM`, etc.

3. Falls back to compression detection (LZ, BLZ)

### 3.2 Internal Model Representation

**The central data structure is `RenderBase.OModelGroup`** (`RenderBase.cs` lines 2241-2294):

```csharp
public class OModelGroup {
    public List<OModel> model;           // List of 3D models
    public List<OTexture> texture;       // Shared texture list
    public List<OLookUpTable> lookUpTable;
    public List<OLight> light;
    public List<OCamera> camera;
    public List<OFog> fog;
    public OAnimationListBase skeletalAnimation;
    public OAnimationListBase materialAnimation;
    // ... more animation lists, scenes
}
```

**OModel** (`RenderBase.cs` lines 1490-1521):
```csharp
public class OModel {
    public string name;
    public List<OMesh> mesh;             // List of mesh parts
    public List<OBone> skeleton;         // Skeleton bones
    public List<OMaterial> material;     // Material definitions
    public OMatrix transform;            // World transform
}
```

**OMesh** (`RenderBase.cs` lines 774-796):
```csharp
public class OMesh {
    public List<OVertex> vertices;       // Vertex data (position, normal, UV, weights)
    public ushort materialId;            // INDEX into OModel.material list
    public string name;                  // Mesh node name
    public ushort renderPriority;
    public bool isVisible;
}
```

**OMaterial** (`RenderBase.cs` lines 1392-1445):
```csharp
public class OMaterial {
    public string name;                  // Material name
    public string name0;                 // TEXTURE 0 NAME (diffuse)
    public string name1;                 // TEXTURE 1 NAME (secondary)
    public string name2;                 // TEXTURE 2 NAME (tertiary)
    public OTextureCoordinator[] textureCoordinator;  // [3] UV transform
    public OTextureMapper[] textureMapper;             // [3] wrap/filter
    public OFragmentShader fragmentShader;             // Combiner stages
    public OMaterialColor materialColor;               // Colors
    public OFragmentOperation fragmentOperation;       // Blend/depth/stencil
    // ... lighting, fog references
}
```

**OTexture** (`RenderBase.cs` lines 1548-1564):
```csharp
public class OTexture {
    public Bitmap texture;               // Decoded bitmap (System.Drawing)
    public string name;                  // Texture name (matched to material.name0/1/2)
}
```

### 3.3 Critical Data Flow: Material-to-Texture Binding

The binding between meshes, materials, and textures works as follows:

1. **Mesh -> Material:** `OMesh.materialId` is a numeric index into `OModel.material` list
2. **Material -> Texture names:** `OMaterial.name0`, `.name1`, `.name2` are texture name strings
3. **Texture names -> Texture bitmaps:** `OModelGroup.texture` is a flat list of `OTexture` objects; the texture is found by matching `OTexture.name` to `OMaterial.name0`

**In BCH loading** (BCH.cs lines 1342-1344):
```csharp
material.name0 = readString(input);  // Texture 0 name
material.name1 = readString(input);  // Texture 1 name
material.name2 = readString(input);  // Texture 2 name
material.name = readString(input);   // Material name
```

**In BCH mesh loading** (BCH.cs line 1584, 1609):
```csharp
objectEntry.materialId = input.ReadUInt16();  // Read from file
// ...
obj.materialId = objects[objIndex].materialId;  // Assigned to mesh
```

**In GfModel loading** (GfModel.cs lines 172-260):
```csharp
// Materials read texture names per unit (up to 3):
mat.name0 = texName;  // unit 0
mat.name1 = texName;  // unit 1
mat.name2 = texName;  // unit 2

// Meshes get materialId by name matching:
obj.materialId = (ushort)matMeshBinding.IndexOf(obj.name);  // line 260
```

---

## 4. Multi-Material Handling and Bug Analysis

### 4.1 How Multi-Material SHOULD Work

A Pokemon model typically has multiple meshes (body, eyes, mouth, accessories), each with a different `materialId` pointing to a different `OMaterial`, which in turn references different textures via `name0`.

Example for a typical Pokemon:
```
OModel.material[0] = { name: "body_mat",  name0: "body_texture" }
OModel.material[1] = { name: "eye_mat",   name0: "eye_texture" }
OModel.material[2] = { name: "mouth_mat", name0: "mouth_texture" }

OModel.mesh[0] = { name: "body",  materialId: 0 }  -> body_mat -> body_texture
OModel.mesh[1] = { name: "eyes",  materialId: 1 }  -> eye_mat  -> eye_texture
OModel.mesh[2] = { name: "mouth", materialId: 2 }  -> mouth_mat -> mouth_texture
```

### 4.2 How Exporters Handle Materials

#### DAE Exporter (DAE.cs lines 589-1007)

The DAE exporter **correctly creates one material per OModel.material entry** and **correctly assigns materials to geometry**:

```csharp
// Creates materials from OModel.material list (line 607-678):
foreach (RenderBase.OMaterial mat in mdl.material) {
    // Creates material with texture reference using mat.name0
    surface.surface.init_from = mat.name0 + "_id";  // line 624
    // Creates effect with phong shader referencing texture
}

// Creates images from OModelGroup.texture list (line 597-605):
foreach (RenderBase.OTexture tex in model.texture) {
    img.init_from = "./" + tex.name + ".png";  // line 602
}

// Assigns material per mesh (line 833):
geometry.mesh.triangles.material = mdl.material[obj.materialId].name;

// Binds material in visual scene (lines 975-976):
node.instance_controller.bind_material...instance_material.symbol = mdl.material[obj.materialId].name;
node.instance_controller.bind_material...instance_material.target = "#" + mdl.material[obj.materialId].name + "_mat_id";
```

**DAE appears to handle multi-material correctly.** Each mesh gets its own material assignment.

#### OBJ Exporter (OBJ.cs lines 18-55)

```csharp
// Line 30: Correctly references per-mesh material
output.AppendLine("usemtl " + mdl.material[mdl.mesh[objIndex].materialId].name0 + ".png");
```

**OBJ references the correct material per mesh** but does NOT write an MTL file. There is no `mtllib` statement and no `.mtl` file generation. This means materials exist as `usemtl` directives but texture paths are not defined in a sidecar file.

### 4.3 Where the Bug Likely Is: FBX Export Does Not Exist

**Critical finding: Ohana3DS-Rebirth has NO FBX exporter.** The available export formats are:
1. **DAE** (Collada) - `DAE.cs`
2. **SMD** (Source Model) - `SMD.cs`
3. **OBJ** (Wavefront) - `OBJ.cs`
4. **CMDL** (Nintendo XML) - `CMDL.cs`

The export form (`OModelExportForm.cs` lines 67-74) shows exactly these four options:
```csharp
if (RadioSMD.Checked) format = 1;
else if (RadioOBJ.Checked) format = 2;
else if (RadioCMDL.Checked) format = 3;
// Default (0) = DAE
```

**If FBX files were produced with broken materials, they were NOT exported by Ohana3DS directly.** They were likely created by:
1. Exporting to DAE from Ohana3DS
2. Importing DAE into another tool (Blender, Autodesk, etc.)
3. Re-exporting to FBX from that tool

The material loss likely happened during the DAE-to-FBX conversion step, possibly because:
- Textures were not saved alongside the DAE (they must be manually exported separately)
- The importing tool could not resolve texture paths
- The DAE exporter's texture binding uses an image library that references `./texturename.png` but the PNG files were not present

### 4.4 Texture Export Is Separate

**Textures are NOT automatically saved during model export.** They must be exported separately using `OTextureExportForm` (`OTextureExportForm.cs` lines 57-69):

```csharp
// Saves each texture as PNG
foreach (RenderBase.OTexture tex in mdls.texture) {
    string fileName = Path.Combine(TxtOutFolder.Text, tex.name) + ".png";
    tex.texture.Save(fileName);
}
```

This is a major workflow issue for bulk conversion: you must export models AND textures to the same directory, and the texture filenames must match the `name0` references in the materials.

### 4.5 DAE Exporter Material Bug: Shared Surface/Sampler IDs

There IS a subtle bug in the DAE exporter. Looking at `DAE.cs` lines 620-668:

```csharp
foreach (RenderBase.OMaterial mat in mdl.material) {
    // ...
    daeParam surface = new daeParam();
    surface.sid = "img_surface";           // SAME SID FOR ALL MATERIALS
    surface.surface.init_from = mat.name0 + "_id";  // Different texture per material

    daeParam sampler = new daeParam();
    sampler.sid = "img_sampler";           // SAME SID FOR ALL MATERIALS
    // ...
    eff.profile_COMMON.technique.phong.diffuse.texture.texture = "img_sampler";
}
```

The surface and sampler `sid` values are always `"img_surface"` and `"img_sampler"` regardless of material. While each effect gets its own profile_COMMON with its own newparam list, some DAE importers may get confused by duplicate sids across effects. **This is not technically invalid COLLADA but could cause issues with some importers.**

---

## 5. CLI Conversion Plan

### 5.1 Approach: New CLI Project Referencing Ohana Logic

**Recommended approach:** Create a new .NET console application that references or includes the Ohana3DS core logic files (everything under `Ohana/` except `RenderEngine.cs`).

**Why not modify the existing project:**
- The existing project is .NET Framework 3.5 with WinForms and Managed DirectX
- We only need the loader/exporter logic, not the GUI
- A clean console app avoids all DirectX/WinForms dependencies

### 5.2 Files to Include in CLI Project

**Required core files (from `Ohana3DS Rebirth/Ohana/`):**
```
FileIO.cs                    (needs minor edit to remove WinForms usings/methods)
IOUtils.cs
RenderBase.cs
TextureCodec.cs
TextureUtils.cs
PatriciaTree.cs
MeshUtils.cs (from Models/)

Models/
  BCH.cs
  CGFX.cs                   (if CGFX support needed)
  Models/GenericFormats/DAE.cs
  Models/GenericFormats/OBJ.cs
  Models/GenericFormats/SMD.cs

  Models/PICA200/PICACommand.cs
  Models/PICA200/PICACommandReader.cs

  Models/PocketMonsters/GfModel.cs
  Models/PocketMonsters/PC.cs
  Models/PocketMonsters/CM.cs
  Models/PocketMonsters/CP.cs
  Models/PocketMonsters/GR.cs
  Models/PocketMonsters/MM.cs

Textures/PocketMonsters/GfTexture.cs
Textures/PocketMonsters/AD.cs
Textures/PocketMonsters/PT.cs

Animations/GfMotion.cs
Animations/BS.cs

Compressions/BLZ.cs
Compressions/LZSS.cs
Compressions/LZSS_Ninty.cs

Containers/GARC.cs
Containers/PkmnContainer.cs
Containers/OContainer.cs
```

### 5.3 Required Modifications to Ohana Code

1. **FileIO.cs** -- Remove `using System.Windows.Forms;` and the `import()` / `export()` methods (lines 253-380). Keep `load(string)` and `load(Stream)` intact.

2. **SMD.cs** -- Has `using System.Windows.Forms;` for `MessageBox` in error handling. Replace with `Console.Error.WriteLine` or throw exceptions.

3. **System.Drawing dependency** -- `RenderBase.OTexture` uses `System.Drawing.Bitmap`, and `TextureCodec` creates bitmaps. On .NET Framework this is fine. If targeting .NET Core/5+, need `System.Drawing.Common` NuGet package.

### 5.4 CLI Tool Design

```
OhanaConvert.exe <input-path> <output-path> [options]

Options:
  --format=dae|obj|smd        Export format (default: dae)
  --textures                  Also export textures as PNG
  --recursive                 Process subdirectories
  --pokemon-garc              Treat inputs as GARC archives (extract first)
  --flatten                   Flatten output (no subdirectories)
  --verbose                   Print detailed progress
```

### 5.5 CLI Core Logic (Pseudocode)

```csharp
static void Main(string[] args) {
    // Parse args
    string inputPath = args[0];
    string outputPath = args[1];
    string format = "dae";  // default

    // Find all input files
    var files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories);

    foreach (string file in files) {
        try {
            FileIO.file loaded = FileIO.load(file);

            if (loaded.type == FileIO.formatType.container) {
                // GARC or PkmnContainer: extract and process each sub-file
                OContainer container = (OContainer)loaded.data;
                foreach (var entry in container.content) {
                    ProcessSubFile(entry.data, entry.name, outputPath, format);
                }
            }
            else if (loaded.type == FileIO.formatType.model) {
                ExportModel((RenderBase.OModelGroup)loaded.data, file, outputPath, format);
            }
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Error processing {file}: {ex.Message}");
        }
    }
}

static void ExportModel(RenderBase.OModelGroup models, string sourcePath,
                         string outputPath, string format) {
    string baseName = Path.GetFileNameWithoutExtension(sourcePath);
    string outDir = Path.Combine(outputPath, baseName);
    Directory.CreateDirectory(outDir);

    // Export all textures as PNG
    foreach (RenderBase.OTexture tex in models.texture) {
        string texPath = Path.Combine(outDir, tex.name + ".png");
        tex.texture.Save(texPath);
        Console.WriteLine($"  Texture: {tex.name}.png");
    }

    // Export each model
    for (int i = 0; i < models.model.Count; i++) {
        string modelName = models.model[i].name ?? $"model_{i}";
        string outFile = Path.Combine(outDir, modelName);

        switch (format) {
            case "dae": DAE.export(models, outFile + ".dae", i); break;
            case "obj": OBJ.export(models, outFile + ".obj", i); break;
            case "smd": SMD.export(models, outFile + ".smd", i); break;
        }

        // Log material/mesh info for verification
        var mdl = models.model[i];
        Console.WriteLine($"  Model: {modelName}");
        Console.WriteLine($"    Meshes: {mdl.mesh.Count}");
        Console.WriteLine($"    Materials: {mdl.material.Count}");
        for (int m = 0; m < mdl.mesh.Count; m++) {
            var mesh = mdl.mesh[m];
            var mat = mdl.material[mesh.materialId];
            Console.WriteLine($"    Mesh '{mesh.name}' -> Material '{mat.name}' -> Texture '{mat.name0}'");
        }
    }
}
```

### 5.6 Handling the GARC Archive Pipeline

For Sun/Moon Pokemon models, the pipeline is:

```
GARC file (a/0/3/0, etc.)
  -> GARC.load() returns OContainer with sub-files
    -> Each sub-file is fed back to FileIO.load()
      -> May be GfModel (0x00010000) -> OModelGroup
      -> May be GfTexture (0x15041213) -> OTexture
      -> May be BCH -> OModelGroup
      -> May be another container (PC, GR, etc.)
```

The CLI needs to recursively process containers. The `PC.load()` method (`PC.cs`) already does this internally:

```csharp
public static RenderBase.OModelGroup load(Stream data) {
    OContainer container = PkmnContainer.load(data);
    foreach (OContainer.fileEntry file in container.content) {
        FileIO.file loaded = FileIO.load(new MemoryStream(file.data));
        switch (loaded.type) {
            case FileIO.formatType.model: models.merge((RenderBase.OModelGroup)loaded.data); break;
            case FileIO.formatType.image: models.texture.Add((RenderBase.OTexture)loaded.data); break;
        }
    }
    return models;
}
```

For GARC archives, we need similar logic but GARC returns an `OContainer`, not an `OModelGroup`. The CLI must:
1. Load GARC -> get OContainer
2. For each sub-file in container, call FileIO.load()
3. Merge all models and textures into a single OModelGroup
4. Export the merged group

### 5.7 Ensuring Proper Multi-Material Export

To guarantee correct multi-material output:

1. **Use DAE format** -- It has the best material support of the available exporters
2. **Always export textures alongside models** -- DAE references `./texturename.png`
3. **Verify material-mesh binding** -- Log each mesh's materialId and corresponding texture
4. **Fix DAE surface/sampler SID uniqueness** -- Change `DAE.cs` to use unique sids per material:

```csharp
// Change from:
surface.sid = "img_surface";
sampler.sid = "img_sampler";

// To:
surface.sid = mat.name + "_surface";
sampler.sid = mat.name + "_sampler";
// And update the texture reference:
eff.profile_COMMON.technique.phong.diffuse.texture.texture = mat.name + "_sampler";
```

5. **Consider adding FBX export** -- If FBX is required, use a library like Assimp.NET to convert the DAE output, or integrate the Autodesk FBX SDK. However, DAE should be sufficient for Unity/Blender import.

---

## 6. Key Code References

### 6.1 Data Structures

| Class | File | Lines | Purpose |
|---|---|---|---|
| `RenderBase.OModelGroup` | `Ohana/RenderBase.cs` | 2241-2294 | Top-level container for all model data |
| `RenderBase.OModel` | `Ohana/RenderBase.cs` | 1490-1521 | Single model with mesh, material, skeleton |
| `RenderBase.OMesh` | `Ohana/RenderBase.cs` | 774-796 | Mesh with vertices and materialId |
| `RenderBase.OMaterial` | `Ohana/RenderBase.cs` | 1392-1445 | Material with texture name refs (name0/1/2) |
| `RenderBase.OTexture` | `Ohana/RenderBase.cs` | 1548-1564 | Texture bitmap + name |
| `RenderBase.OVertex` | `Ohana/RenderBase.cs` | 342-424 | Vertex with position, normal, UV, weights |
| `RenderBase.OBone` | `Ohana/RenderBase.cs` | 815-841 | Skeleton bone |

### 6.2 Loaders

| Loader | File | Key Method | Lines |
|---|---|---|---|
| File dispatcher | `Ohana/FileIO.cs` | `load(string)`, `load(Stream)` | 42-174 |
| BCH loader | `Ohana/Models/BCH.cs` | `load(MemoryStream)` | 162-1866 |
| BCH materials | `Ohana/Models/BCH.cs` | (material loop) | 1315-1516 |
| BCH mesh/material binding | `Ohana/Models/BCH.cs` | `objectEntry.materialId` | 1584, 1609 |
| BCH textures | `Ohana/Models/BCH.cs` | (texture loop) | 344-375 |
| GfModel loader | `Ohana/Models/PocketMonsters/GfModel.cs` | `load(Stream)` | 29-83 |
| GfModel materials | `Ohana/Models/PocketMonsters/GfModel.cs` | (material loop) | 176-238 |
| GfModel mesh binding | `Ohana/Models/PocketMonsters/GfModel.cs` | `matMeshBinding` | 172, 198, 260 |
| PC container | `Ohana/Models/PocketMonsters/PC.cs` | `load(Stream)` | 16-47 |
| GARC archive | `Ohana/Containers/GARC.cs` | `load(Stream)` | 23-100+ |
| PkmnContainer | `Ohana/Containers/PkmnContainer.cs` | `load(Stream)` | 24-59 |

### 6.3 Exporters

| Exporter | File | Key Method | Lines |
|---|---|---|---|
| DAE (Collada) | `Ohana/Models/GenericFormats/DAE.cs` | `export()` | 589-1007 |
| DAE material creation | `Ohana/Models/GenericFormats/DAE.cs` | (material loop) | 607-678 |
| DAE mesh-material bind | `Ohana/Models/GenericFormats/DAE.cs` | `geometry.mesh.triangles.material` | 833 |
| DAE texture images | `Ohana/Models/GenericFormats/DAE.cs` | (image loop) | 597-605 |
| OBJ | `Ohana/Models/GenericFormats/OBJ.cs` | `export()` | 18-55 |
| SMD | `Ohana/Models/GenericFormats/SMD.cs` | `export()` | 24+ |
| CMDL (XML) | `Ohana/Models/GenericFormats/CMDL.cs` | `export()` | 495-935 |

### 6.4 Texture Handling

| Component | File | Lines | Purpose |
|---|---|---|---|
| Texture decode | `Ohana/TextureCodec.cs` | - | Decodes PICA200 texture formats to Bitmap |
| Texture save | `GUI/Forms/OTextureExportForm.cs` | 57-69 | Saves Bitmap as PNG |
| GfTexture load | `Ohana/Textures/PocketMonsters/GfTexture.cs` | - | Sun/Moon texture format |

---

## 7. Risks and Open Questions

### 7.1 Risks

1. **.NET Framework 3.5 lock-in** -- The existing code targets .NET 3.5. A CLI could target the same, or be retargeted to .NET 4.x/Core. The core logic uses only basic BCL + System.Drawing, so retargeting should be straightforward.

2. **System.Drawing.Bitmap on non-Windows** -- If the CLI needs to run on Linux/macOS, `System.Drawing.Bitmap` requires the `System.Drawing.Common` package and `libgdiplus`. For Windows-only usage this is not an issue.

3. **No FBX export** -- Ohana3DS does not export FBX. If FBX is the required output, an additional conversion step or FBX SDK integration is needed. **DAE is the best available format** and imports well into Unity, Blender, and most 3D tools.

4. **Managed DirectX dependency** -- The `.csproj` references `Microsoft.DirectX.Direct3D`. The CLI project must NOT include these references. Only `RenderEngine.cs` uses DirectX; all other files are DirectX-free.

5. **GARC extraction needed** -- The Sun/Moon dump files are GARC archives, not raw model files. The CLI must handle GARC extraction as a preprocessing step.

6. **Empty files in dump** -- Many files in the dump are 0 bytes. The CLI must gracefully handle empty/corrupt files.

7. **File identification by index** -- Sun/Moon files have no extensions and are identified only by directory path (e.g., `a/0/3/5`). A Pokemon species mapping table may be needed to produce meaningful filenames.

### 7.2 Open Questions

1. **Which specific files in the dump correspond to which Pokemon?** The `a/0/x/y` numbering needs to be mapped to National Dex numbers. Community documentation (Project Pokemon, Kaphotics' resources) has these mappings.

2. **Do we need animation export?** The plan above covers static mesh/material/texture export. Skeletal animation export (SMD format) is available but adds complexity.

3. **DAE vs OBJ for Unity import?** DAE preserves materials, skeleton, and skinning. OBJ preserves geometry and material names but no skeleton. For Unity, DAE or direct FBX conversion is preferred.

4. **Is the DAE material SID bug the actual cause of broken materials?** This needs testing: export a multi-material model to DAE, check that all materials have distinct texture references, and verify import in the target tool (Blender/Unity).

5. **Pokemon Sun/Moon vs X/Y format differences** -- Sun/Moon uses GfModel format (magic `0x00010000`), while X/Y uses BCH. The dump appears to be Sun/Moon (GARC with GfModel). Confirm which generation the target models are from.

6. **Scale and coordinate system** -- 3DS models use a different coordinate system than Unity. The DAE exporter preserves the original coordinates. A post-processing scale/rotation step may be needed.

### 7.3 Implementation Priority

| Step | Task | Effort |
|---|---|---|
| 1 | Create CLI console project, include Ohana source files | Low |
| 2 | Remove WinForms/DirectX dependencies from included files | Low |
| 3 | Implement GARC extraction + recursive file processing | Medium |
| 4 | Implement DAE export with automatic texture extraction | Low |
| 5 | Fix DAE surface/sampler SID uniqueness bug | Low |
| 6 | Test with sample Sun/Moon GARC files | Medium |
| 7 | Create Pokemon name mapping (index -> species name) | Medium |
| 8 | Add OBJ MTL file generation (optional) | Low |
| 9 | Add FBX conversion via Assimp (optional) | High |
| 10 | Batch processing with output directory structure | Low |
