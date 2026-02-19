import { Vector2, Vector3, Vector4 } from './Math.js';
import { PathString } from '../Utils/PathString.js';
import type { TrinityArmature } from './TrinityArmature.js';
import { TrinityMaterial } from './TrinityMaterial.js';
import type {
    TRMDL, TRMSH, TRMBF, TRBuffer, TRVertexDeclaration,
    TRVertexUsage, TRVertexFormat, TRIndexFormat,
    TRBoneWeight, TRMaterial, TRTexture,
    TRFloatParameter, TRVec2fParameter, TRVec3fParameter, TRVec4fParameter, TRSampler,
    TRSKL, TRTransformNode, TRJointInfo, TRHelperBoneInfo, TRMaterialShader, TRStringParameter
} from '../Flatbuffers/TR/Model/index.js';

enum BlendIndexRemapMode {
    None,
    BoneWeights,
    JointInfo,
    SkinningPalette,
    BoneMeta
}

interface BlendIndexStats {
    VertexCount: number;
    MaxIndex: number;
}

export interface ExportSubmesh {
    Name: string;
    MaterialName: string;
    Positions: Vector3[];
    Normals: Vector3[];
    UVs: Vector2[];
    Colors: Vector4[];
    Tangents: Vector4[];
    Binormals: Vector3[];
    BlendIndices: Vector4[];
    BlendWeights: Vector4[];
    Indices: number[];
    HasVertexColors: boolean;
    HasTangents: boolean;
    HasBinormals: boolean;
    HasSkinning: boolean;
}

export interface ExportData {
    Name: string;
    Submeshes: readonly ExportSubmesh[];
    Armature: TrinityArmature | null;
    Materials: readonly TrinityMaterial[];
}

/**
 * Headless Trinity model decoder. Parses TRMDL → mesh, skeleton, materials.
 * Ported from gftool Model.cs — all GL rendering code removed.
 */
export class TrinityModelDecoder {
    private _modelPath: PathString;
    private _baseSkeletonCategoryHint: string | null = null;

    public Name: string;

    private Positions: Vector3[][] = [];
    private Normals: Vector3[][] = [];
    private UVs: Vector2[][] = [];
    private Colors: Vector4[][] = [];
    private Tangents: Vector4[][] = [];
    private Binormals: Vector3[][] = [];
    private BlendIndicies: Vector4[][] = [];
    private BlendWeights: Vector4[][] = [];
    private BlendBoneWeights: (TRBoneWeight[] | null)[] = [];
    private BlendIndiciesOriginal: Vector4[][] = [];
    private BlendMeshNames: string[] = [];

    private Indices: number[][] = [];
    private HasVertexColors: boolean[] = [];
    private HasTangents: boolean[] = [];
    private HasBinormals: boolean[] = [];
    private HasSkinning: boolean[] = [];

    private _materials: TrinityMaterial[] | null = null;
    private MaterialNames: string[] = [];
    private SubmeshNames: string[] = [];

    private _armature: TrinityArmature | null = null;
    public get Armature(): TrinityArmature | null {
        return this._armature;
    }

    private _blendIndexStats: BlendIndexStats | null = null;

    constructor(modelFile: string, loadAllLods: boolean = false) {
        this.Name = this.GetFileNameWithoutExtension(modelFile);
        this._modelPath = new PathString(modelFile);

        // Stub - deserialize TRMDL
        const mdl: TRMDL = {} as TRMDL;

        // Meshes
        if (loadAllLods) {
            for (const mesh of mdl.Meshes ?? []) {
                this.ParseMesh(this._modelPath.combine(mesh.PathName));
            }
        } else if (mdl.Meshes && mdl.Meshes.length > 0) {
            const mesh = mdl.Meshes[0]; // LOD0
            this.ParseMesh(this._modelPath.combine(mesh.PathName));
        }

        this._baseSkeletonCategoryHint = this.GuessBaseSkeletonCategoryFromMesh(
            mdl.Meshes != null && mdl.Meshes.length > 0 ? mdl.Meshes[0].PathName : null
        );

        // Materials
        for (const mat of mdl.Materials ?? []) {
            this.ParseMaterial(this._modelPath.combine(mat));
        }

        // Skeleton
        if (mdl.Skeleton != null) {
            this.ParseArmature(this._modelPath.combine(mdl.Skeleton.PathName));
        }
    }

    private GetFileNameWithoutExtension(path: string): string {
        const lastSlash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
        const fileName = lastSlash >= 0 ? path.substring(lastSlash + 1) : path;
        const lastDot = fileName.lastIndexOf('.');
        return lastDot >= 0 ? fileName.substring(0, lastDot) : fileName;
    }

    public CreateExportData(): ExportData {
        const subs: ExportSubmesh[] = [];
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

    private ParseMesh(_file: string): void {
        // Stub - mesh parsing not implemented
        throw new Error('ParseMesh not implemented');
    }

    private ParseMeshBuffer(
        _vertDesc: TRVertexDeclaration,
        _vertexBuffers: TRBuffer[],
        _indexBuf: TRBuffer,
        _polyType: TRIndexFormat,
        _start: number,
        _count: number,
        _boneWeights: TRBoneWeight[] | null,
        _meshName: string
    ): void {
        // Stub - mesh buffer parsing not implemented
        throw new Error('ParseMeshBuffer not implemented');
    }

    private ParseMaterial(_file: string): void {
        // Stub - material parsing not implemented
        throw new Error('ParseMaterial not implemented');
    }

    private ParseArmature(_file: string): void {
        // Stub - armature parsing not implemented
        throw new Error('ParseArmature not implemented');
    }

    private GuessBaseSkeletonCategoryFromMesh(meshPathName: string | null): string | null {
        if (!meshPathName || meshPathName.trim().length === 0) {
            return null;
        }

        const fn = this.GetFileNameWithoutExtension(meshPathName).toLowerCase();
        if (fn.startsWith('p0') || fn.startsWith('p1') || fn.startsWith('p2')) {
            return 'Protag';
        }

        if (fn.startsWith('bu_')) return 'CommonNPCbu';
        if (fn.startsWith('dm_')) return 'CommonNPCdm';
        if (fn.startsWith('df_')) return 'CommonNPCdf';
        if (fn.startsWith('em_')) return 'CommonNPCem';
        if (fn.startsWith('fm_')) return 'CommonNPCfm';
        if (fn.startsWith('ff_')) return 'CommonNPCff';
        if (fn.startsWith('gm_')) return 'CommonNPCgm';
        if (fn.startsWith('gf_')) return 'CommonNPCgf';
        if (fn.startsWith('rv_')) return 'CommonNPCrv';

        return null;
    }

    private ApplyBlendIndexMapping(): void {
        // Stub - blend index mapping not implemented
    }
}
