export class Vector2 {
    x;
    y;
    constructor(x, y) {
        this.x = x;
        this.y = y;
    }
    static get Zero() { return new Vector2(0, 0); }
}
export class Vector3 {
    x;
    y;
    z;
    constructor(x, y, z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    static get Zero() { return new Vector3(0, 0, 0); }
    static get UnitX() { return new Vector3(1, 0, 0); }
    static get UnitY() { return new Vector3(0, 1, 0); }
    static get UnitZ() { return new Vector3(0, 0, 1); }
    static Lerp(a, b, t) {
        return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
}
export class Vector4 {
    x;
    y;
    z;
    w;
    constructor(x, y, z, w) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    static get Zero() { return new Vector4(0, 0, 0, 0); }
    static get One() { return new Vector4(1, 1, 1, 1); }
}
export class MathQuaternion {
    x;
    y;
    z;
    w;
    constructor(x, y, z, w) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    static get Identity() { return new MathQuaternion(0, 0, 0, 1); }
    static FromAxisAngle(axis, angle) {
        const halfAngle = angle / 2;
        const sin = Math.sin(halfAngle);
        return new MathQuaternion(axis.x * sin, axis.y * sin, axis.z * sin, Math.cos(halfAngle));
    }
    static Slerp(q1, q2, t) {
        // Stub - return q1 for now
        return q1;
    }
    get LengthSquared() {
        return this.x * this.x + this.y * this.y + this.z * this.z + this.w * this.w;
    }
    Normalized() {
        const len = Math.sqrt(this.LengthSquared);
        if (len === 0)
            return MathQuaternion.Identity;
        return new MathQuaternion(this.x / len, this.y / len, this.z / len, this.w / len);
    }
}
export class Matrix4 {
    // Stub implementation
    static get Identity() { return new Matrix4(); }
    static CreateScale(scale) {
        return new Matrix4();
    }
    static CreateFromQuaternion(q) {
        return new Matrix4();
    }
    static CreateTranslation(translation) {
        return new Matrix4();
    }
    static Invert(m) {
        return new Matrix4();
    }
}
//# sourceMappingURL=Math.js.map