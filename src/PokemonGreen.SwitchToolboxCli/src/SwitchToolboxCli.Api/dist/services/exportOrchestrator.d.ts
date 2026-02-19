import type { ExportConfig, ExportStatus, ExportEvent } from '../types/index.js';
type ProgressCallback = (event: ExportEvent) => void;
export declare class ExportOrchestrator {
    private jobId;
    private phase;
    private total;
    private success;
    private failed;
    private skipped;
    private startTime;
    private activeProcesses;
    private cancelled;
    private listeners;
    start(config: ExportConfig): {
        id: string;
        total: number;
    };
    cancel(): number;
    getStatus(): ExportStatus;
    onProgress(cb: ProgressCallback): () => void;
    private emit;
    private runBatch;
    private runSingleExport;
}
export {};
