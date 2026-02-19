import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs';
import { randomUUID } from 'crypto';
const CLI_EXE = path.resolve(import.meta.dirname, '../../../../publish/SwitchToolboxCli.App.exe');
export class ExportOrchestrator {
    jobId = null;
    phase = 'idle';
    total = 0;
    success = 0;
    failed = 0;
    skipped = 0;
    startTime = 0;
    activeProcesses = [];
    cancelled = false;
    listeners = new Set();
    start(config) {
        if (this.phase === 'exporting') {
            throw new Error('Export already in progress');
        }
        this.jobId = randomUUID();
        this.phase = 'exporting';
        this.total = config.modelPaths.length;
        this.success = 0;
        this.failed = 0;
        this.skipped = 0;
        this.startTime = Date.now();
        this.cancelled = false;
        this.activeProcesses = [];
        this.emit({ type: 'started', total: this.total });
        // Run in background
        this.runBatch(config).catch((err) => {
            this.phase = 'error';
            this.emit({ type: 'error', message: err.message });
        });
        return { id: this.jobId, total: this.total };
    }
    cancel() {
        this.cancelled = true;
        const killed = this.activeProcesses.length;
        for (const proc of this.activeProcesses) {
            try {
                proc.kill();
            }
            catch { /* ignore */ }
        }
        this.activeProcesses = [];
        this.phase = 'cancelled';
        return killed;
    }
    getStatus() {
        return {
            jobId: this.jobId ?? '',
            phase: this.phase,
            total: this.total,
            success: this.success,
            failed: this.failed,
            skipped: this.skipped,
            elapsedMs: this.startTime > 0 ? Date.now() - this.startTime : 0,
        };
    }
    onProgress(cb) {
        this.listeners.add(cb);
        return () => this.listeners.delete(cb);
    }
    emit(event) {
        for (const cb of this.listeners) {
            try {
                cb(event);
            }
            catch { /* ignore */ }
        }
    }
    async runBatch(config) {
        const { arcPath, outputDir, modelPaths, parallelJobs } = config;
        const concurrency = Math.max(1, Math.min(parallelJobs, 16));
        let index = 0;
        const queue = [...modelPaths];
        const runNext = async () => {
            while (queue.length > 0 && !this.cancelled) {
                const modelPath = queue.shift();
                const currentIndex = index++;
                const modelName = path.basename(modelPath, '.trmdl');
                const modelOutDir = path.join(outputDir, modelName);
                // Skip if already exported
                if (fs.existsSync(path.join(modelOutDir, 'model.dae'))) {
                    this.skipped++;
                    this.emit({ type: 'model-skipped', model: modelName, index: currentIndex });
                    continue;
                }
                const success = await this.runSingleExport(arcPath, modelPath, modelOutDir);
                if (success) {
                    this.success++;
                    this.emit({ type: 'model-done', model: modelName, success: true, index: currentIndex });
                }
                else {
                    this.failed++;
                    this.emit({ type: 'model-done', model: modelName, success: false, index: currentIndex });
                }
            }
        };
        // Launch N concurrent workers
        const workers = Array.from({ length: concurrency }, () => runNext());
        await Promise.all(workers);
        if (!this.cancelled) {
            this.phase = 'complete';
            this.emit({
                type: 'complete',
                success: this.success,
                failed: this.failed,
                skipped: this.skipped,
                elapsed: (Date.now() - this.startTime) / 1000,
            });
        }
    }
    runSingleExport(arcPath, modelPath, outputDir) {
        return new Promise((resolve) => {
            const proc = spawn(CLI_EXE, ['--arc', arcPath, '--model', modelPath, '-o', outputDir], {
                stdio: ['ignore', 'pipe', 'pipe'],
            });
            this.activeProcesses.push(proc);
            const timeout = setTimeout(() => {
                try {
                    proc.kill();
                }
                catch { /* ignore */ }
                resolve(false);
            }, 300_000); // 5 min timeout
            proc.on('close', (code) => {
                clearTimeout(timeout);
                this.activeProcesses = this.activeProcesses.filter(p => p !== proc);
                resolve(code === 0);
            });
            proc.on('error', () => {
                clearTimeout(timeout);
                this.activeProcesses = this.activeProcesses.filter(p => p !== proc);
                resolve(false);
            });
        });
    }
}
//# sourceMappingURL=exportOrchestrator.js.map