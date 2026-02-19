import { TRBoundingBox, Sphere } from '../../Common/Math.js';
export var TRIndexFormat;
(function (TRIndexFormat) {
    TRIndexFormat[TRIndexFormat["BYTE"] = 0] = "BYTE";
    TRIndexFormat[TRIndexFormat["SHORT"] = 1] = "SHORT";
    TRIndexFormat[TRIndexFormat["INT"] = 2] = "INT";
    TRIndexFormat[TRIndexFormat["Count"] = 3] = "Count";
})(TRIndexFormat || (TRIndexFormat = {}));
export var TRVertexUsage;
(function (TRVertexUsage) {
    TRVertexUsage[TRVertexUsage["NONE"] = 0] = "NONE";
    TRVertexUsage[TRVertexUsage["POSITION"] = 1] = "POSITION";
    TRVertexUsage[TRVertexUsage["NORMAL"] = 2] = "NORMAL";
    TRVertexUsage[TRVertexUsage["TANGENT"] = 3] = "TANGENT";
    TRVertexUsage[TRVertexUsage["BINORMAL"] = 4] = "BINORMAL";
    TRVertexUsage[TRVertexUsage["COLOR"] = 5] = "COLOR";
    TRVertexUsage[TRVertexUsage["TEX_COORD"] = 6] = "TEX_COORD";
    TRVertexUsage[TRVertexUsage["BLEND_INDEX"] = 7] = "BLEND_INDEX";
    TRVertexUsage[TRVertexUsage["BLEND_WEIGHTS"] = 8] = "BLEND_WEIGHTS";
})(TRVertexUsage || (TRVertexUsage = {}));
export var TRVertexFormat;
(function (TRVertexFormat) {
    TRVertexFormat[TRVertexFormat["NONE"] = 0] = "NONE";
    TRVertexFormat[TRVertexFormat["R8_G8_B8_A8_UNSIGNED_NORMALIZED"] = 20] = "R8_G8_B8_A8_UNSIGNED_NORMALIZED";
    TRVertexFormat[TRVertexFormat["W8_X8_Y8_Z8_UNSIGNED"] = 22] = "W8_X8_Y8_Z8_UNSIGNED";
    TRVertexFormat[TRVertexFormat["W32_X32_Y32_Z32_UNSIGNED"] = 52] = "W32_X32_Y32_Z32_UNSIGNED";
    TRVertexFormat[TRVertexFormat["W16_X16_Y16_Z16_UNSIGNED_NORMALIZED"] = 39] = "W16_X16_Y16_Z16_UNSIGNED_NORMALIZED";
    TRVertexFormat[TRVertexFormat["W16_X16_Y16_Z16_FLOAT"] = 43] = "W16_X16_Y16_Z16_FLOAT";
    TRVertexFormat[TRVertexFormat["X32_Y32_FLOAT"] = 48] = "X32_Y32_FLOAT";
    TRVertexFormat[TRVertexFormat["X32_Y32_Z32_FLOAT"] = 51] = "X32_Y32_Z32_FLOAT";
    TRVertexFormat[TRVertexFormat["W32_X32_Y32_Z32_FLOAT"] = 54] = "W32_X32_Y32_Z32_FLOAT";
})(TRVertexFormat || (TRVertexFormat = {}));
export class TRVertexElement {
    vertexElementSizeIndex = 0;
    vertexUsage = TRVertexUsage.NONE;
    vertexElementLayer = 0;
    vertexFormat = TRVertexFormat.NONE;
    vertexElementOffset = 0;
}
export class TRVertexElementSize {
    elementSize = 0;
}
export class TRVertexDeclaration {
    vertexElements = [];
    vertexElementSizes = [];
}
export class TRMeshPart {
    indexCount = 0;
    indexOffset = 0;
    Field_02 = 0;
    MaterialName = '';
    vertexDeclarationIndex = 0;
}
export class TRBoneWeight {
    RigIndex = 0;
    RigWeight = 0;
}
export class TRMesh {
    Name = '';
    boundingBox = new TRBoundingBox();
    IndexType = TRIndexFormat.BYTE;
    vertexDeclaration = [];
    meshParts = [];
    Field_05 = 0;
    Field_06 = 0;
    Field_07 = 0;
    Field_08 = 0;
    clipSphere = new Sphere();
    boneWeight = [];
    Field_11 = '';
    Field_12 = '';
}
export class TRMSH {
    Version = 0;
    Meshes = [];
    bufferFilePath = '';
}
//# sourceMappingURL=TRMSH.js.map