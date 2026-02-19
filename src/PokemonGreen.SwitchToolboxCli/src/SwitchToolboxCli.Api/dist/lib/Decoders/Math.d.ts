export declare class Vector2 {
    x: number;
    y: number;
    constructor(x: number, y: number);
    static get Zero(): Vector2;
}
export declare class Vector3 {
    x: number;
    y: number;
    z: number;
    constructor(x: number, y: number, z: number);
    static get Zero(): Vector3;
    static get UnitX(): Vector3;
    static get UnitY(): Vector3;
    static get UnitZ(): Vector3;
    static Lerp(a: Vector3, b: Vector3, t: number): Vector3;
}
export declare class Vector4 {
    x: number;
    y: number;
    z: number;
    w: number;
    constructor(x: number, y: number, z: number, w: number);
    static get Zero(): Vector4;
    static get One(): Vector4;
}
export declare class Quaternion {
    x: number;
    y: number;
    z: number;
    w: number;
    constructor(x: number, y: number, z: number, w: number);
    static get Identity(): Quaternion;
    static FromAxisAngle(axis: Vector3, angle: number): Quaternion;
    static Slerp(q1: Quaternion, q2: Quaternion, t: number): Quaternion;
    get LengthSquared(): number;
    Normalized(): Quaternion;
}
export declare class Matrix4 {
    static get Identity(): Matrix4;
    static CreateScale(scale: Vector3): Matrix4;
    static CreateFromQuaternion(q: Quaternion): Matrix4;
    static CreateTranslation(translation: Vector3): Matrix4;
    static Invert(m: Matrix4): Matrix4;
}
