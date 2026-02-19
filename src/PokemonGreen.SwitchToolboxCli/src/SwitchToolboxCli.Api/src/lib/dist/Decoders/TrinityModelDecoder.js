import { PathString } from '../Utils/PathString.js';
var BlendIndexRemapMode;
(function (BlendIndexRemapMode) {
    BlendIndexRemapMode[BlendIndexRemapMode["None"] = 0] = "None";
    BlendIndexRemapMode[BlendIndexRemapMode["BoneWeights"] = 1] = "BoneWeights";
    BlendIndexRemapMode[BlendIndexRemapMode["JointInfo"] = 2] = "JointInfo";
    BlendIndexRemapMode[BlendIndexRemapMode["SkinningPalette"] = 3] = "SkinningPalette";
    BlendIndexRemapMode[BlendIndexRemapMode["BoneMeta"] = 4] = "BoneMeta";
})(BlendIndexRemapMode || (BlendIndexRemapMode = {}));
/**
 * Headless Trinity model decoder. Parses TRMDL → mesh, skeleton, materials.
 * Ported from gftool Model.cs — all GL rendering code removed.
 */
export class TrinityModelDecoder {
    _modelPath;
    _baseSkeletonCategoryHint = null;
    Name;
    Positions = [];
    Normals = [];
    UVs = [];
    Colors = [];
    Tangents = [];
    Binormals = [];
    BlendIndicies = [];
    BlendWeights = [];
    BlendBoneWeights = [];
    BlendIndiciesOriginal = [];
    BlendMeshNames = [];
    Indices = [];
    HasVertexColors = [];
    HasTangents = [];
    HasBinormals = [];
    HasSkinning = [];
    _materials = null;
    MaterialNames = [];
    SubmeshNames = [];
    _armature = null;
    get Armature() {
        return this._armature;
    }
    _blendIndexStats = null;
    constructor(modelFile, loadAllLods = false) {
        this.Name = this.GetFileNameWithoutExtension(modelFile);
        this._modelPath = new PathString(modelFile);
        // Stub - deserialize TRMDL
        const mdl = {};
        // Meshes
        if (loadAllLods) {
            for (const mesh of mdl.Meshes ?? []) {
                this.ParseMesh(this._modelPath.combine(mesh.PathName));
            }
        }
        else if (mdl.Meshes && mdl.Meshes.length > 0) {
            const mesh = mdl.Meshes[0]; // LOD0
            this.ParseMesh(this._modelPath.combine(mesh.PathName));
        }
        this._baseSkeletonCategoryHint = this.GuessBaseSkeletonCategoryFromMesh(mdl.Meshes != null && mdl.Meshes.length > 0 ? mdl.Meshes[0].PathName : null);
        // Materials
        for (const mat of mdl.Materials ?? []) {
            this.ParseMaterial(this._modelPath.combine(mat));
        }
        // Skeleton
        if (mdl.Skeleton != null) {
            this.ParseArmature(this._modelPath.combine(mdl.Skeleton.PathName));
        }
    }
    GetFileNameWithoutExtension(path) {
        const lastSlash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
        const fileName = lastSlash >= 0 ? path.substring(lastSlash + 1) : path;
        const lastDot = fileName.lastIndexOf('.');
        return lastDot >= 0 ? fileName.substring(0, lastDot) : fileName;
    }
    CreateExportData() {
        const subs = [];
        const count = this.Positions.length;
        for (let i = 0; i < count; i++) {
            const submeshName = i < this.SubmeshNames.length ? this.SubmeshNames[i] : `Submesh ${i}`;
            const materialName = i < this.MaterialNames.length ? this.MaterialNames[i] : '';
            subs.push({
                Name: submeshName,
                MaterialName: materialName,
                Positions: this.Positions[i],
                Normals: i < this.Normals.length ? this.Normals[i] : [],
                UVs: i < this.UVs.length ? this.UVs[i] : [],
                Colors: i < this.Colors.length ? this.Colors[i] : [],
                Tangents: i < this.Tangents.length ? this.Tangents[i] : [],
                Binormals: i < this.Binormals.length ? this.Binormals[i] : [],
                BlendIndices: i < this.BlendIndicies.length ? this.BlendIndicies[i] : [],
                BlendWeights: i < this.BlendWeights.length ? this.BlendWeights[i] : [],
                Indices: i < this.Indices.length ? this.Indices[i] : [],
                HasVertexColors: i < this.HasVertexColors.length && this.HasVertexColors[i],
                HasTangents: i < this.HasTangents.length && this.HasTangents[i],
                HasBinormals: i < this.HasBinormals.length && this.HasBinormals[i],
                HasSkinning: i < this.HasSkinning.length && this.HasSkinning[i]
            });
        }
        return {
            Name: this.Name,
            Submeshes: subs,
            Armature: this._armature,
            Materials: this._materials ?? []
        };
    }
    ParseMesh(_file) {
        // Stub - mesh parsing not implemented
        throw new Error('ParseMesh not implemented');
    }
    ParseMeshBuffer(_vertDesc, _vertexBuffers, _indexBuf, _polyType, _start, _count, _boneWeights, _meshName) {
        // Stub - mesh buffer parsing not implemented
        throw new Error('ParseMeshBuffer not implemented');
    }
    ParseMaterial(_file) {
        // Stub - material parsing not implemented
        throw new Error('ParseMaterial not implemented');
    }
    ParseArmature(_file) {
        // Stub - armature parsing not implemented
        throw new Error('ParseArmature not implemented');
    }
    GuessBaseSkeletonCategoryFromMesh(meshPathName) {
        if (!meshPathName || meshPathName.trim().length === 0) {
            return null;
        }
        const fn = this.GetFileNameWithoutExtension(meshPathName).toLowerCase();
        if (fn.startsWith('p0') || fn.startsWith('p1') || fn.startsWith('p2')) {
            return 'Protag';
        }
        if (fn.startsWith('bu_'))
            return 'CommonNPCbu';
        if (fn.startsWith('dm_'))
            return 'CommonNPCdm';
        if (fn.startsWith('df_'))
            return 'CommonNPCdf';
        if (fn.startsWith('em_'))
            return 'CommonNPCem';
        if (fn.startsWith('fm_'))
            return 'CommonNPCfm';
        if (fn.startsWith('ff_'))
            return 'CommonNPCff';
        if (fn.startsWith('gm_'))
            return 'CommonNPCgm';
        if (fn.startsWith('gf_'))
            return 'CommonNPCgf';
        if (fn.startsWith('rv_'))
            return 'CommonNPCrv';
        return null;
    }
    ApplyBlendIndexMapping() {
        // Stub - blend index mapping not implemented
    }
}
//# sourceMappingURL=TrinityModelDecoder.js.map