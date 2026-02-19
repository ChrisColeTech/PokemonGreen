/**
 * COLLADA XML DOM types for building DAE documents.
 * Ported from C# ColladaTypes.cs
 */
export declare enum SemanticType {
    POSITION = "POSITION",
    VERTEX = "VERTEX",
    NORMAL = "NORMAL",
    TEXCOORD = "TEXCOORD",
    COLOR = "COLOR",
    WEIGHT = "WEIGHT",
    JOINT = "JOINT",
    INV_BIND_MATRIX = "INV_BIND_MATRIX"
}
export interface ColladaGeometry {
    Id: string;
    Name: string;
    Mesh: ColladaMesh;
}
export interface ColladaMesh {
    Sources: ColladaSource[];
    Vertices: ColladaVertices;
    Polygons: ColladaPolygons[];
}
export interface ColladaSource {
    Id: string;
    Data: number[] | null;
    DataString: string[] | null;
    Stride: number;
    AccessorParams: string[];
    IsNameArray: boolean;
}
export interface ColladaVertices {
    Id: string;
    Inputs: ColladaInput[];
}
export interface ColladaPolygons {
    Count: number;
    RemappedIndices: number[] | null;
    MaterialSymbol: string;
    Inputs: ColladaInput[];
}
export interface ColladaInput {
    Semantic: SemanticType;
    Source: string;
    Offset: number;
    Set: number;
}
export interface ColladaMaterial {
    Id: string;
    Name: string;
    EffectUrl: string;
}
export interface ColladaImage {
    Id: string;
    Name: string;
    InitFrom: string;
}
export interface ColladaEffect {
    Id: string;
    Name: string;
    SurfaceSid: string;
    SamplerSid: string;
    ImageId: string;
}
export interface ColladaController {
    Id: string;
    Skin: ColladaSkin;
}
export interface ColladaSkin {
    Source: string;
    BindShapeMatrix: import('../Decoders/Math.js').Matrix4;
    Sources: ColladaSource[];
    Joints: ColladaJoints;
    VertexWeights: ColladaVertexWeights;
}
export interface ColladaJoints {
    Inputs: ColladaInput[];
}
export interface ColladaVertexWeights {
    Count: number;
    Inputs: ColladaInput[];
    VCount: number[] | null;
    V: number[] | null;
}
export interface ColladaNode {
    Id: string;
    Name: string;
    NodeType: string;
    Transform: import('../Decoders/Math.js').Matrix4;
    InstanceType: string;
    InstanceUrl: string;
    MaterialSymbol: string;
    MaterialTarget: string;
    SkeletonRootId: string;
    Children: ColladaNode[];
}
export declare function attr(name: string, value: string): string;
export declare function escapeXml(text: string): string;
export declare function fmtFloat(f: number): string;
export declare function fmtMatrix(_m: import('../Decoders/Math.js').Matrix4): string;
export declare function createElement(name: string, attributes: Record<string, string>, content?: string): string;
export declare function createElementWithChildren(name: string, attributes: Record<string, string>, children: string[]): string;
export declare const COLLADA_NAMESPACE = "http://www.collada.org/2005/11/COLLADASchema";
export declare const COLLADA_VERSION = "1.4.1";
//# sourceMappingURL=ColladaTypes.d.ts.map