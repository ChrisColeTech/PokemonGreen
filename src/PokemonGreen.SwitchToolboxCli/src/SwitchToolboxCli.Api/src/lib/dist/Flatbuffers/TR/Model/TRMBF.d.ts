export declare class TRBuffer {
    Bytes: Uint8Array;
}
export declare class TRMorphTarget {
    morphBuffers: TRBuffer[];
}
export declare class TRModelBuffer {
    IndexBuffer: TRBuffer[];
    VertexBuffer: TRBuffer[];
    MorphTargets: TRMorphTarget[];
}
export declare class TRMBF {
    Field_00: number;
    TRMeshBuffers: TRModelBuffer[];
}
//# sourceMappingURL=TRMBF.d.ts.map