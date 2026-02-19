import { Vector4f, TRBoundingBox } from '../../Common/Math.js';
export class ModelMesh {
    PathName = '';
}
export class ModelSkeleton {
    PathName = '';
}
export class ModelLODEntry {
    Index = 0;
}
export class ModelLOD {
    Entries = [];
    Type = '';
}
export class TRMDL {
    Field_00 = 0;
    Meshes = [];
    Skeleton = new ModelSkeleton();
    Materials = [];
    LODs = [];
    Bounds = new TRBoundingBox();
    Field_06 = new Vector4f();
}
//# sourceMappingURL=TRMDL.js.map