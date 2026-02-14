const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const inputDir = path.resolve(__dirname, '..', 'Assets', 'Sprites');
const outputDir = path.resolve(__dirname, '..', 'src', 'PokemonGreen', 'Content', 'sprites');

fs.mkdirSync(outputDir, { recursive: true });

const files = fs.readdirSync(inputDir).filter(f => f.endsWith('.svg'));

(async () => {
    for (const file of files) {
        const name = path.basename(file, '.svg');
        const inputPath = path.join(inputDir, file);
        const outputPath = path.join(outputDir, `${name}.png`);

        try {
            await sharp(inputPath, { density: 72 })
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
