const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const inputDir = path.resolve(__dirname, '..', 'Assets', 'Sprites');
const outputDir = path.resolve(__dirname, '..', 'src', 'PokemonGreen.Assets', 'Sprites');

fs.mkdirSync(outputDir, { recursive: true });

// Skip v2 files — their blades are now baked into the main SVGs
const files = fs.readdirSync(inputDir)
    .filter(f => f.endsWith('.svg') && !f.includes('_v2'));

(async () => {
    for (const file of files) {
        const name = path.basename(file, '.svg');
        const inputPath = path.join(inputDir, file);
        const outputPath = path.join(outputDir, `${name}.png`);

        try {
            await sharp(inputPath, { density: 144 })
                .png()
                .toFile(outputPath);

            const meta = await sharp(outputPath).metadata();
            console.log(`${name}.png  ${meta.width}x${meta.height}`);
        } catch (err) {
            console.error(`FAIL ${file}: ${err.message}`);
        }
    }
    console.log(`\nConverted ${files.length} SVGs to ${outputDir}`);
})();
