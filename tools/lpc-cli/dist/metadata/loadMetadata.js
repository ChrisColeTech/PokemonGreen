"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.DEFAULT_METADATA_SOURCE_PATH = void 0;
exports.loadMetadataFromFile = loadMetadataFromFile;
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
const node_vm_1 = __importDefault(require("node:vm"));
exports.DEFAULT_METADATA_SOURCE_PATH = "D:/Projects/Universal-LPC-Spritesheet-Character-Generator/item-metadata.js";
async function loadMetadataFromFile(sourcePath = exports.DEFAULT_METADATA_SOURCE_PATH) {
    const resolvedPath = (0, node_path_1.resolve)(sourcePath);
    const sourceText = await (0, promises_1.readFile)(resolvedPath, "utf8");
    const sandbox = { window: {} };
    sandbox.globalThis = sandbox.window;
    try {
        node_vm_1.default.runInNewContext(sourceText, sandbox, {
            filename: resolvedPath,
            timeout: 3000
        });
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Unable to evaluate metadata source file: ${message}`);
    }
    const rawItems = sandbox.window.itemMetadata;
    const rawTree = sandbox.window.categoryTree;
    if (!isRecord(rawItems)) {
        throw new Error("Metadata source did not provide window.itemMetadata");
    }
    const itemsById = {};
    for (const [itemId, rawValue] of Object.entries(rawItems)) {
        if (!isRecord(rawValue)) {
            continue;
        }
        const sourceItem = rawValue;
        itemsById[itemId] = {
            id: itemId,
            name: asString(sourceItem.name, itemId),
            typeName: asString(sourceItem.type_name, itemId),
            priority: typeof sourceItem.priority === "number" ? sourceItem.priority : null,
            required: asStringArray(sourceItem.required),
            animations: asStringArray(sourceItem.animations),
            tags: asStringArray(sourceItem.tags),
            requiredTags: asStringArray(sourceItem.required_tags),
            excludedTags: asStringArray(sourceItem.excluded_tags),
            path: asStringArray(sourceItem.path),
            variants: asStringArray(sourceItem.variants),
            layers: toLayerMap(sourceItem.layers),
            credits: Array.isArray(sourceItem.credits) ? sourceItem.credits : [],
            raw: sourceItem
        };
    }
    const items = Object.values(itemsById).sort((a, b) => a.id.localeCompare(b.id));
    const categoryTree = toCategoryTreeNode(rawTree);
    const bodyTypes = inferBodyTypes(itemsById, categoryTree);
    return {
        sourcePath: resolvedPath,
        itemsById,
        items,
        categoryTree,
        bodyTypes
    };
}
function inferBodyTypes(itemsById, categoryTree) {
    const ordered = [];
    const seen = new Set();
    const bodyItem = itemsById.body;
    if (bodyItem) {
        for (const bodyType of bodyItem.required) {
            pushUnique(bodyType, ordered, seen);
        }
        for (const layer of Object.values(bodyItem.layers)) {
            for (const key of Object.keys(layer)) {
                if (key !== "zPos") {
                    pushUnique(key, ordered, seen);
                }
            }
        }
    }
    for (const item of Object.values(itemsById).sort((a, b) => a.id.localeCompare(b.id))) {
        for (const bodyType of item.required) {
            pushUnique(bodyType, ordered, seen);
        }
    }
    collectBodyTypesFromTree(categoryTree, ordered, seen);
    return ordered;
}
function collectBodyTypesFromTree(node, ordered, seen) {
    for (const value of node.required ?? []) {
        pushUnique(value, ordered, seen);
    }
    for (const child of Object.values(node.children)) {
        collectBodyTypesFromTree(child, ordered, seen);
    }
}
function toCategoryTreeNode(value) {
    if (!isRecord(value)) {
        return { items: [], children: {} };
    }
    const items = asStringArray(value.items);
    const children = {};
    if (isRecord(value.children)) {
        for (const [key, child] of Object.entries(value.children)) {
            children[key] = toCategoryTreeNode(child);
        }
    }
    const node = {
        items,
        children
    };
    if (typeof value.label === "string") {
        node.label = value.label;
    }
    if (typeof value.priority === "number") {
        node.priority = value.priority;
    }
    if (Array.isArray(value.required)) {
        node.required = asStringArray(value.required);
    }
    if (Array.isArray(value.animations)) {
        node.animations = asStringArray(value.animations);
    }
    return node;
}
function toLayerMap(value) {
    if (!isRecord(value)) {
        return {};
    }
    const layers = {};
    for (const [layerId, layerValue] of Object.entries(value)) {
        if (!isRecord(layerValue)) {
            continue;
        }
        const layer = {};
        for (const [key, rawLayerField] of Object.entries(layerValue)) {
            if (typeof rawLayerField === "string" || typeof rawLayerField === "number") {
                layer[key] = rawLayerField;
            }
        }
        layers[layerId] = layer;
    }
    return layers;
}
function asString(value, fallback) {
    return typeof value === "string" && value.length > 0 ? value : fallback;
}
function asStringArray(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value.filter((entry) => typeof entry === "string");
}
function pushUnique(value, list, seen) {
    if (!value || seen.has(value)) {
        return;
    }
    seen.add(value);
    list.push(value);
}
function isRecord(value) {
    return typeof value === "object" && value !== null;
}
