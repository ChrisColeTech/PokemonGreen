"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.STANDARD_ANIMATIONS = exports.SPRITESHEET_HEIGHT = exports.SPRITESHEET_WIDTH = exports.FRAME_SIZE = void 0;
exports.FRAME_SIZE = 64;
exports.SPRITESHEET_WIDTH = 13 * exports.FRAME_SIZE;
exports.SPRITESHEET_HEIGHT = 54 * exports.FRAME_SIZE;
exports.STANDARD_ANIMATIONS = [
    { name: "spellcast", folderName: "spellcast", yOffset: 0 },
    { name: "thrust", folderName: "thrust", yOffset: 4 * exports.FRAME_SIZE },
    { name: "walk", folderName: "walk", yOffset: 8 * exports.FRAME_SIZE },
    { name: "slash", folderName: "slash", yOffset: 12 * exports.FRAME_SIZE },
    { name: "shoot", folderName: "shoot", yOffset: 16 * exports.FRAME_SIZE },
    { name: "hurt", folderName: "hurt", yOffset: 20 * exports.FRAME_SIZE },
    { name: "climb", folderName: "climb", yOffset: 21 * exports.FRAME_SIZE },
    { name: "idle", folderName: "idle", yOffset: 22 * exports.FRAME_SIZE },
    { name: "jump", folderName: "jump", yOffset: 26 * exports.FRAME_SIZE },
    { name: "sit", folderName: "sit", yOffset: 30 * exports.FRAME_SIZE },
    { name: "emote", folderName: "emote", yOffset: 34 * exports.FRAME_SIZE },
    { name: "run", folderName: "run", yOffset: 38 * exports.FRAME_SIZE },
    { name: "combat_idle", folderName: "combat_idle", yOffset: 42 * exports.FRAME_SIZE },
    { name: "backslash", folderName: "backslash", yOffset: 46 * exports.FRAME_SIZE },
    { name: "halfslash", folderName: "halfslash", yOffset: 50 * exports.FRAME_SIZE }
];
