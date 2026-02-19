import type { ScanResult } from '../types/index.js';
/**
 * Spawn the CLI with --list to scan archive contents without extracting.
 * Parses .trmdl paths and groups them by folder prefix.
 */
export declare function scanArchive(arcPath: string): Promise<ScanResult>;
