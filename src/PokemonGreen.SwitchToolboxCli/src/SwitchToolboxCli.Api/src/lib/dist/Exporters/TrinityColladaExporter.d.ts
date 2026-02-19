/**
 * Exports Trinity model data to COLLADA 1.4.1 DAE.
 * Consumes TrinityModelDecoder.ExportData.
 */
import type { ExportData } from '../Decoders/TrinityModelDecoder.js';
import type { TrinityAnimationDecoder } from '../Decoders/TrinityAnimationDecoder.js';
import type { TrinityArmature } from '../Decoders/TrinityArmature.js';
export declare class TrinityColladaExporter {
    /**
     * Export model-only DAE (no animation).
     */
    static Export(outputPath: string, data: ExportData): void;
    /**
     * Export model with baked animation.
     */
    static ExportWithAnimation(outputPath: string, data: ExportData, animation: TrinityAnimationDecoder): void;
    /**
     * Export clip-only DAE (skeleton + animation, no geometry).
     */
    static ExportClipOnly(outputPath: string, armature: TrinityArmature, animation: TrinityAnimationDecoder, name?: string): void;
}
//# sourceMappingURL=TrinityColladaExporter.d.ts.map