import fs from 'fs'
import path from 'path'

const ASSETS_DIR = 'D:/Projects/PokemonGreen/src/PokemonGreen.Assets/Pokemon3D'
const MODEL_EXTS = ['.fbx', '.dae', '.obj']
const TEXTURE_EXTS = ['.png', '.jpg', '.jpeg', '.bmp', '.tga']

interface Manifest {
  name: string
  dir: string
  assetsPath: string  // relative path from Assets root (e.g., "Pokemon/pm0001_00_Rig")
  modelFile: string
  modelFormat: string
  textures: string[]
  mtlFile?: string
}

function isModel(file: string): boolean {
  return MODEL_EXTS.includes(path.extname(file).toLowerCase())
}

function isTexture(file: string): boolean {
  return TEXTURE_EXTS.includes(path.extname(file).toLowerCase())
}

function processFolder(folderPath: string): number {
  const entries = fs.readdirSync(folderPath)
  const files = entries.filter(e => fs.statSync(path.join(folderPath, e)).isFile())
  const dirs = entries.filter(e => fs.statSync(path.join(folderPath, e)).isDirectory())
  let count = 0

  const modelFile = files.find(f => isModel(f))
  if (modelFile) {
    const textureFiles = files.filter(f => isTexture(f))
    const ext = path.extname(modelFile).toLowerCase().slice(1)
    const mtlFile = files.find(f => path.extname(f).toLowerCase() === '.mtl')

    const manifest: Manifest = {
      name: path.basename(folderPath),
      dir: folderPath.replace(/\\/g, '/'),
      assetsPath: path.relative(ASSETS_DIR, folderPath).replace(/\\/g, '/'),
      modelFile,
      modelFormat: ext,
      textures: textureFiles,
    }
    if (mtlFile) {
      manifest.mtlFile = mtlFile
    }

    const outPath = path.join(folderPath, 'manifest.json')
    fs.writeFileSync(outPath, JSON.stringify(manifest, null, 2))
    console.log(`  ${path.relative(ASSETS_DIR, outPath)} (${textureFiles.length} textures)`)
    count++
  }

  for (const dir of dirs) {
    count += processFolder(path.join(folderPath, dir))
  }
  return count
}

console.log(`Scanning: ${ASSETS_DIR}`)
if (!fs.existsSync(ASSETS_DIR)) {
  console.error(`Assets directory not found: ${ASSETS_DIR}`)
  process.exit(1)
}

const count = processFolder(ASSETS_DIR)
console.log(`\nGenerated ${count} manifest files.`)
