import { Vector3, MathQuaternion } from './Math.js';
export var PlayType;
(function (PlayType) {
    PlayType[PlayType["Once"] = 0] = "Once";
    PlayType[PlayType["Looped"] = 1] = "Looped";
})(PlayType || (PlayType = {}));
/**
 * Data-only animation decoder ported from gftool Animation.cs.
 * Parses GF animation FlatBuffers and provides per-bone pose sampling.
 * No GL rendering code — purely functional for DAE export.
 */
export class TrinityAnimationDecoder {
    Name;
    LoopType;
    FrameCount;
    FrameRate;
    _tracks = new Map();
    _trackOrder = [];
    get TrackCount() {
        return this._tracks.size;
    }
    get TrackNames() {
        return this._trackOrder;
    }
    constructor(anim, name) {
        this.Name = name;
        this.LoopType = anim.Info.DoesLoop !== 0 ? PlayType.Looped : PlayType.Once;
        this.FrameCount = anim.Info.KeyFrames;
        this.FrameRate = anim.Info.FrameRate;
        if (anim.Skeleton?.Tracks != null) {
            for (const track of anim.Skeleton.Tracks) {
                if (!track.Name || track.Name.trim().length === 0) {
                    continue;
                }
                const trackNameLower = track.Name.toLowerCase();
                if (!this._tracks.has(trackNameLower)) {
                    this._tracks.set(trackNameLower, track);
                    this._trackOrder.push(track.Name);
                }
                const normalized = TrinityAnimationDecoder.NormalizeBoneName(track.Name);
                if (normalized && normalized.length > 0) {
                    const normalizedLower = normalized.toLowerCase();
                    if (!this._tracks.has(normalizedLower)) {
                        this._tracks.set(normalizedLower, track);
                    }
                }
            }
        }
    }
    /**
     * Convert time in seconds to a frame index, respecting loop type and frame count.
     */
    GetFrame(timeSeconds) {
        const frameRate = this.FrameRate > 0 ? this.FrameRate : 30;
        let frame = timeSeconds * frameRate;
        if (this.FrameCount > 0) {
            if (this.LoopType === PlayType.Looped) {
                frame = frame % this.FrameCount;
            }
            frame = Math.max(0, Math.min(frame, Math.max(0, this.FrameCount - 1)));
        }
        return frame;
    }
    /**
     * Sample the pose for a specific bone at a given frame.
     * Returns false if no track exists for the bone.
     */
    TryGetPose(boneName, frame) {
        const track = this.TryGetTrack(boneName);
        if (!track) {
            return { scale: null, rotation: null, translation: null, success: false };
        }
        const scale = this.SampleVector(track.Scale, frame);
        const rotation = this.SampleRotation(track.Rotate, frame);
        const translation = this.SampleVector(track.Translate, frame);
        return { scale, rotation, translation, success: true };
    }
    /**
     * Check if a track exists for the given bone name.
     */
    HasTrack(boneName) {
        return this.TryGetTrack(boneName) !== null;
    }
    TryGetTrack(boneName) {
        if (!boneName || boneName.trim().length === 0) {
            return null;
        }
        const boneNameLower = boneName.toLowerCase();
        if (this._tracks.has(boneNameLower)) {
            return this._tracks.get(boneNameLower);
        }
        const normalized = TrinityAnimationDecoder.NormalizeBoneName(boneName);
        if (normalized && normalized.length > 0) {
            const normalizedLower = normalized.toLowerCase();
            if (this._tracks.has(normalizedLower)) {
                return this._tracks.get(normalizedLower);
            }
        }
        return null;
    }
    static NormalizeBoneName(name) {
        if (!name || name.trim().length === 0) {
            return '';
        }
        name = name.trim();
        const lastColon = name.lastIndexOf(':');
        if (lastColon >= 0 && lastColon < name.length - 1) {
            name = name.substring(lastColon + 1);
        }
        const lastPipe = name.lastIndexOf('|');
        if (lastPipe >= 0 && lastPipe < name.length - 1) {
            name = name.substring(lastPipe + 1);
        }
        const lastSlash = Math.max(name.lastIndexOf('/'), name.lastIndexOf('\\'));
        if (lastSlash >= 0 && lastSlash < name.length - 1) {
            name = name.substring(lastSlash + 1);
        }
        return name.trim();
    }
    SampleVector(channel, frame) {
        // Stub - vector sampling not fully implemented
        return Vector3.Zero;
    }
    SampleRotation(channel, frame) {
        // Stub - rotation sampling not fully implemented
        return MathQuaternion.Identity;
    }
    static ToVector3(v) {
        return new Vector3(v.X, v.Y, v.Z);
    }
    static ToMathQuaternion(_packed) {
        // Stub - quaternion unpacking not implemented
        return MathQuaternion.Identity;
    }
}
//# sourceMappingURL=TrinityAnimationDecoder.js.map