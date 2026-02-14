"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.isPresetJsonFile = isPresetJsonFile;
exports.orderPresetFileNames = orderPresetFileNames;
exports.discoverPresetPathsFromEntries = discoverPresetPathsFromEntries;
exports.discoverPresetFiles = discoverPresetFiles;
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
function isPresetJsonFile(entryName) {
    return (0, node_path_1.extname)(entryName).toLowerCase() === ".json";
}
function orderPresetFileNames(fileNames) {
    return [...fileNames].sort((a, b) => {
        const byLowerName = a.toLowerCase().localeCompare(b.toLowerCase());
        if (byLowerName !== 0) {
            return byLowerName;
        }
        return a.localeCompare(b);
    });
}
function discoverPresetPathsFromEntries(presetDirPath, entries) {
    const presetFileNames = entries.filter((entry) => entry.isFile() && isPresetJsonFile(entry.name)).map((entry) => entry.name);
    const orderedNames = orderPresetFileNames(presetFileNames);
    return orderedNames.map((entryName) => (0, node_path_1.resolve)(presetDirPath, entryName));
}
async function discoverPresetFiles(presetDirPath) {
    const entries = await (0, promises_1.readdir)(presetDirPath, { withFileTypes: true });
    return discoverPresetPathsFromEntries(presetDirPath, entries);
}
