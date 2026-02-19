import { Vector4f, TRBoundingBox } from '../../Common/Math.js';
export declare class ModelMesh {
    PathName: string;
}
export declare class ModelSkeleton {
    PathName: string;
}
export declare class ModelLODEntry {
    Index: number;
}
export declare class ModelLOD {
    Entries: ModelLODEntry[];
    Type: string;
}
export declare class TRMDL {
    Field_00: number;
    Meshes: ModelMesh[];
    Skeleton: ModelSkeleton;
    Materials: string[];
    LODs: ModelLOD[];
    Bounds: TRBoundingBox;
    Field_06: Vector4f;
}
//# sourceMappingURL=TRMDL.d.ts.map