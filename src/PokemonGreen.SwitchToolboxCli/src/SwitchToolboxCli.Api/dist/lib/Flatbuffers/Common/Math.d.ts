export declare class Vector2f {
    X: number;
    Y: number;
    constructor(X?: number, Y?: number);
}
export declare class Vector3f {
    X: number;
    Y: number;
    Z: number;
    constructor(X?: number, Y?: number, Z?: number);
}
export declare class Vector4f {
    W: number;
    X: number;
    Y: number;
    Z: number;
    constructor(W?: number, X?: number, Y?: number, Z?: number);
}
export declare class Vector2i {
    X: number;
    Y: number;
    constructor(X?: number, Y?: number);
}
export declare class Sphere {
    X: number;
    Y: number;
    Z: number;
    Radius: number;
    constructor(X?: number, Y?: number, Z?: number, Radius?: number);
}
export declare class TRBoundingBox {
    MinBound: Vector3f;
    MaxBound: Vector3f;
    constructor(MinBound?: Vector3f, MaxBound?: Vector3f);
}
export declare class PackedQuaternion {
    X: number;
    Y: number;
    Z: number;
    constructor(X?: number, Y?: number, Z?: number);
}
export declare class RGBA {
    R: number;
    G: number;
    B: number;
    A: number;
    constructor(R?: number, G?: number, B?: number, A?: number);
}
export declare class Transform {
    Scale: Vector3f;
    Rotate: Vector4f;
    Translate: Vector3f;
    constructor(Scale?: Vector3f, Rotate?: Vector4f, Translate?: Vector3f);
}
