"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.collectCredits = collectCredits;
exports.creditsToCsv = creditsToCsv;
exports.creditsToTxt = creditsToTxt;
function collectCredits(character, metadata) {
    const usedPaths = buildUsedPathSet(character);
    const seen = new Set();
    const results = [];
    for (const selection of character.selections) {
        const item = metadata.itemsById[selection.itemId];
        if (!item) {
            continue;
        }
        for (const credit of item.credits) {
            const matchedPath = findMatchingUsedPath(credit, usedPaths);
            if (!matchedPath || seen.has(matchedPath)) {
                continue;
            }
            results.push({
                fileName: matchedPath,
                notes: typeof credit.notes === "string" ? credit.notes : "",
                authors: normalizeStringArray(credit.authors),
                licenses: normalizeStringArray(credit.licenses),
                urls: normalizeStringArray(credit.urls)
            });
            seen.add(matchedPath);
        }
    }
    results.sort((a, b) => a.fileName.localeCompare(b.fileName));
    return results;
}
function creditsToCsv(credits) {
    const lines = ["filename,notes,authors,licenses,urls"];
    for (const credit of credits) {
        lines.push([
            credit.fileName,
            credit.notes,
            credit.authors.join(", "),
            credit.licenses.join(", "),
            credit.urls.join(", ")
        ]
            .map((value) => `"${escapeCsv(value)}"`)
            .join(","));
    }
    return `${lines.join("\n")}\n`;
}
function creditsToTxt(credits) {
    let output = "";
    for (const credit of credits) {
        output += `${credit.fileName}\n`;
        if (credit.notes.length > 0) {
            output += `\t- Note: ${credit.notes}\n`;
        }
        output += `\t- Licenses:\n${formatList(credit.licenses)}\n`;
        output += `\t- Authors:\n${formatList(credit.authors)}\n`;
        output += `\t- Links:\n${formatList(credit.urls)}\n\n`;
    }
    return output;
}
function buildUsedPathSet(character) {
    const used = new Set();
    for (const layer of character.layers) {
        for (const animation of layer.animations) {
            used.add(stripSpritesheetPrefix(animation.relativePath));
        }
    }
    for (const layer of character.customLayers) {
        used.add(stripSpritesheetPrefix(layer.relativePath));
    }
    return Array.from(used).sort((a, b) => a.localeCompare(b));
}
function stripSpritesheetPrefix(value) {
    const prefix = "spritesheets/";
    if (value.startsWith(prefix)) {
        return value.slice(prefix.length);
    }
    return value;
}
function findMatchingUsedPath(credit, usedPaths) {
    const creditFile = typeof credit.file === "string" ? credit.file : "";
    if (creditFile.length === 0) {
        return null;
    }
    for (const usedPath of usedPaths) {
        if (usedPath === creditFile || usedPath.startsWith(`${creditFile}/`)) {
            return usedPath;
        }
    }
    return null;
}
function normalizeStringArray(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value.filter((entry) => typeof entry === "string");
}
function formatList(entries) {
    if (entries.length === 0) {
        return "\t\t- n/a";
    }
    return `\t\t- ${entries.join("\n\t\t- ")}`;
}
function escapeCsv(value) {
    return value.replaceAll('"', '""');
}
