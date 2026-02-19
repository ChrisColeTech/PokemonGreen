export class Vector2 {
    constructor(public x: number, public y: number) {}
    static get Zero(): Vector2 { return new Vector2(0, 0); }
}

export class Vector3 {
    constructor(public x: number, public y: number, public z: number) {}
    static get Zero(): Vector3 { return new Vector3(0, 0, 0); }
    static get UnitX(): Vector3 { return new Vector3(1, 0, 0); }
    static get UnitY(): Vector3 { return new Vector3(0, 1, 0); }
    static get UnitZ(): Vector3 { return new Vector3(0, 0, 1); }

    static Lerp(a: Vector3, b: Vector3, t: number): Vector3 {
        return new Vector3(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t,
            a.z + (b.z - a.z) * t
        );
    }
}

export class Vector4 {
    constructor(public x: number, public y: number, public z: number, public w: number) {}
    static get Zero(): Vector4 { return new Vector4(0, 0, 0, 0); }
    static get One(): Vector4 { return new Vector4(1, 1, 1, 1); }
}

export class MathQuaternion {
    constructor(public x: number, public y: number, public z: number, public w: number) {}
    static get Identity(): MathQuaternion { return new MathQuaternion(0, 0, 0, 1); }

    static FromAxisAngle(axis: Vector3, angle: number): MathQuaternion {
        const halfAngle = angle / 2;
        const sin = Math.sin(halfAngle);
        return new MathQuaternion(
            axis.x * sin,
            axis.y * sin,
            axis.z * sin,
            Math.cos(halfAngle)
        );
    }

    static Slerp(q1: MathQuaternion, q2: MathQuaternion, t: number): MathQuaternion {
        // Stub - return q1 for now
        return q1;
    }

    get LengthSquared(): number {
        return this.x * this.x + this.y * this.y + this.z * this.z + this.w * this.w;
    }

    Normalized(): MathQuaternion {
        const len = Math.sqrt(this.LengthSquared);
        if (len === 0) return MathQuaternion.Identity;
        return new MathQuaternion(this.x / len, this.y / len, this.z / len, this.w / len);
    }
}

export class Matrix4 {
    // Stub implementation
    static get Identity(): Matrix4 { return new Matrix4(); }
    
    static CreateScale(scale: Vector3): Matrix4 {
        return new Matrix4();
    }

    static CreateFromQuaternion(q: MathQuaternion): Matrix4 {
        return new Matrix4();
    }

    static CreateTranslation(translation: Vector3): Matrix4 {
        return new Matrix4();
    }

    static Invert(m: Matrix4): Matrix4 {
        return new Matrix4();
    }
}
