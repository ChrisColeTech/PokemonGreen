"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.createDeterministicBundleZip = createDeterministicBundleZip;
exports.buildBundleEntries = buildBundleEntries;
const node_fs_1 = require("node:fs");
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
const promises_2 = require("node:stream/promises");
const yazl = __importStar(require("yazl"));
const FIXED_MTIME = new Date(1980, 0, 1, 0, 0, 0);
const FILE_MODE = 0o100644;
async function createDeterministicBundleZip(options) {
    const entries = buildBundleEntries(options);
    const bundlePath = (0, node_path_1.resolve)(options.outDir, `${options.outputBaseName}.bundle.zip`);
    try {
        await (0, promises_1.rm)(bundlePath, { force: true });
        await writeDeterministicZip(bundlePath, entries);
        return bundlePath;
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to create deterministic bundle zip at '${bundlePath}': ${message}`);
    }
}
function buildBundleEntries(options) {
    const root = options.outputBaseName;
    const entries = [
        {
            sourcePath: options.spritesheetPath,
            archivePath: `${root}/${root}.spritesheet.png`
        },
        {
            sourcePath: options.characterPath,
            archivePath: `${root}/${root}.character.json`
        },
        {
            sourcePath: options.creditsCsvPath,
            archivePath: `${root}/${root}.credits.csv`
        },
        {
            sourcePath: options.creditsTxtPath,
            archivePath: `${root}/${root}.credits.txt`
        }
    ];
    const sortedActions = [...options.splitActionPaths].sort((a, b) => {
        const byBaseName = (0, node_path_1.basename)(a).localeCompare((0, node_path_1.basename)(b));
        if (byBaseName !== 0) {
            return byBaseName;
        }
        return a.localeCompare(b);
    });
    for (const actionPath of sortedActions) {
        entries.push({
            sourcePath: actionPath,
            archivePath: `${root}/actions/${(0, node_path_1.basename)(actionPath)}`
        });
    }
    const collisionCheck = new Set();
    for (const entry of entries) {
        if (collisionCheck.has(entry.archivePath)) {
            throw new Error(`Bundle entry collision for '${entry.archivePath}'.`);
        }
        collisionCheck.add(entry.archivePath);
    }
    return entries;
}
async function writeDeterministicZip(zipPath, entries) {
    const zipFile = new yazl.ZipFile();
    try {
        for (const entry of entries) {
            const bytes = await (0, promises_1.readFile)(entry.sourcePath);
            zipFile.addBuffer(bytes, entry.archivePath, {
                compress: false,
                mtime: FIXED_MTIME,
                mode: FILE_MODE
            });
        }
    }
    catch (error) {
        zipFile.end();
        throw error;
    }
    zipFile.end();
    await (0, promises_2.pipeline)(zipFile.outputStream, (0, node_fs_1.createWriteStream)(zipPath));
}
