import { Vector3, MathQuaternion } from './Math.js';
import type { Animation } from '../Flatbuffers/GF/Animation/index.js';
export declare enum PlayType {
    Once = 0,
    Looped = 1
}
/**
 * Data-only animation decoder ported from gftool Animation.cs.
 * Parses GF animation FlatBuffers and provides per-bone pose sampling.
 * No GL rendering code — purely functional for DAE export.
 */
export declare class TrinityAnimationDecoder {
    Name: string;
    LoopType: PlayType;
    FrameCount: number;
    FrameRate: number;
    private _tracks;
    private _trackOrder;
    get TrackCount(): number;
    get TrackNames(): readonly string[];
    constructor(anim: Animation, name: string);
    /**
     * Convert time in seconds to a frame index, respecting loop type and frame count.
     */
    GetFrame(timeSeconds: number): number;
    /**
     * Sample the pose for a specific bone at a given frame.
     * Returns false if no track exists for the bone.
     */
    TryGetPose(boneName: string, frame: number): {
        scale: Vector3 | null;
        rotation: MathQuaternion | null;
        translation: Vector3 | null;
        success: boolean;
    };
    /**
     * Check if a track exists for the given bone name.
     */
    HasTrack(boneName: string): boolean;
    private TryGetTrack;
    private static NormalizeBoneName;
    private SampleVector;
    private SampleRotation;
    private static ToVector3;
    private static ToMathQuaternion;
}
//# sourceMappingURL=TrinityAnimationDecoder.d.ts.map