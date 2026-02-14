#!/usr/bin/env node
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
const commander_1 = require("commander");
const loadMetadata_1 = require("./metadata/loadMetadata");
const presetDiscovery_1 = require("./batch/presetDiscovery");
const bundleZip_1 = require("./generate/bundleZip");
const credits_1 = require("./generate/credits");
const renderer_1 = require("./generate/renderer");
const resolver_1 = require("./generate/resolver");
const splitActions_1 = require("./generate/splitActions");
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
function buildSchemaFailureMessage(errors) {
    const details = errors.map((error) => `- [schema.${error.code}] ${error.message}`).join("\n");
    return `Preset schema validation failed:\n${details}`;
}
function buildMetadataFailureMessage(errors, warnings) {
    const lines = [
        "Preset validation failed:",
        ...errors.map((error) => `- [metadata.${error.code}] ${error.message}`)
    ];
    if (warnings.length > 0) {
        lines.push("Warnings:");
        for (const warning of warnings) {
            lines.push(`- [metadata.${warning.code}] ${warning.message}`);
        }
    }
    return lines.join("\n");
}
async function generatePresetArtifacts(options) {
    const assetsRootError = await validateAssetsRoot(options.assetsRootPath);
    if (assetsRootError) {
        throw new Error(assetsRootError);
    }
    const presetRaw = await (0, validatePreset_1.loadPresetFromFile)(options.presetPath);
    const presetShapeResult = (0, validatePreset_1.validatePresetShape)(presetRaw);
    if (!presetShapeResult.preset) {
        throw new Error(buildSchemaFailureMessage(presetShapeResult.errors));
    }
    const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyTypeOverride);
    const metadata = await (0, loadMetadata_1.loadMetadataFromFile)(options.metadataPath);
    const semantic = (0, validatePreset_1.validatePresetAgainstMetadata)(preset, metadata);
    if (semantic.result.errors.length > 0) {
        throw new Error(buildMetadataFailureMessage(semantic.result.errors, semantic.result.warnings));
    }
    const validationWarnings = semantic.result.warnings.map((warning) => `[metadata.${warning.code}] ${warning.message}`);
    const resolved = (0, resolver_1.resolveStandardCharacter)(semantic.normalizedPreset, metadata, options.assetsRootPath);
    await (0, promises_1.mkdir)(options.outDir, { recursive: true });
    const outputBaseName = resolveOutputName(options.presetPath, options.outputName);
    const spritesheetPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.spritesheet.png`);
    const characterPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.character.json`);
    const creditsCsvPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.credits.csv`);
    const creditsTxtPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.credits.txt`);
    const actionsDirPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.actions`);
    const bundleZipPath = (0, node_path_1.resolve)(options.outDir, `${outputBaseName}.bundle.zip`);
    await Promise.all([
        (0, promises_1.rm)(spritesheetPath, { force: true }),
        (0, promises_1.rm)(characterPath, { force: true }),
        (0, promises_1.rm)(creditsCsvPath, { force: true }),
        (0, promises_1.rm)(creditsTxtPath, { force: true })
    ]);
    await (0, renderer_1.renderStandardSpritesheet)(resolved, spritesheetPath);
    let splitActionPaths = [];
    if (options.splitActions) {
        splitActionPaths = await (0, splitActions_1.exportSplitActionSprites)({
            spritesheetPath,
            outputDir: actionsDirPath,
            character: resolved
        });
    }
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
    if (options.bundleZip) {
        await (0, bundleZip_1.createDeterministicBundleZip)({
            outputBaseName,
            outDir: options.outDir,
            spritesheetPath,
            characterPath,
            creditsCsvPath,
            creditsTxtPath,
            splitActionPaths
        });
    }
    return {
        outputBaseName,
        spritesheetPath,
        characterPath,
        creditsCsvPath,
        creditsTxtPath,
        actionsDirPath,
        bundleZipPath,
        splitActionPaths,
        resolvedLayerCount: resolved.layers.length,
        resolvedCustomLayerCount: resolved.customLayers.length,
        resolvedCustomAnimationCount: resolved.customAnimations.length,
        creditsCount: credits.length,
        validationWarnings
    };
}
function getErrorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
function summarizeError(error) {
    const message = getErrorMessage(error).trim();
    const [firstLine] = message.split(/\r?\n/, 1);
    return firstLine && firstLine.length > 0 ? firstLine : "Unknown error";
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
    .option("--splitActions", "Export per-action PNG files")
    .option("--bundleZip", "Write deterministic ZIP bundle for generated outputs")
    .action(async (options) => {
    try {
        const result = await generatePresetArtifacts({
            presetPath: (0, node_path_1.resolve)(options.preset),
            assetsRootPath: (0, node_path_1.resolve)(options.assetsRoot),
            metadataPath: options.metadata,
            outDir: (0, node_path_1.resolve)(options.outDir),
            bodyTypeOverride: options.bodyType,
            outputName: options.outputName,
            splitActions: options.splitActions,
            bundleZip: options.bundleZip
        });
        if (result.validationWarnings.length > 0) {
            console.log("Preset validation warnings:");
            for (const warning of result.validationWarnings) {
                console.log(`- ${warning}`);
            }
        }
        console.log(`Generated ${result.spritesheetPath}`);
        console.log(`Generated ${result.characterPath}`);
        console.log(`Generated ${result.creditsCsvPath}`);
        console.log(`Generated ${result.creditsTxtPath}`);
        if (options.splitActions) {
            console.log(`Generated split actions in ${result.actionsDirPath} (${result.splitActionPaths.length} files)`);
        }
        if (options.bundleZip) {
            console.log(`Generated ${result.bundleZipPath}`);
        }
        console.log(`Resolved layers: ${result.resolvedLayerCount}`);
        console.log(`Resolved custom layers: ${result.resolvedCustomLayerCount}`);
        console.log(`Resolved custom animations: ${result.resolvedCustomAnimationCount}`);
        console.log(`Credits entries: ${result.creditsCount}`);
    }
    catch (error) {
        console.error(getErrorMessage(error));
        process.exitCode = 1;
    }
});
program
    .command("generate-batch")
    .description("Generate outputs for every preset JSON in a folder")
    .requiredOption("--presetDir <path>", "Path to folder containing preset JSON files")
    .requiredOption("--assetsRoot <path>", "Path to assets root")
    .requiredOption("--outDir <path>", "Output directory")
    .option("--metadata <path>", "Path to item-metadata.js", loadMetadata_1.DEFAULT_METADATA_SOURCE_PATH)
    .option("--splitActions", "Export per-action PNG files")
    .option("--bundleZip", "Write deterministic ZIP bundle for generated outputs")
    .option("--bodyType <value>", "Override bodyType from preset")
    .action(async (options) => {
    const presetDirPath = (0, node_path_1.resolve)(options.presetDir);
    const outDirPath = (0, node_path_1.resolve)(options.outDir);
    const assetsRootPath = (0, node_path_1.resolve)(options.assetsRoot);
    const presetDirError = await validateAssetsRoot(presetDirPath);
    if (presetDirError) {
        console.error(`Preset directory error: ${presetDirError.replace("Assets root", "Preset directory")}`);
        process.exitCode = 1;
        return;
    }
    const presetPaths = await (0, presetDiscovery_1.discoverPresetFiles)(presetDirPath);
    if (presetPaths.length === 0) {
        console.log(`No preset JSON files found in ${presetDirPath}`);
        console.log("Batch summary:");
        console.log("- Total presets: 0");
        console.log("- Successes: 0");
        console.log("- Failures: 0");
        return;
    }
    const failures = [];
    let successCount = 0;
    for (const presetPath of presetPaths) {
        const presetName = (0, node_path_1.basename)(presetPath);
        try {
            await generatePresetArtifacts({
                presetPath,
                assetsRootPath,
                metadataPath: options.metadata,
                outDir: outDirPath,
                bodyTypeOverride: options.bodyType,
                splitActions: options.splitActions,
                bundleZip: options.bundleZip
            });
            successCount += 1;
            console.log(`[ok] ${presetName}`);
        }
        catch (error) {
            const reason = summarizeError(error);
            failures.push({ presetName, reason });
            console.error(`[failed] ${presetName}: ${reason}`);
        }
    }
    console.log("Batch summary:");
    console.log(`- Total presets: ${presetPaths.length}`);
    console.log(`- Successes: ${successCount}`);
    console.log(`- Failures: ${failures.length}`);
    if (failures.length > 0) {
        console.log("- Failed presets:");
        for (const failure of failures) {
            console.log(`  - ${failure.presetName}: ${failure.reason}`);
        }
        process.exitCode = 1;
    }
});
program.parseAsync(process.argv).catch((error) => {
    const message = getErrorMessage(error);
    console.error(`Command failed: ${message}`);
    process.exitCode = 1;
});
