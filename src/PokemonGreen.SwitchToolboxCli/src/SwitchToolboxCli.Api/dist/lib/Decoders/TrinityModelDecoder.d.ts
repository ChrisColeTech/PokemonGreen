import { Vector2, Vector3, Vector4 } from './Math.js';
import type { TrinityArmature } from './TrinityArmature.js';
import { TrinityMaterial } from './TrinityMaterial.js';
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
export declare class TrinityModelDecoder {
    private _modelPath;
    private _baseSkeletonCategoryHint;
    Name: string;
    private Positions;
    private Normals;
    private UVs;
    private Colors;
    private Tangents;
    private Binormals;
    private BlendIndicies;
    private BlendWeights;
    private BlendBoneWeights;
    private BlendIndiciesOriginal;
    private BlendMeshNames;
    private Indices;
    private HasVertexColors;
    private HasTangents;
    private HasBinormals;
    private HasSkinning;
    private _materials;
    private MaterialNames;
    private SubmeshNames;
    private _armature;
    get Armature(): TrinityArmature | null;
    private _blendIndexStats;
    constructor(modelFile: string, loadAllLods?: boolean);
    private GetFileNameWithoutExtension;
    CreateExportData(): ExportData;
    private ParseMesh;
    private ParseMeshBuffer;
    private ParseMaterial;
    private ParseArmature;
    private GuessBaseSkeletonCategoryFromMesh;
    private ApplyBlendIndexMapping;
}
