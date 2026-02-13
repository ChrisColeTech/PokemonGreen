#!/usr/bin/env node
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
const commander_1 = require("commander");
const loadMetadata_1 = require("./metadata/loadMetadata");
const credits_1 = require("./generate/credits");
const renderer_1 = require("./generate/renderer");
const resolver_1 = require("./generate/resolver");
const validatePreset_1 = require("./validation/validatePreset");
const program = new commander_1.Command();
function withBodyTypeOverride(preset, bodyTypeOverride) {
    if (!bodyTypeOverride) {
        return preset;
    }
    return {
        ...preset,
        bodyType: bodyTypeOverride
    };
}
function toIssueMessages(prefix, issues) {
    return issues.map((issue) => `[${prefix}.${issue.code}] ${issue.message}`);
}
async function validateAssetsRoot(assetsRootPath) {
    try {
        const stats = await (0, promises_1.stat)(assetsRootPath);
        if (!stats.isDirectory()) {
            return `Assets root is not a directory: ${assetsRootPath}`;
        }
    }
    catch {
        return `Assets root does not exist: ${assetsRootPath}`;
    }
    return null;
}
async function validatePresetAndResolution(options) {
    const assetsRootError = await validateAssetsRoot(options.assetsRootPath);
    if (assetsRootError) {
        return {
            schemaErrors: [],
            metadataErrors: [],
            metadataWarnings: [],
            readinessErrors: [assetsRootError]
        };
    }
    const presetRaw = await (0, validatePreset_1.loadPresetFromFile)(options.presetPath);
    const presetShapeResult = (0, validatePreset_1.validatePresetShape)(presetRaw);
    if (!presetShapeResult.preset) {
        return {
            schemaErrors: toIssueMessages("schema", presetShapeResult.errors),
            metadataErrors: [],
            metadataWarnings: [],
            readinessErrors: []
        };
    }
    const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyTypeOverride);
    const metadata = await (0, loadMetadata_1.loadMetadataFromFile)(options.metadataPath);
    const semantic = (0, validatePreset_1.validatePresetAgainstMetadata)(preset, metadata);
    const metadataErrors = toIssueMessages("metadata", semantic.result.errors);
    const metadataWarnings = toIssueMessages("metadata", semantic.result.warnings);
    if (metadataErrors.length > 0) {
        return {
            schemaErrors: [],
            metadataErrors,
            metadataWarnings,
            readinessErrors: [],
            bodyType: semantic.normalizedPreset.bodyType,
            selectionCount: semantic.normalizedPreset.selections.length
        };
    }
    let readinessErrors = [];
    try {
        const resolved = (0, resolver_1.resolveStandardCharacter)(semantic.normalizedPreset, metadata, options.assetsRootPath);
        readinessErrors = await collectMissingSpritePaths(resolved);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        readinessErrors = [message];
    }
    return {
        schemaErrors: [],
        metadataErrors,
        metadataWarnings,
        readinessErrors,
        bodyType: semantic.normalizedPreset.bodyType,
        selectionCount: semantic.normalizedPreset.selections.length
    };
}
async function collectMissingSpritePaths(resolved) {
    const uniquePaths = Array.from(new Set(resolved.layers
        .flatMap((layer) => layer.animations.map((animation) => animation.absolutePath))
        .concat(resolved.customLayers.map((layer) => layer.absolutePath)))).sort((a, b) => a.localeCompare(b));
    const checks = await Promise.all(uniquePaths.map(async (absolutePath) => {
        try {
            await (0, promises_1.access)(absolutePath);
            return null;
        }
        catch {
            return absolutePath;
        }
    }));
    return checks.filter((value) => value !== null);
}
function printGroupedValidation(result) {
    const hasErrors = result.schemaErrors.length > 0 || result.metadataErrors.length > 0 || result.readinessErrors.length > 0;
    if (!hasErrors) {
        if (result.metadataWarnings.length > 0) {
            console.log("Preset validation succeeded with warnings:");
            for (const warning of result.metadataWarnings) {
                console.log(`- ${warning}`);
            }
        }
        else {
            console.log("Preset validation succeeded.");
        }
        console.log(`Body type: ${result.bodyType ?? "<unknown>"}`);
        console.log(`Selections: ${result.selectionCount ?? 0}`);
        return;
    }
    console.error("Preset validation failed:");
    if (result.schemaErrors.length > 0) {
        console.error("Schema:");
        for (const error of result.schemaErrors) {
            console.error(`- ${error}`);
        }
    }
    if (result.metadataErrors.length > 0) {
        console.error("Metadata match:");
        for (const error of result.metadataErrors) {
            console.error(`- ${error}`);
        }
    }
    if (result.readinessErrors.length > 0) {
        console.error("Path resolution readiness:");
        const listed = result.readinessErrors.slice(0, 30);
        for (const error of listed) {
            console.error(`- ${error}`);
        }
        if (result.readinessErrors.length > listed.length) {
            console.error(`- ...and ${result.readinessErrors.length - listed.length} more`);
        }
    }
    if (result.metadataWarnings.length > 0) {
        console.error("Warnings:");
        for (const warning of result.metadataWarnings) {
            console.error(`- ${warning}`);
        }
    }
}
function resolveOutputName(presetPath, outputName) {
    const raw = outputName && outputName.trim().length > 0 ? outputName.trim() : (0, node_path_1.basename)(presetPath, (0, node_path_1.extname)(presetPath));
    const normalized = raw.replace(/[^a-zA-Z0-9._-]/g, "-").replace(/-+/g, "-").replace(/^-|-$/g, "");
    return normalized.length > 0 ? normalized : "character";
}
program
    .name("lpc-cli")
    .description("Slim LPC spritesheet CLI (Phase 3)")
    .version("0.1.0");
const listCommand = program.command("list").description("List metadata resources");
listCommand
    .command("items")
    .description("List available item ids")
    .option("--metadata <path>", "Path to item-metadata.js", loadMetadata_1.DEFAULT_METADATA_SOURCE_PATH)
    .action(async (options) => {
    const metadata = await (0, loadMetadata_1.loadMetadataFromFile)(options.metadata);
    console.log(`Loaded ${metadata.items.length} items from ${metadata.sourcePath}`);
    for (const item of metadata.items) {
        console.log(`- ${item.id} (type: ${item.typeName}, variants: ${item.variants.length})`);
    }
});
listCommand
    .command("body-types")
    .description("List discovered body types")
    .option("--metadata <path>", "Path to item-metadata.js", loadMetadata_1.DEFAULT_METADATA_SOURCE_PATH)
    .action(async (options) => {
    const metadata = await (0, loadMetadata_1.loadMetadataFromFile)(options.metadata);
    console.log(`Body types (${metadata.bodyTypes.length}):`);
    for (const bodyType of metadata.bodyTypes) {
        console.log(`- ${bodyType}`);
    }
});
program
    .command("validate")
    .description("Validate a preset against metadata")
    .requiredOption("--preset <path>", "Path to preset JSON")
    .requiredOption("--assetsRoot <path>", "Path to assets root")
    .option("--metadata <path>", "Path to item-metadata.js", loadMetadata_1.DEFAULT_METADATA_SOURCE_PATH)
    .option("--bodyType <value>", "Override bodyType from preset")
    .action(async (options) => {
    const result = await validatePresetAndResolution({
        presetPath: options.preset,
        metadataPath: options.metadata,
        assetsRootPath: (0, node_path_1.resolve)(options.assetsRoot),
        bodyTypeOverride: options.bodyType
    });
    printGroupedValidation(result);
    if (result.schemaErrors.length > 0 || result.metadataErrors.length > 0 || result.readinessErrors.length > 0) {
        process.exitCode = 1;
    }
});
program
    .command("generate")
    .description("Generate deterministic LPC spritesheet + credits")
    .requiredOption("--preset <path>", "Path to preset JSON")
    .requiredOption("--assetsRoot <path>", "Path to assets root")
    .option("--metadata <path>", "Path to item-metadata.js", loadMetadata_1.DEFAULT_METADATA_SOURCE_PATH)
    .option("--outDir <path>", "Output directory", "./output")
    .option("--bodyType <value>", "Override bodyType from preset")
    .option("--outputName <name>", "Output file base name (without extension)")
    .action(async (options) => {
    const assetsRootPath = (0, node_path_1.resolve)(options.assetsRoot);
    const assetsRootError = await validateAssetsRoot(assetsRootPath);
    if (assetsRootError) {
        console.error(assetsRootError);
        process.exitCode = 1;
        return;
    }
    const presetRaw = await (0, validatePreset_1.loadPresetFromFile)(options.preset);
    const presetShapeResult = (0, validatePreset_1.validatePresetShape)(presetRaw);
    if (!presetShapeResult.preset) {
        console.error("Preset schema validation failed:");
        for (const error of presetShapeResult.errors) {
            console.error(`- [schema.${error.code}] ${error.message}`);
        }
        process.exitCode = 1;
        return;
    }
    const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyType);
    const metadata = await (0, loadMetadata_1.loadMetadataFromFile)(options.metadata);
    const semantic = (0, validatePreset_1.validatePresetAgainstMetadata)(preset, metadata);
    if (semantic.result.errors.length > 0) {
        console.error("Preset validation failed:");
        for (const error of semantic.result.errors) {
            console.error(`- [metadata.${error.code}] ${error.message}`);
        }
        if (semantic.result.warnings.length > 0) {
            console.error("Warnings:");
            for (const warning of semantic.result.warnings) {
                console.error(`- [metadata.${warning.code}] ${warning.message}`);
            }
        }
        process.exitCode = 1;
        return;
    }
    if (semantic.result.warnings.length > 0) {
        console.log("Preset validation warnings:");
        for (const warning of semantic.result.warnings) {
            console.log(`- [metadata.${warning.code}] ${warning.message}`);
        }
    }
    const resolved = (0, resolver_1.resolveStandardCharacter)(semantic.normalizedPreset, metadata, assetsRootPath);
    const outDir = (0, node_path_1.resolve)(options.outDir);
    await (0, promises_1.mkdir)(outDir, { recursive: true });
    const outputBaseName = resolveOutputName(options.preset, options.outputName);
    const spritesheetPath = (0, node_path_1.resolve)(outDir, `${outputBaseName}.spritesheet.png`);
    const characterPath = (0, node_path_1.resolve)(outDir, `${outputBaseName}.character.json`);
    const creditsCsvPath = (0, node_path_1.resolve)(outDir, `${outputBaseName}.credits.csv`);
    const creditsTxtPath = (0, node_path_1.resolve)(outDir, `${outputBaseName}.credits.txt`);
    await Promise.all([
        (0, promises_1.rm)(spritesheetPath, { force: true }),
        (0, promises_1.rm)(characterPath, { force: true }),
        (0, promises_1.rm)(creditsCsvPath, { force: true }),
        (0, promises_1.rm)(creditsTxtPath, { force: true })
    ]);
    await (0, renderer_1.renderStandardSpritesheet)(resolved, spritesheetPath);
    const credits = (0, credits_1.collectCredits)(resolved, metadata);
    const characterJson = {
        bodyType: resolved.bodyType,
        dimensions: resolved.dimensions,
        selections: resolved.selections.map((selection) => ({
            itemId: selection.itemId,
            typeName: selection.typeName,
            variant: selection.variant
        })),
        layers: resolved.layers.map((layer) => ({
            itemId: layer.itemId,
            typeName: layer.typeName,
            variant: layer.variant,
            layerKey: layer.layerKey,
            layerNum: layer.layerNum,
            zPos: layer.zPos,
            animations: layer.animations.map((animation) => ({
                animation: animation.animation,
                yOffset: animation.yOffset,
                path: animation.relativePath
            }))
        })),
        customAnimations: resolved.customAnimations,
        customLayers: resolved.customLayers.map((layer) => ({
            itemId: layer.itemId,
            typeName: layer.typeName,
            variant: layer.variant,
            layerKey: layer.layerKey,
            layerNum: layer.layerNum,
            zPos: layer.zPos,
            customAnimation: layer.customAnimation,
            path: layer.relativePath
        }))
    };
    await (0, promises_1.writeFile)(characterPath, `${JSON.stringify(characterJson, null, 2)}\n`, "utf8");
    await (0, promises_1.writeFile)(creditsCsvPath, (0, credits_1.creditsToCsv)(credits), "utf8");
    await (0, promises_1.writeFile)(creditsTxtPath, (0, credits_1.creditsToTxt)(credits), "utf8");
    console.log(`Generated ${spritesheetPath}`);
    console.log(`Generated ${characterPath}`);
    console.log(`Generated ${creditsCsvPath}`);
    console.log(`Generated ${creditsTxtPath}`);
    console.log(`Resolved layers: ${resolved.layers.length}`);
    console.log(`Resolved custom layers: ${resolved.customLayers.length}`);
    console.log(`Resolved custom animations: ${resolved.customAnimations.length}`);
    console.log(`Credits entries: ${credits.length}`);
});
program.parseAsync(process.argv).catch((error) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Command failed: ${message}`);
    process.exitCode = 1;
});
