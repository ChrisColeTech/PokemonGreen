import { spawn } from 'child_process';
import path from 'path';
// Path to the published CLI executable
const CLI_EXE = path.resolve(import.meta.dirname, '../../../../publish/SwitchToolboxCli.App.exe');
/**
 * Spawn the CLI with --list to scan archive contents without extracting.
 * Parses .trmdl paths and groups them by folder prefix.
 */
export async function scanArchive(arcPath) {
    const startTime = Date.now();
    const lines = await runCliList(arcPath);
    const modelPaths = lines
        .filter(line => line.match(/^\s+\S+\.trmdl$/))
        .map(line => line.trim());
    const groups = groupByPrefix(modelPaths);
    return {
        totalModels: modelPaths.length,
        groups,
        scanTimeMs: Date.now() - startTime,
    };
}
function groupByPrefix(paths) {
    const map = new Map();
    for (const p of paths) {
        const parts = p.split('/');
        // Group by top two path segments: "pokemon/data", "field_graphic/terrain_model", etc.
        const prefix = parts.length >= 2 ? `${parts[0]}/${parts[1]}` : parts[0];
        if (!map.has(prefix))
            map.set(prefix, []);
        map.get(prefix).push(p);
    }
    return [...map.entries()]
        .map(([prefix, modelPaths]) => ({
        prefix,
        label: `${prefix} (${modelPaths.length})`,
        modelPaths,
        modelCount: modelPaths.length,
        selected: false,
    }))
        .sort((a, b) => b.modelCount - a.modelCount); // largest groups first
}
function runCliList(arcPath) {
    return new Promise((resolve, reject) => {
        const proc = spawn(CLI_EXE, ['--arc', arcPath, '--list']);
        const lines = [];
        let stderr = '';
        proc.stdout.on('data', (chunk) => {
            lines.push(...chunk.toString().split('\n'));
        });
        proc.stderr.on('data', (chunk) => {
            stderr += chunk.toString();
        });
        proc.on('close', (code) => {
            if (code === 0) {
                resolve(lines);
            }
            else {
                reject(new Error(`CLI exited with code ${code}: ${stderr.slice(0, 200)}`));
            }
        });
        proc.on('error', (err) => {
            reject(new Error(`Failed to spawn CLI: ${err.message}`));
        });
    });
}
//# sourceMappingURL=archiveScanner.js.map