export class Vector2f {
    X;
    Y;
    constructor(X = 0.0, Y = 0.0) {
        this.X = X;
        this.Y = Y;
    }
}
export class Vector3f {
    X;
    Y;
    Z;
    constructor(X = 0.0, Y = 0.0, Z = 0.0) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }
}
export class Vector4f {
    W;
    X;
    Y;
    Z;
    constructor(W = 0.0, X = 0.0, Y = 0.0, Z = 0.0) {
        this.W = W;
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }
}
export class Vector2i {
    X;
    Y;
    constructor(X = 0, Y = 0) {
        this.X = X;
        this.Y = Y;
    }
}
export class Sphere {
    X;
    Y;
    Z;
    Radius;
    constructor(X = 0.0, Y = 0.0, Z = 0.0, Radius = 0.0) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
        this.Radius = Radius;
    }
}
export class TRBoundingBox {
    MinBound;
    MaxBound;
    constructor(MinBound = new Vector3f(), MaxBound = new Vector3f()) {
        this.MinBound = MinBound;
        this.MaxBound = MaxBound;
    }
}
export class PackedQuaternion {
    X;
    Y;
    Z;
    constructor(X = 0, Y = 0, Z = 0) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }
}
export class RGBA {
    R;
    G;
    B;
    A;
    constructor(R = 0.0, G = 0.0, B = 0.0, A = 0.0) {
        this.R = R;
        this.G = G;
        this.B = B;
        this.A = A;
    }
}
export class Transform {
    Scale;
    Rotate;
    Translate;
    constructor(Scale = new Vector3f(), Rotate = new Vector4f(), Translate = new Vector3f()) {
        this.Scale = Scale;
        this.Rotate = Rotate;
        this.Translate = Translate;
    }
}
//# sourceMappingURL=Math.js.map