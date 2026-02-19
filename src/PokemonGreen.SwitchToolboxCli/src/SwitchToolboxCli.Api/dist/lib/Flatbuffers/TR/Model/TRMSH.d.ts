import { TRBoundingBox, Sphere } from '../../Common/Math.js';
export declare enum TRIndexFormat {
    BYTE = 0,
    SHORT = 1,
    INT = 2,
    Count = 3
}
export declare enum TRVertexUsage {
    NONE = 0,
    POSITION = 1,
    NORMAL = 2,
    TANGENT = 3,
    BINORMAL = 4,
    COLOR = 5,
    TEX_COORD = 6,
    BLEND_INDEX = 7,
    BLEND_WEIGHTS = 8
}
export declare enum TRVertexFormat {
    NONE = 0,
    R8_G8_B8_A8_UNSIGNED_NORMALIZED = 20,
    W8_X8_Y8_Z8_UNSIGNED = 22,
    W32_X32_Y32_Z32_UNSIGNED = 52,
    W16_X16_Y16_Z16_UNSIGNED_NORMALIZED = 39,
    W16_X16_Y16_Z16_FLOAT = 43,
    X32_Y32_FLOAT = 48,
    X32_Y32_Z32_FLOAT = 51,
    W32_X32_Y32_Z32_FLOAT = 54
}
export declare class TRVertexElement {
    vertexElementSizeIndex: number;
    vertexUsage: TRVertexUsage;
    vertexElementLayer: number;
    vertexFormat: TRVertexFormat;
    vertexElementOffset: number;
}
export declare class TRVertexElementSize {
    elementSize: number;
}
export declare class TRVertexDeclaration {
    vertexElements: TRVertexElement[];
    vertexElementSizes: TRVertexElementSize[];
}
export declare class TRMeshPart {
    indexCount: number;
    indexOffset: number;
    Field_02: number;
    MaterialName: string;
    vertexDeclarationIndex: number;
}
export declare class TRBoneWeight {
    RigIndex: number;
    RigWeight: number;
}
export declare class TRMesh {
    Name: string;
    boundingBox: TRBoundingBox;
    IndexType: TRIndexFormat;
    vertexDeclaration: TRVertexDeclaration[];
    meshParts: TRMeshPart[];
    Field_05: number;
    Field_06: number;
    Field_07: number;
    Field_08: number;
    clipSphere: Sphere;
    boneWeight: TRBoneWeight[];
    Field_11: string;
    Field_12: string;
}
export declare class TRMSH {
    Version: number;
    Meshes: TRMesh[];
    bufferFilePath: string;
}
