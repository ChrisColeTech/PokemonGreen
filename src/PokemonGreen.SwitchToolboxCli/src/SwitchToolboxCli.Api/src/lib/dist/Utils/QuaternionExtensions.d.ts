import { PackedQuaternion } from '../Flatbuffers/Common/Math.js';
export interface Quaternion {
    X: number;
    Y: number;
    Z: number;
    W: number;
}
export declare class QuaternionExtensions {
    static readonly PI_DIVISOR: number;
    static readonly PI_ADDEND: number;
    private static readonly PI_HALF;
    private static readonly SCALE;
    private static ExpandFloat;
    private static QuantizeFloat;
    static Unpack(pq: PackedQuaternion): Quaternion;
    static Pack(q: Quaternion): PackedQuaternion;
    private static Normalize;
    static ToDictionary(quaternion: Quaternion): Record<string, number>;
}
//# sourceMappingURL=QuaternionExtensions.d.ts.map