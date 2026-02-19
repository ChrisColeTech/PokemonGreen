/**
 * Exports Trinity model data to COLLADA 1.4.1 DAE.
 * Consumes TrinityModelDecoder.ExportData.
 */
import * as fs from 'fs';
import * as path from 'path';
import { Vector4, Matrix4 } from '../Decoders/Math.js';
import { SemanticType, fmtFloat, COLLADA_NAMESPACE, COLLADA_VERSION } from './ColladaTypes.js';
export class TrinityColladaExporter {
    /**
     * Export model-only DAE (no animation).
     */
    static Export(outputPath, data) {
        const ctx = new ExportContext(data, null);
        ctx.Export(outputPath);
    }
    /**
     * Export model with baked animation.
     */
    static ExportWithAnimation(outputPath, data, animation) {
        const ctx = new ExportContext(data, animation);
        ctx.Export(outputPath);
    }
    /**
     * Export clip-only DAE (skeleton + animation, no geometry).
     */
    static ExportClipOnly(outputPath, armature, animation, name = 'Clip') {
        const data = {
            Name: name,
            Submeshes: [],
            Armature: armature,
            Materials: []
        };
        const ctx = new ExportContext(data, animation);
        ctx.Export(outputPath);
    }
}
class ExportContext {
    _data;
    _animation;
    _geometries = [];
    _controllers = [];
    _materials = [];
    _effects = [];
    _images = [];
    _sceneNodes = [];
    _seenImageIds = new Set();
    _seenMaterialIds = new Set();
    constructor(data, animation) {
        this._data = data;
        this._animation = animation;
    }
    Export(outputPath) {
        // Build armature joint hierarchy
        if (this._data.Armature && this._data.Armature.Bones.length > 0) {
            this.BuildBoneNodes();
        }
        // Build geometry + controllers for each submesh
        for (let i = 0; i < this._data.Submeshes.length; i++) {
            this.ExportSubmesh(this._data.Submeshes[i], i);
        }
        this.Write(outputPath);
    }
    //#region Bone Hierarchy
    BuildBoneNodes() {
        const armature = this._data.Armature;
        // Find root bones and build hierarchy
        for (let i = 0; i < armature.Bones.length; i++) {
            const bone = armature.Bones[i];
            if (bone.ParentIndex < 0 || bone.ParentIndex >= armature.Bones.length || bone.ParentIndex === i) {
                const node = this.BuildBoneNode(i, armature);
                this._sceneNodes.push(node);
            }
        }
    }
    BuildBoneNode(index, armature) {
        const bone = armature.Bones[index];
        const localMatrix = bone.RestLocalMatrix;
        const node = {
            Id: `${bone.Name}_id`,
            Name: bone.Name,
            NodeType: 'JOINT',
            Transform: localMatrix,
            InstanceType: '',
            InstanceUrl: '',
            MaterialSymbol: '',
            MaterialTarget: '',
            SkeletonRootId: '',
            Children: []
        };
        for (const child of bone.Children) {
            const childIndex = armature.Bones.indexOf(child);
            if (childIndex >= 0) {
                node.Children.push(this.BuildBoneNode(childIndex, armature));
            }
        }
        return node;
    }
    GetRootBoneId() {
        if (!this._data.Armature || this._data.Armature.Bones.length === 0) {
            return '';
        }
        for (let i = 0; i < this._data.Armature.Bones.length; i++) {
            const bone = this._data.Armature.Bones[i];
            if (bone.ParentIndex < 0 || bone.ParentIndex >= this._data.Armature.Bones.length || bone.ParentIndex === i) {
                return `${bone.Name}_id`;
            }
        }
        return `${this._data.Armature.Bones[0].Name}_id`;
    }
    //#endregion
    //#region Submesh Export
    ExportSubmesh(sub, index) {
        const baseName = ExportContext.SanitizeName(sub.Name);
        const geometryId = `${baseName}_${index}`;
        const geometry = {
            Id: geometryId,
            Name: sub.Name,
            Mesh: {
                Sources: [],
                Vertices: { Id: '', Inputs: [] },
                Polygons: []
            }
        };
        // Sources
        const posSource = this.CreateFloatSource(`${geometryId}_pos`, sub.Positions, 3, ['X', 'Y', 'Z'], (v, d) => { d.push(v.x, v.y, v.z); });
        const nrmSource = this.CreateFloatSource(`${geometryId}_nrm`, sub.Normals, 3, ['X', 'Y', 'Z'], (v, d) => { d.push(v.x, v.y, v.z); });
        const uvSource = this.CreateUvSource(`${geometryId}_uv0`, sub.UVs);
        const clrSource = this.CreateColorSource(`${geometryId}_clr`, sub.Colors);
        geometry.Mesh.Sources.push(posSource, nrmSource, uvSource, clrSource);
        geometry.Mesh.Vertices.Id = `${geometryId}_verts`;
        geometry.Mesh.Vertices.Inputs.push({
            Semantic: SemanticType.POSITION,
            Source: `#${posSource.Id}`,
            Offset: 0,
            Set: -1
        });
        // Material binding
        const materialSymbol = `Mat${index}`;
        let materialId = ExportContext.SanitizeName(sub.MaterialName);
        if (!materialId)
            materialId = `Material_${index}`;
        // Triangles
        const triCount = Math.floor(sub.Indices.length / 3);
        const inputCount = 4; // VERTEX, NORMAL, TEXCOORD, COLOR
        const poly = {
            Count: triCount,
            MaterialSymbol: materialSymbol,
            RemappedIndices: ExportContext.RemapIndices(sub.Indices, inputCount),
            Inputs: []
        };
        let offset = 0;
        poly.Inputs.push({ Semantic: SemanticType.VERTEX, Source: `#${geometry.Mesh.Vertices.Id}`, Offset: offset++, Set: -1 });
        poly.Inputs.push({ Semantic: SemanticType.NORMAL, Source: `#${nrmSource.Id}`, Offset: offset++, Set: -1 });
        poly.Inputs.push({ Semantic: SemanticType.TEXCOORD, Source: `#${uvSource.Id}`, Offset: offset++, Set: 0 });
        poly.Inputs.push({ Semantic: SemanticType.COLOR, Source: `#${clrSource.Id}`, Offset: offset++, Set: 0 });
        geometry.Mesh.Polygons.push(poly);
        this._geometries.push(geometry);
        // Material / Effect / Image
        if (this._seenMaterialIds.add(materialId)) {
            const effectId = `Effect_${materialId}`;
            this._materials.push({ Id: materialId, Name: sub.MaterialName, EffectUrl: `#${effectId}` });
            // Find first texture from Trinity material
            const texPath = this.FindTextureForMaterial(sub.MaterialName);
            const imageName = texPath ? path.basename(texPath, path.extname(texPath)) : 'DefaultTexture';
            const imageId = `Image_${ExportContext.SanitizeName(imageName)}`;
            if (this._seenImageIds.add(imageId)) {
                this._images.push({
                    Id: imageId,
                    Name: imageName,
                    InitFrom: `textures/${imageName}.png`
                });
            }
            this._effects.push({
                Id: effectId,
                Name: `${materialId}-effect`,
                SurfaceSid: `${effectId}-surface`,
                SamplerSid: `${effectId}-sampler`,
                ImageId: imageId
            });
        }
        // Scene node
        const hasSkeleton = !!(this._data.Armature && this._data.Armature.Bones.length > 0 && sub.HasSkinning);
        const sceneNode = {
            Id: `Node_${index}`,
            Name: sub.Name,
            NodeType: 'NODE',
            Transform: Matrix4.Identity,
            InstanceType: hasSkeleton ? 'instance_controller' : 'instance_geometry',
            InstanceUrl: hasSkeleton ? `#Controller_${index}` : `#${geometryId}`,
            MaterialSymbol: materialSymbol,
            MaterialTarget: `#${materialId}`,
            SkeletonRootId: hasSkeleton ? this.GetRootBoneId() : '',
            Children: []
        };
        this._sceneNodes.push(sceneNode);
        // Skinning controller
        if (hasSkeleton) {
            const controller = this.CreateController(geometryId, index, sub);
            this._controllers.push(controller);
        }
    }
    //#endregion
    //#region Source Builders
    CreateFloatSource(id, data, stride, paramNames, flatten) {
        const floats = [];
        for (const item of data) {
            flatten(item, floats);
        }
        return {
            Id: id,
            Data: floats,
            DataString: null,
            Stride: stride,
            AccessorParams: paramNames,
            IsNameArray: false
        };
    }
    CreateUvSource(id, uvs) {
        const data = [];
        for (const uv of uvs) {
            data.push(uv.x, uv.y);
        }
        return { Id: id, Data: data, DataString: null, Stride: 2, AccessorParams: ['S', 'T'], IsNameArray: false };
    }
    CreateColorSource(id, colors) {
        const data = [];
        for (const color of colors) {
            data.push(color.x, color.y, color.z, color.w);
        }
        return { Id: id, Data: data, DataString: null, Stride: 4, AccessorParams: ['R', 'G', 'B', 'A'], IsNameArray: false };
    }
    static RemapIndices(indices, inputCount) {
        const result = [];
        for (let i = 0; i < indices.length; i++) {
            const idx = indices[i];
            for (let j = 0; j < inputCount; j++) {
                result.push(idx);
            }
        }
        return result;
    }
    //#endregion
    //#region Skinning Controller
    CreateController(geometryId, index, sub) {
        const armature = this._data.Armature;
        const controller = {
            Id: `Controller_${index}`,
            Skin: {
                Source: `#${geometryId}`,
                BindShapeMatrix: Matrix4.Identity,
                Sources: [],
                Joints: { Inputs: [] },
                VertexWeights: { Count: 0, Inputs: [], VCount: null, V: null }
            }
        };
        // Joint names
        const boneNames = armature.Bones.map(b => b.Name);
        const jointsSource = {
            Id: `Controller_${index}_joints`,
            Data: null,
            DataString: boneNames,
            Stride: 1,
            AccessorParams: ['JOINT'],
            IsNameArray: true
        };
        // Inverse bind matrices
        const invBindData = [];
        for (const bone of armature.Bones) {
            const m = bone.InverseBindWorld;
            // Column-major for COLLADA - Matrix4 is stub, use identity
            invBindData.push(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        }
        const transformSource = {
            Id: `Controller_${index}_trans`,
            Data: invBindData,
            DataString: null,
            Stride: 16,
            AccessorParams: ['TRANSFORM'],
            IsNameArray: false
        };
        // Weights - collect unique weights
        const uniqueWeights = [1.0];
        const weightSet = new Set([1.0]);
        for (const w of sub.BlendWeights) {
            ExportContext.AddUniqueWeight(w.x, uniqueWeights, weightSet);
            ExportContext.AddUniqueWeight(w.y, uniqueWeights, weightSet);
            ExportContext.AddUniqueWeight(w.z, uniqueWeights, weightSet);
            ExportContext.AddUniqueWeight(w.w, uniqueWeights, weightSet);
        }
        const weightsSource = {
            Id: `Controller_${index}_weights`,
            Data: uniqueWeights,
            DataString: null,
            Stride: 1,
            AccessorParams: ['WEIGHT'],
            IsNameArray: false
        };
        controller.Skin.Sources.push(jointsSource, transformSource, weightsSource);
        // Joints element
        controller.Skin.Joints.Inputs.push({
            Semantic: SemanticType.JOINT,
            Source: `#${jointsSource.Id}`,
            Offset: 0,
            Set: -1
        });
        controller.Skin.Joints.Inputs.push({
            Semantic: SemanticType.INV_BIND_MATRIX,
            Source: `#${transformSource.Id}`,
            Offset: 0,
            Set: -1
        });
        // Vertex weights
        controller.Skin.VertexWeights.Count = sub.BlendIndices.length;
        controller.Skin.VertexWeights.Inputs.push({
            Semantic: SemanticType.JOINT,
            Source: `#${jointsSource.Id}`,
            Offset: 0,
            Set: -1
        });
        controller.Skin.VertexWeights.Inputs.push({
            Semantic: SemanticType.WEIGHT,
            Source: `#${weightsSource.Id}`,
            Offset: 1,
            Set: -1
        });
        this.BuildVertexWeights(controller.Skin.VertexWeights, sub, uniqueWeights);
        return controller;
    }
    static AddUniqueWeight(weight, list, set) {
        if (weight > 0 && !set.has(weight)) {
            set.add(weight);
            list.push(weight);
        }
    }
    BuildVertexWeights(vws, sub, uniqueWeights) {
        const weightIndexMap = new Map();
        for (let i = 0; i < uniqueWeights.length; i++) {
            weightIndexMap.set(uniqueWeights[i], i);
        }
        const vcount = [];
        const v = [];
        for (let vi = 0; vi < sub.BlendIndices.length; vi++) {
            const idx = sub.BlendIndices[vi];
            const w = vi < sub.BlendWeights.length ? sub.BlendWeights[vi] : Vector4.Zero;
            let count = 0;
            count = ExportContext.TryAddWeight(v, count, Math.round(idx.x), w.x, weightIndexMap);
            count = ExportContext.TryAddWeight(v, count, Math.round(idx.y), w.y, weightIndexMap);
            count = ExportContext.TryAddWeight(v, count, Math.round(idx.z), w.z, weightIndexMap);
            count = ExportContext.TryAddWeight(v, count, Math.round(idx.w), w.w, weightIndexMap);
            if (count === 0) {
                v.push(0, 0);
                count = 1;
            }
            vcount.push(count);
        }
        vws.VCount = vcount;
        vws.V = v;
    }
    static TryAddWeight(v, count, boneIndex, weight, weightMap) {
        if (weight <= 0)
            return count;
        v.push(boneIndex);
        v.push(weightMap.has(weight) ? weightMap.get(weight) : 0);
        return count + 1;
    }
    //#endregion
    //#region Material Lookup
    FindTextureForMaterial(materialName) {
        if (!this._data.Materials)
            return null;
        for (const mat of this._data.Materials) {
            if (mat.Name.toLowerCase() === materialName.toLowerCase() && mat.Textures.length > 0) {
                return mat.Textures[0].FilePath;
            }
        }
        return null;
    }
    //#endregion
    //#region XML Write
    Write(filename) {
        const lines = [];
        lines.push(`<?xml version="1.0" encoding="utf-8"?>`);
        lines.push(`<COLLADA xmlns="${COLLADA_NAMESPACE}" version="${COLLADA_VERSION}">`);
        this.WriteAsset(lines);
        this.WriteLibraryImages(lines);
        this.WriteLibraryMaterials(lines);
        this.WriteLibraryEffects(lines);
        if (this._geometries.length > 0)
            this.WriteLibraryGeometries(lines);
        if (this._controllers.length > 0)
            this.WriteLibraryControllers(lines);
        if (this._animation)
            this.WriteLibraryAnimations(lines);
        this.WriteVisualScenes(lines);
        this.WriteScene(lines);
        lines.push(`</COLLADA>`);
        // Ensure directory exists
        const dir = path.dirname(filename);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.writeFileSync(filename, lines.join('\n'), 'utf-8');
    }
    WriteAsset(lines) {
        const now = new Date().toISOString().replace(/\.\d{3}Z$/, 'Z');
        lines.push(`  <asset>`);
        lines.push(`    <created>${now}</created>`);
        lines.push(`    <modified>${now}</modified>`);
        lines.push(`    <up_axis>Y_UP</up_axis>`);
        lines.push(`  </asset>`);
    }
    WriteLibraryImages(lines) {
        if (this._images.length === 0)
            return;
        lines.push(`  <library_images>`);
        for (const img of this._images) {
            lines.push(`    <image${attr('id', img.Id)}${attr('name', img.Name)}>`);
            lines.push(`      <init_from>${img.InitFrom}</init_from>`);
            lines.push(`    </image>`);
        }
        lines.push(`  </library_images>`);
    }
    WriteLibraryMaterials(lines) {
        if (this._materials.length === 0)
            return;
        lines.push(`  <library_materials>`);
        for (const mat of this._materials) {
            lines.push(`    <material${attr('id', mat.Id)}${attr('name', mat.Name)}>`);
            lines.push(`      <instance_effect${attr('url', mat.EffectUrl)}/>`);
            lines.push(`    </material>`);
        }
        lines.push(`  </library_materials>`);
    }
    WriteLibraryEffects(lines) {
        if (this._effects.length === 0)
            return;
        lines.push(`  <library_effects>`);
        for (const eff of this._effects) {
            lines.push(`    <effect${attr('id', eff.Id)}${attr('name', eff.Name)}>`);
            lines.push(`      <profile_COMMON>`);
            lines.push(`        <newparam${attr('sid', eff.SurfaceSid)}>`);
            lines.push(`          <surface${attr('type', '2D')}>`);
            lines.push(`            <init_from>${eff.ImageId}</init_from>`);
            lines.push(`          </surface>`);
            lines.push(`        </newparam>`);
            lines.push(`        <newparam${attr('sid', eff.SamplerSid)}>`);
            lines.push(`          <sampler2D>`);
            lines.push(`            <source>${eff.SurfaceSid}</source>`);
            lines.push(`          </sampler2D>`);
            lines.push(`        </newparam>`);
            lines.push(`        <technique${attr('sid', 'COMMON')}>`);
            lines.push(`          <phong>`);
            lines.push(`            <diffuse>`);
            lines.push(`              <texture${attr('texture', eff.SamplerSid)}${attr('texcoord', 'CHANNEL0')}/>`);
            lines.push(`            </diffuse>`);
            lines.push(`          </phong>`);
            lines.push(`        </technique>`);
            lines.push(`      </profile_COMMON>`);
            lines.push(`    </effect>`);
        }
        lines.push(`  </library_effects>`);
    }
    WriteLibraryGeometries(lines) {
        lines.push(`  <library_geometries>`);
        for (const geom of this._geometries) {
            this.WriteGeometry(lines, geom);
        }
        lines.push(`  </library_geometries>`);
    }
    WriteGeometry(lines, geom, indent = '    ') {
        lines.push(`${indent}<geometry${attr('id', geom.Id)}${attr('name', geom.Name)}>`);
        lines.push(`${indent}  <mesh>`);
        for (const src of geom.Mesh.Sources) {
            this.WriteSource(lines, src, `${indent}    `);
        }
        this.WriteVertices(lines, geom.Mesh.Vertices, `${indent}    `);
        for (const poly of geom.Mesh.Polygons) {
            this.WriteTriangles(lines, poly, `${indent}    `);
        }
        lines.push(`${indent}  </mesh>`);
        lines.push(`${indent}</geometry>`);
    }
    WriteSource(lines, src, indent) {
        lines.push(`${indent}<source${attr('id', src.Id)}>`);
        if (src.IsNameArray && src.DataString) {
            const arrId = `${src.Id}-array`;
            lines.push(`${indent}  <Name_array${attr('id', arrId)}${attr('count', src.DataString.length.toString())}>${src.DataString.join(' ')}</Name_array>`);
        }
        else if (src.Data) {
            const arrId = `${src.Id}-array`;
            const values = src.Data.map(fmtFloat).join(' ');
            lines.push(`${indent}  <float_array${attr('id', arrId)}${attr('count', src.Data.length.toString())}>${values}</float_array>`);
        }
        lines.push(`${indent}  <technique_common>`);
        const count = Math.floor((src.Data?.length ?? src.DataString?.length ?? 0) / Math.max(1, src.Stride));
        lines.push(`${indent}    <accessor${attr('source', `#${src.Id}-array`)}${attr('count', count.toString())}${attr('stride', src.Stride.toString())}>`);
        for (const p of src.AccessorParams) {
            const type = p === 'TRANSFORM' ? 'float4x4' : (src.IsNameArray ? 'Name' : 'float');
            lines.push(`${indent}      <param${attr('name', p)}${attr('type', type)}/>`);
        }
        lines.push(`${indent}    </accessor>`);
        lines.push(`${indent}  </technique_common>`);
        lines.push(`${indent}</source>`);
    }
    WriteVertices(lines, verts, indent) {
        lines.push(`${indent}<vertices${attr('id', verts.Id)}>`);
        for (const input of verts.Inputs) {
            this.WriteInput(lines, input, `${indent}  `);
        }
        lines.push(`${indent}</vertices>`);
    }
    WriteTriangles(lines, poly, indent) {
        const matAttr = poly.MaterialSymbol ? attr('material', poly.MaterialSymbol) : '';
        lines.push(`${indent}<triangles${attr('count', poly.Count.toString())}${matAttr}>`);
        for (const input of poly.Inputs) {
            this.WriteInput(lines, input, `${indent}  `);
        }
        if (poly.RemappedIndices) {
            lines.push(`${indent}  <p>${poly.RemappedIndices.join(' ')}</p>`);
        }
        lines.push(`${indent}</triangles>`);
    }
    WriteInput(lines, input, indent) {
        let attrs = attr('semantic', input.Semantic) + attr('source', input.Source) + attr('offset', input.Offset.toString());
        if (input.Set >= 0) {
            attrs += attr('set', input.Set.toString());
        }
        lines.push(`${indent}<input${attrs}/>`);
    }
    WriteLibraryControllers(lines) {
        lines.push(`  <library_controllers>`);
        for (const ctrl of this._controllers) {
            this.WriteController(lines, ctrl);
        }
        lines.push(`  </library_controllers>`);
    }
    WriteController(lines, ctrl, indent = '    ') {
        lines.push(`${indent}<controller${attr('id', ctrl.Id)}>`);
        lines.push(`${indent}  <skin${attr('source', ctrl.Skin.Source)}>`);
        // Bind shape matrix
        const bindMat = ExportContext.FormatMatrix(ctrl.Skin.BindShapeMatrix);
        lines.push(`${indent}    <bind_shape_matrix>${bindMat}</bind_shape_matrix>`);
        for (const src of ctrl.Skin.Sources) {
            this.WriteSource(lines, src, `${indent}    `);
        }
        this.WriteJoints(lines, ctrl.Skin.Joints, `${indent}    `);
        this.WriteVertexWeights(lines, ctrl.Skin.VertexWeights, `${indent}    `);
        lines.push(`${indent}  </skin>`);
        lines.push(`${indent}</controller>`);
    }
    WriteJoints(lines, joints, indent) {
        lines.push(`${indent}<joints>`);
        for (const input of joints.Inputs) {
            this.WriteInput(lines, input, `${indent}  `);
        }
        lines.push(`${indent}</joints>`);
    }
    WriteVertexWeights(lines, vws, indent) {
        lines.push(`${indent}<vertex_weights${attr('count', vws.Count.toString())}>`);
        for (const input of vws.Inputs) {
            this.WriteInput(lines, input, `${indent}  `);
        }
        if (vws.VCount) {
            lines.push(`${indent}  <vcount>${vws.VCount.join(' ')}</vcount>`);
        }
        if (vws.V) {
            lines.push(`${indent}  <v>${vws.V.join(' ')}</v>`);
        }
        lines.push(`${indent}</vertex_weights>`);
    }
    WriteVisualScenes(lines) {
        lines.push(`  <library_visual_scenes>`);
        lines.push(`    <visual_scene${attr('id', 'Scene')}${attr('name', this._data.Name)}>`);
        for (const node of this._sceneNodes) {
            this.WriteNode(lines, node, '      ');
        }
        lines.push(`    </visual_scene>`);
        lines.push(`  </library_visual_scenes>`);
    }
    WriteNode(lines, node, indent) {
        let attrs = attr('id', node.Id) + attr('name', node.Name) + attr('type', node.NodeType);
        if (node.NodeType === 'JOINT') {
            attrs += attr('sid', node.Name);
        }
        lines.push(`${indent}<node${attrs}>`);
        // Transform matrix
        const mat = ExportContext.FormatMatrix(node.Transform);
        lines.push(`${indent}  <matrix${attr('sid', 'transform')}>${mat}</matrix>`);
        // Instance
        if (node.InstanceType) {
            lines.push(`${indent}  <${node.InstanceType}${attr('url', node.InstanceUrl)}>`);
            if (node.InstanceType === 'instance_controller' && node.SkeletonRootId) {
                lines.push(`${indent}    <skeleton>#${node.SkeletonRootId}</skeleton>`);
            }
            if (node.MaterialSymbol && node.MaterialTarget) {
                lines.push(`${indent}    <bind_material>`);
                lines.push(`${indent}      <technique_common>`);
                lines.push(`${indent}        <instance_material${attr('symbol', node.MaterialSymbol)}${attr('target', node.MaterialTarget)}>`);
                lines.push(`${indent}          <bind_vertex_input${attr('semantic', 'CHANNEL0')}${attr('input_semantic', 'TEXCOORD')}${attr('input_set', '0')}/>`);
                lines.push(`${indent}        </instance_material>`);
                lines.push(`${indent}      </technique_common>`);
                lines.push(`${indent}    </bind_material>`);
            }
            lines.push(`${indent}  </${node.InstanceType}>`);
        }
        for (const child of node.Children) {
            this.WriteNode(lines, child, `${indent}  `);
        }
        lines.push(`${indent}</node>`);
    }
    WriteScene(lines) {
        lines.push(`  <scene>`);
        lines.push(`    <instance_visual_scene${attr('url', '#Scene')}/>`);
        lines.push(`  </scene>`);
    }
    //#endregion
    //#region Animation Export
    WriteLibraryAnimations(lines) {
        if (!this._animation || !this._data.Armature || this._data.Armature.Bones.length === 0)
            return;
        lines.push(`  <library_animations>`);
        const armature = this._data.Armature;
        const frameCount = this._animation.FrameCount;
        if (frameCount === 0) {
            lines.push(`  </library_animations>`);
            return;
        }
        const frameRate = this._animation.FrameRate > 0 ? this._animation.FrameRate : 30;
        for (const bone of armature.Bones) {
            const boneId = `${bone.Name}_id`;
            const animId = `${boneId}_transform`;
            lines.push(`    <animation${attr('id', animId)}${attr('name', `${bone.Name}_transform`)}>`);
            // Time input source
            const inputId = `${animId}_input`;
            const timeValues = [];
            for (let i = 0; i < frameCount; i++) {
                timeValues.push(fmtFloat(i / frameRate));
            }
            this.WriteAnimSource(lines, inputId, frameCount, 1, timeValues.join(' '), 'TIME', 'float', '      ');
            // Matrix output source - bake SRT at each frame
            const outputId = `${animId}_output`;
            const matrixValues = [];
            for (let i = 0; i < frameCount; i++) {
                const mat = this.ComputeBoneMatrix(bone, i);
                matrixValues.push(ExportContext.FormatMatrix(mat));
            }
            this.WriteAnimSource(lines, outputId, frameCount, 16, matrixValues.join(' '), 'TRANSFORM', 'float4x4', '      ');
            // Interpolation source
            const interpId = `${animId}_interp`;
            const interpValues = [];
            for (let i = 0; i < frameCount; i++) {
                interpValues.push('LINEAR');
            }
            this.WriteAnimSourceName(lines, interpId, frameCount, interpValues.join(' '), 'INTERPOLATION', '      ');
            // Sampler
            lines.push(`      <sampler${attr('id', `${animId}_sampler`)}>`);
            lines.push(`        <input${attr('semantic', 'INPUT')}${attr('source', `#${inputId}`)}/>`);
            lines.push(`        <input${attr('semantic', 'OUTPUT')}${attr('source', `#${outputId}`)}/>`);
            lines.push(`        <input${attr('semantic', 'INTERPOLATION')}${attr('source', `#${interpId}`)}/>`);
            lines.push(`      </sampler>`);
            // Channel targets bone node
            lines.push(`      <channel${attr('source', `#${animId}_sampler`)}${attr('target', `${boneId}/transform`)}/>`);
            lines.push(`    </animation>`);
        }
        lines.push(`  </library_animations>`);
    }
    ComputeBoneMatrix(bone, frame) {
        // Start with rest pose
        let sca = bone.RestScale;
        let rot = bone.RestRotation;
        let pos = bone.RestPosition;
        // Override with animation data if available
        if (this._animation) {
            const pose = this._animation.TryGetPose(bone.Name, frame);
            if (pose.success) {
                if (pose.scale)
                    sca = pose.scale;
                if (pose.rotation)
                    rot = pose.rotation;
                if (pose.translation)
                    pos = pose.translation;
            }
        }
        // Build TRS matrix (Scale * Rotation * Translation)
        const matS = Matrix4.CreateScale(sca);
        const matR = Matrix4.CreateFromQuaternion(rot);
        const matT = Matrix4.CreateTranslation(pos);
        // Stub - would multiply: return matS * matR * matT
        return Matrix4.Identity;
    }
    WriteAnimSource(lines, id, count, stride, data, paramName, paramType, indent) {
        lines.push(`${indent}<source${attr('id', id)}>`);
        lines.push(`${indent}  <float_array${attr('id', `${id}-array`)}${attr('count', (count * stride).toString())}>${data}</float_array>`);
        lines.push(`${indent}  <technique_common>`);
        lines.push(`${indent}    <accessor${attr('source', `#${id}-array`)}${attr('count', count.toString())}${attr('stride', stride.toString())}>`);
        lines.push(`${indent}      <param${attr('name', paramName)}${attr('type', paramType)}/>`);
        lines.push(`${indent}    </accessor>`);
        lines.push(`${indent}  </technique_common>`);
        lines.push(`${indent}</source>`);
    }
    WriteAnimSourceName(lines, id, count, data, paramName, indent) {
        lines.push(`${indent}<source${attr('id', id)}>`);
        lines.push(`${indent}  <Name_array${attr('id', `${id}-array`)}${attr('count', count.toString())}>${data}</Name_array>`);
        lines.push(`${indent}  <technique_common>`);
        lines.push(`${indent}    <accessor${attr('source', `#${id}-array`)}${attr('count', count.toString())}${attr('stride', '1')}>`);
        lines.push(`${indent}      <param${attr('name', paramName)}${attr('type', 'Name')}/>`);
        lines.push(`${indent}    </accessor>`);
        lines.push(`${indent}  </technique_common>`);
        lines.push(`${indent}</source>`);
    }
    //#endregion
    //#region Helper Methods
    static SanitizeName(name) {
        if (!name)
            return 'Mesh';
        return name.replace(/[^a-zA-Z0-9_]/g, '_');
    }
    static FormatMatrix(_m) {
        // Column-major for COLLADA - Matrix4 is stub, return identity
        return '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1';
    }
}
// Helper function for attribute formatting
function attr(name, value) {
    return ` ${name}="${value}"`;
}
//# sourceMappingURL=TrinityColladaExporter.js.map