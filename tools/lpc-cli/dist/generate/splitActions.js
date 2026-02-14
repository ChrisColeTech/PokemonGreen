"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.exportSplitActionSprites = exportSplitActionSprites;
exports.buildActionExportAreas = buildActionExportAreas;
exports.validateActionAreaNames = validateActionAreaNames;
const promises_1 = require("node:fs/promises");
const node_path_1 = require("node:path");
const sharp_1 = __importDefault(require("sharp"));
const constants_1 = require("./constants");
async function exportSplitActionSprites(options) {
    const image = (0, sharp_1.default)(options.spritesheetPath, { failOn: "error" });
    const metadata = await image.metadata();
    const sourceWidth = metadata.width;
    const sourceHeight = metadata.height;
    if (!sourceWidth || !sourceHeight) {
        throw new Error(`Unable to read spritesheet dimensions from '${options.spritesheetPath}'.`);
    }
    if (sourceWidth < options.character.dimensions.width || sourceHeight < options.character.dimensions.height) {
        throw new Error(`Spritesheet dimensions ${sourceWidth}x${sourceHeight} do not match resolved dimensions ${options.character.dimensions.width}x${options.character.dimensions.height}.`);
    }
    const areas = buildActionExportAreas(options.character);
    validateActionAreaNames(areas);
    await (0, promises_1.rm)(options.outputDir, { recursive: true, force: true });
    await (0, promises_1.mkdir)(options.outputDir, { recursive: true });
    const generatedPaths = [];
    for (const area of areas) {
        if (area.left < 0 || area.top < 0 || area.width <= 0 || area.height <= 0) {
            throw new Error(`Invalid export bounds for action '${area.name}': left=${area.left}, top=${area.top}, width=${area.width}, height=${area.height}.`);
        }
        if (area.left + area.width > sourceWidth || area.top + area.height > sourceHeight) {
            throw new Error(`Action '${area.name}' bounds exceed spritesheet size ${sourceWidth}x${sourceHeight}: left=${area.left}, top=${area.top}, width=${area.width}, height=${area.height}.`);
        }
        const outputPath = (0, node_path_1.resolve)(options.outputDir, `${area.name}.png`);
        await (0, sharp_1.default)(options.spritesheetPath)
            .extract({ left: area.left, top: area.top, width: area.width, height: area.height })
            .png()
            .toFile(outputPath);
        generatedPaths.push(outputPath);
    }
    return generatedPaths;
}
function buildActionExportAreas(character) {
    const areas = [];
    for (const [index, animation] of constants_1.STANDARD_ANIMATIONS.entries()) {
        const currentOffset = animation.yOffset;
        const nextOffset = constants_1.STANDARD_ANIMATIONS[index + 1]?.yOffset ?? constants_1.SPRITESHEET_HEIGHT;
        const segmentHeight = nextOffset - currentOffset;
        if (segmentHeight <= 0 || segmentHeight % constants_1.FRAME_SIZE !== 0) {
            throw new Error(`Invalid standard animation range for '${animation.name}': yOffset=${currentOffset}, nextOffset=${nextOffset}.`);
        }
        areas.push({
            name: animation.name,
            left: 0,
            top: currentOffset,
            width: constants_1.SPRITESHEET_WIDTH,
            height: segmentHeight
        });
    }
    for (const customArea of character.customAnimations) {
        if (customArea.width <= 0 || customArea.height <= 0) {
            throw new Error(`Invalid custom animation area for '${customArea.name}': width=${customArea.width}, height=${customArea.height}.`);
        }
        areas.push({
            name: customArea.name,
            left: 0,
            top: customArea.yOffset,
            width: customArea.width,
            height: customArea.height
        });
    }
    return areas;
}
function validateActionAreaNames(areas) {
    const unique = new Set();
    for (const area of areas) {
        if (!/^[a-zA-Z0-9._-]+$/.test(area.name)) {
            throw new Error(`Action name '${area.name}' contains unsupported characters. Allowed: letters, digits, dot, underscore, dash.`);
        }
        if (unique.has(area.name)) {
            throw new Error(`Action export name collision detected for '${area.name}'.`);
        }
        unique.add(area.name);
    }
}
