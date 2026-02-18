# DAE Exporter Animation Debugging & Fixes Handoff

**Author:** Antigravity  
**Date:** 2026-02-18  
**Status:** In Progress (Diagnosis Complete, Fixes Pending)

## 1. Accomplishments

We have successfully isolated the root causes preventing `drp-to-dae` exported animations from playing in Blender. After initial fixes to bone naming (`_id` suffix removal) and matrix SIDs (`sid="transform"`) failed to yield results, we built a comprehensive diagnostics suite that revealed two fundamental structural flaws in the generated COLLADA files.

### Key Diagnostics Built
- **`blender_import_test.py`**: A robust, interactive Python script for Blender that automates importing model + animation DAE pairs and reports object/armature creation.
- **`blender_diagnostic.py`**: A deep-dive introspection tool that analyzes the internal Blender data structures after import. It prints exact bone hierarchies, fcurve data paths, and action targets.
- **`verify_dae.ps1` & `verify_sid.ps1`**: PowerShell scripts for rapid static analysis of generated XML without opening Blender.

### Critical Findings (The "Why")
The `blender_diagnostic.py` output provided the smoking gun. We discovered two major structural bugs:

1.  **Flat Animation Skeleton (The "115 Armatures" Bug):**
    - **Symptom:** Blender created 115 separate `Armature` objects (e.g., `Armature.001`, `Armature.002`...) instead of one single skeleton.
    - **Cause:** The animation DAE's `library_visual_scenes` was exporting bone nodes as a **flat list of siblings** rather than a **nested hierarchy**.
    - **Result:** Blender treated each bone as a separate root object. Since the animation channels targeted specific bone names, but the bones were scattered across 115 different objects, no single armature received the full animation. 1840 curves were discarded on import.

2.  **Unresolved Skeleton Root (The "Hardcoded ID" Bug):**
    - **Symptom:** The model DAE skin controller reported `Could not resolve sid "BASE"` for *every single bone*.
    - **Cause:** The `<skeleton>` tag in `ColladaExporter.cs` (line 1153) was hardcoded to reference `#Bone_0_id`. However, the actual root bone node in our generated DAE uses the ID `BASE_id`.
    - **Result:** Blender could not find the skeleton root matching `#Bone_0_id`. Consequently, it couldn't bind the mesh to the armature, and the skinning instructions became orphaned.

## 2. Work Remaining

The immediate next steps are to implement the targeted structural fixes. We are no longer guessing; we have specific lines of code to change.

1.  **Fix Structural Hierarchy:**
    - Rewrite `AnimationExporter.cs`'s `CreateLibraryVisualScenes`.
    - Instead of iterating through `skeleton.Bones` (linear list), we must find the root bone(s) and recursively generate `<node>` elements to mirror the parent-child hierarchy properly.

2.  **Fix ID Reference:**
    - Update `ColladaExporter.cs` line 1153.
    - Change the hardcoded `#Bone_0_id` to dynamically use the ID of the root bone (e.g., `#BASE_id` or whatever the first bone's ID is).

3.  **Verification:**
    - Run `drp-to-dae` to re-export `a038`.
    - Run `blender_diagnostic.py` again. We expect to see **one** Armature object with 115 bones, and a matching Action with 1840 active fcurves.
    - Visual confirmation in Blender that the mesh moves.

## 3. Optimizations (Prime Suspects)

Once functionality is restored, the following optimizations should be prioritized:

1.  **Parallel Processing for Animation Clips:**
    - Validated in `OhanaCli` (via `Task.WhenAll`), we should implement parallel processing for exporting animation clips in `drp-to-dae`. Currently, it processes sequentially.
    - **Target:** `BatchExport` in `Program.cs`.

2.  **String Allocation within recursive calls:**
    - `AnimationExporter` currently does a lot of string interpolation (`$"{boneId}/transform"`) inside tight loops. Pre-calculating IDs or using `StringBuilder` for the massive DAE XML generation will reduce GC pressure.

3.  **Texture Conversion Caching:**
    - If running multiple times, `TextureExporter` re-decodes textures every run. Implementing a hash-based check to skip already-exported PNGs would speed up iteration times significantly.

## 4. Step-by-Step Recovery Plan

To get the app fully working with no errors:

1.  **Modify `DrpToDae/Formats/Collada/ColladaExporter.cs`:**
    - Locate `var skel = doc.CreateElement("skeleton");` around line 1153.
    - Change `skel.InnerText = "#Bone_0_id";` to `skel.InnerText = "#" + rootNodeId;` (ensuring `rootNodeId` is passed down or retrieved from the constructed tree).

2.  **Modify `DrpToDae/Formats/Animation/AnimationExporter.cs`:**
    - Locate `CreateLibraryVisualScenes`.
    - Replace the `foreach (var bone in skeleton.Bones)` loop.
    - Implement a `AddNodeRecursive(XElement parentXml, Bone currentBone)` function.
    - Call this on the root bone to build the proper nested XML structure.

3.  **Build & Run:**
    - `dotnet build`
    - `drp-to-dae.exe "D:\Projects\PokemonGreen\romfs\a\0\3\8" -o "D:\Projects\PokemonGreen\tools\drp-to-dae\test-output" --max 1 --types both`

4.  **Test in Blender:**
    - Open Blender 4.0+.
    - Switch to Scripting tab.
    - Open `D:\Projects\PokemonGreen\tools\drp-to-dae\blender_diagnostic.py`.
    - Run Script.
    - **Success Criteria:** Console output shows `New armatures: ['Armature']` (Count: 1), and `New actions: ['a038hi_attack_anim']` with valid fcurves.

## 5. How to Start/Test the API

The `drp-to-dae` tool is a CLI application.

**Basic Usage:**
```powershell
# Export single character (model + animations)
./drp-to-dae.exe "path/to/drp/folder" -o "output/folder" --max 1 --types both
```

**Verification:**
Use the provided PowerShell scripts in `D:\Projects\PokemonGreen\tools\drp-to-dae\`:
- `verify_sid.ps1`: Checks if generated DAEs have the correct `sid="transform"` on matrices.
- `verify_dae.ps1`: Checks if animation channel targets match bone names.

## 6. Solving "All Errors" Strategies

1.  **Divide and Conquer (The Diagnostic Script Approach):** 
    - We stopped guessing and wrote code to *ask Blender* what it saw. This was the turning point. Future 3D issues should always start with a Python introspection script in the target application (Blender/Maya/Unity).

2.  **Reference Implementation Analysis:**
    - Comparing our DAE structure against `Spica` and `Ohana` codebases (located in `src/PokemonGreen.Spica` and `src/PokemonGreen.OhanaCli`) proved vital. We confirmed that `_id` suffix removal was correct but insufficient on its own. Always cross-reference with known-good parsers.

3.  **Schema Validation:**
    - The DAE logs showed schema warnings (`ERROR_UNKNOWN_ATTRIBUTE`). While often benign, cleaning these up (e.g., removing invalid attributes on `<input>` tags) ensures we aren't triggering fallback/legacy behaviors in importers.

## 7. New Architecture / Features

- **Diagnostic Python Suite:** We now have a reusable harness for testing DAE imports in Blender programmatically. This can be expanded to part of a CI/CD pipeline for art assets.
- **Robust Hash Resolution:** We fixed the bone name resolution logic to correctly handle signed/unsigned integer casting for hashes, ensuring proper bone naming from `vbn` files.
