"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.loadPresetFromFile = loadPresetFromFile;
exports.validatePresetShape = validatePresetShape;
exports.validatePresetAgainstMetadata = validatePresetAgainstMetadata;
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
async function loadPresetFromFile(filePath) {
    const resolvedPath = (0, node_path_1.resolve)(filePath);
    const content = await (0, promises_1.readFile)(resolvedPath, "utf8");
    try {
        return JSON.parse(content);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Preset file is not valid JSON: ${message}`);
    }
}
function validatePresetShape(rawPreset) {
    const errors = [];
    if (!isRecord(rawPreset)) {
        errors.push({ code: "invalid_preset", message: "Preset must be a JSON object." });
        return { errors };
    }
    const version = rawPreset.version;
    const bodyType = rawPreset.bodyType;
    const selections = rawPreset.selections;
    if (typeof version !== "number") {
        errors.push({ code: "invalid_version", message: "Preset.version must be a number." });
    }
    if (typeof bodyType !== "string" || bodyType.length === 0) {
        errors.push({ code: "invalid_body_type", message: "Preset.bodyType must be a non-empty string." });
    }
    if (!Array.isArray(selections)) {
        errors.push({ code: "invalid_selections", message: "Preset.selections must be an array." });
    }
    const parsedSelections = [];
    if (Array.isArray(selections)) {
        selections.forEach((selection, index) => {
            if (!isRecord(selection)) {
                errors.push({
                    code: "invalid_selection",
                    message: `Preset.selections[${index}] must be an object.`
                });
                return;
            }
            if (typeof selection.itemId !== "string" || selection.itemId.length === 0) {
                errors.push({
                    code: "invalid_item_id",
                    message: `Preset.selections[${index}].itemId must be a non-empty string.`
                });
                return;
            }
            if (selection.variant !== undefined && typeof selection.variant !== "string") {
                errors.push({
                    code: "invalid_variant",
                    message: `Preset.selections[${index}].variant must be a string when provided.`
                });
                return;
            }
            if (selection.enabled !== undefined && typeof selection.enabled !== "boolean") {
                errors.push({
                    code: "invalid_enabled",
                    message: `Preset.selections[${index}].enabled must be a boolean when provided.`
                });
                return;
            }
            parsedSelections.push({
                itemId: selection.itemId,
                variant: selection.variant,
                enabled: selection.enabled
            });
        });
    }
    if (errors.length > 0) {
        return { errors };
    }
    return {
        preset: {
            version: version,
            bodyType: bodyType,
            selections: parsedSelections
        },
        errors
    };
}
function validatePresetAgainstMetadata(preset, metadata) {
    const errors = [];
    const warnings = [];
    if (!metadata.bodyTypes.includes(preset.bodyType)) {
        errors.push({
            code: "unknown_body_type",
            message: `Preset bodyType '${preset.bodyType}' is not present in metadata.`
        });
    }
    const normalizedSelections = [];
    const seenItemIds = new Set();
    const seenTypeNames = new Map();
    for (const selection of preset.selections) {
        if (selection.enabled === false) {
            continue;
        }
        if (seenItemIds.has(selection.itemId)) {
            errors.push({
                code: "duplicate_item",
                message: `Item '${selection.itemId}' appears multiple times in selections.`
            });
        }
        seenItemIds.add(selection.itemId);
        const item = metadata.itemsById[selection.itemId];
        if (!item) {
            errors.push({
                code: "unknown_item_id",
                message: `Unknown itemId '${selection.itemId}'.`
            });
            continue;
        }
        if (item.required.length > 0 && !item.required.includes(preset.bodyType)) {
            errors.push({
                code: "invalid_item_for_body_type",
                message: `Item '${selection.itemId}' does not support bodyType '${preset.bodyType}'.`
            });
        }
        const duplicateGroupItem = seenTypeNames.get(item.typeName);
        if (duplicateGroupItem && duplicateGroupItem !== item.id) {
            errors.push({
                code: "duplicate_selection_group",
                message: `Items '${duplicateGroupItem}' and '${item.id}' are both in selection group '${item.typeName}'.`
            });
        }
        if (!duplicateGroupItem) {
            seenTypeNames.set(item.typeName, item.id);
        }
        if (selection.variant !== undefined) {
            if (item.variants.length === 0) {
                errors.push({
                    code: "variant_not_supported",
                    message: `Item '${selection.itemId}' does not support variants, but preset provided '${selection.variant}'.`
                });
            }
            if (!item.variants.includes(selection.variant)) {
                errors.push({
                    code: "unknown_variant",
                    message: `Unknown variant '${selection.variant}' for item '${selection.itemId}'.`
                });
            }
            normalizedSelections.push({
                itemId: selection.itemId,
                variant: selection.variant
            });
            continue;
        }
        if (item.variants.length > 1) {
            errors.push({
                code: "variant_required",
                message: `Item '${selection.itemId}' requires explicit variant selection. Available variants: ${item.variants.join(", ")}.`
            });
        }
        normalizedSelections.push({
            itemId: selection.itemId,
            variant: null
        });
    }
    return {
        normalizedPreset: {
            bodyType: preset.bodyType,
            selections: normalizedSelections
        },
        result: { errors, warnings }
    };
}
function isRecord(value) {
    return typeof value === "object" && value !== null;
}
