export interface FolderGroup {
    prefix: string;
    label: string;
    modelPaths: string[];
    modelCount: number;
    selected: boolean;
}
export interface ScanResult {
    totalModels: number;
    groups: FolderGroup[];
    scanTimeMs: number;
}
export interface ExportConfig {
    arcPath: string;
    outputDir: string;
    modelPaths: string[];
    parallelJobs: number;
}
export type ExportPhase = 'idle' | 'scanning' | 'exporting' | 'complete' | 'cancelled' | 'error';
export interface ExportStatus {
    jobId: string;
    phase: ExportPhase;
    total: number;
    success: number;
    failed: number;
    skipped: number;
    elapsedMs: number;
}
export type ExportEvent = {
    type: 'started';
    total: number;
} | {
    type: 'model-done';
    model: string;
    success: boolean;
    index: number;
} | {
    type: 'model-skipped';
    model: string;
    index: number;
} | {
    type: 'complete';
    success: number;
    failed: number;
    skipped: number;
    elapsed: number;
} | {
    type: 'error';
    message: string;
} | {
    type: 'log';
    message: string;
};
