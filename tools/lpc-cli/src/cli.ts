#!/usr/bin/env node
import { access, mkdir, rm, stat, writeFile } from "node:fs/promises";
import { basename, extname, resolve } from "node:path";
import { Command } from "commander";
import { DEFAULT_METADATA_SOURCE_PATH, loadMetadataFromFile } from "./metadata/loadMetadata";
import { collectCredits, creditsToCsv, creditsToTxt } from "./generate/credits";
import { renderStandardSpritesheet } from "./generate/renderer";
import { resolveStandardCharacter } from "./generate/resolver";
import { loadPresetFromFile, validatePresetAgainstMetadata, validatePresetShape } from "./validation/validatePreset";

const program = new Command();

interface ValidateAndResolveResult {
  schemaErrors: string[];
  metadataErrors: string[];
  metadataWarnings: string[];
  readinessErrors: string[];
  bodyType?: string;
  selectionCount?: number;
}

function withBodyTypeOverride<T extends { bodyType: string }>(preset: T, bodyTypeOverride?: string): T {
  if (!bodyTypeOverride) {
    return preset;
  }
  return {
    ...preset,
    bodyType: bodyTypeOverride
  };
}

function toIssueMessages(prefix: string, issues: Array<{ code: string; message: string }>): string[] {
  return issues.map((issue) => `[${prefix}.${issue.code}] ${issue.message}`);
}

async function validateAssetsRoot(assetsRootPath: string): Promise<string | null> {
  try {
    const stats = await stat(assetsRootPath);
    if (!stats.isDirectory()) {
      return `Assets root is not a directory: ${assetsRootPath}`;
    }
  } catch {
    return `Assets root does not exist: ${assetsRootPath}`;
  }
  return null;
}

async function validatePresetAndResolution(options: {
  presetPath: string;
  metadataPath: string;
  assetsRootPath: string;
  bodyTypeOverride?: string;
}): Promise<ValidateAndResolveResult> {
  const assetsRootError = await validateAssetsRoot(options.assetsRootPath);
  if (assetsRootError) {
    return {
      schemaErrors: [],
      metadataErrors: [],
      metadataWarnings: [],
      readinessErrors: [assetsRootError]
    };
  }

  const presetRaw = await loadPresetFromFile(options.presetPath);
  const presetShapeResult = validatePresetShape(presetRaw);
  if (!presetShapeResult.preset) {
    return {
      schemaErrors: toIssueMessages("schema", presetShapeResult.errors),
      metadataErrors: [],
      metadataWarnings: [],
      readinessErrors: []
    };
  }

  const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyTypeOverride);
  const metadata = await loadMetadataFromFile(options.metadataPath);
  const semantic = validatePresetAgainstMetadata(preset, metadata);
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

  let readinessErrors: string[] = [];
  try {
    const resolved = resolveStandardCharacter(semantic.normalizedPreset, metadata, options.assetsRootPath);
    readinessErrors = await collectMissingSpritePaths(resolved);
  } catch (error) {
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

async function collectMissingSpritePaths(resolved: Awaited<ReturnType<typeof resolveStandardCharacter>>): Promise<string[]> {
  const uniquePaths = Array.from(
    new Set(
      resolved.layers
        .flatMap((layer) => layer.animations.map((animation) => animation.absolutePath))
        .concat(resolved.customLayers.map((layer) => layer.absolutePath))
    )
  ).sort((a, b) => a.localeCompare(b));

  const checks = await Promise.all(
    uniquePaths.map(async (absolutePath) => {
      try {
        await access(absolutePath);
        return null;
      } catch {
        return absolutePath;
      }
    })
  );

  return checks.filter((value): value is string => value !== null);
}

function printGroupedValidation(result: ValidateAndResolveResult): void {
  const hasErrors =
    result.schemaErrors.length > 0 || result.metadataErrors.length > 0 || result.readinessErrors.length > 0;

  if (!hasErrors) {
    if (result.metadataWarnings.length > 0) {
      console.log("Preset validation succeeded with warnings:");
      for (const warning of result.metadataWarnings) {
        console.log(`- ${warning}`);
      }
    } else {
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

function resolveOutputName(presetPath: string, outputName?: string): string {
  const raw = outputName && outputName.trim().length > 0 ? outputName.trim() : basename(presetPath, extname(presetPath));
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
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .action(async (options: { metadata: string }) => {
    const metadata = await loadMetadataFromFile(options.metadata);
    console.log(`Loaded ${metadata.items.length} items from ${metadata.sourcePath}`);
    for (const item of metadata.items) {
      console.log(`- ${item.id} (type: ${item.typeName}, variants: ${item.variants.length})`);
    }
  });

listCommand
  .command("body-types")
  .description("List discovered body types")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .action(async (options: { metadata: string }) => {
    const metadata = await loadMetadataFromFile(options.metadata);
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
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .option("--bodyType <value>", "Override bodyType from preset")
  .action(async (options: { preset: string; assetsRoot: string; metadata: string; bodyType?: string }) => {
    const result = await validatePresetAndResolution({
      presetPath: options.preset,
      metadataPath: options.metadata,
      assetsRootPath: resolve(options.assetsRoot),
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
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .option("--outDir <path>", "Output directory", "./output")
  .option("--bodyType <value>", "Override bodyType from preset")
  .option("--outputName <name>", "Output file base name (without extension)")
  .action(
    async (options: {
      preset: string;
      assetsRoot: string;
      metadata: string;
      outDir: string;
      bodyType?: string;
      outputName?: string;
    }) => {
      const assetsRootPath = resolve(options.assetsRoot);
      const assetsRootError = await validateAssetsRoot(assetsRootPath);
      if (assetsRootError) {
        console.error(assetsRootError);
        process.exitCode = 1;
        return;
      }

      const presetRaw = await loadPresetFromFile(options.preset);
      const presetShapeResult = validatePresetShape(presetRaw);
      if (!presetShapeResult.preset) {
        console.error("Preset schema validation failed:");
        for (const error of presetShapeResult.errors) {
          console.error(`- [schema.${error.code}] ${error.message}`);
        }
        process.exitCode = 1;
        return;
      }

      const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyType);
      const metadata = await loadMetadataFromFile(options.metadata);
      const semantic = validatePresetAgainstMetadata(preset, metadata);
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

      const resolved = resolveStandardCharacter(semantic.normalizedPreset, metadata, assetsRootPath);
      const outDir = resolve(options.outDir);
      await mkdir(outDir, { recursive: true });

      const outputBaseName = resolveOutputName(options.preset, options.outputName);
      const spritesheetPath = resolve(outDir, `${outputBaseName}.spritesheet.png`);
      const characterPath = resolve(outDir, `${outputBaseName}.character.json`);
      const creditsCsvPath = resolve(outDir, `${outputBaseName}.credits.csv`);
      const creditsTxtPath = resolve(outDir, `${outputBaseName}.credits.txt`);

      await Promise.all([
        rm(spritesheetPath, { force: true }),
        rm(characterPath, { force: true }),
        rm(creditsCsvPath, { force: true }),
        rm(creditsTxtPath, { force: true })
      ]);

      await renderStandardSpritesheet(resolved, spritesheetPath);

      const credits = collectCredits(resolved, metadata);

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

      await writeFile(characterPath, `${JSON.stringify(characterJson, null, 2)}\n`, "utf8");
      await writeFile(creditsCsvPath, creditsToCsv(credits), "utf8");
      await writeFile(creditsTxtPath, creditsToTxt(credits), "utf8");

      console.log(`Generated ${spritesheetPath}`);
      console.log(`Generated ${characterPath}`);
      console.log(`Generated ${creditsCsvPath}`);
      console.log(`Generated ${creditsTxtPath}`);
      console.log(`Resolved layers: ${resolved.layers.length}`);
      console.log(`Resolved custom layers: ${resolved.customLayers.length}`);
      console.log(`Resolved custom animations: ${resolved.customAnimations.length}`);
      console.log(`Credits entries: ${credits.length}`);
    }
  );

program.parseAsync(process.argv).catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);
  console.error(`Command failed: ${message}`);
  process.exitCode = 1;
});
