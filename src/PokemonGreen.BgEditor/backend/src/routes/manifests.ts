import { FastifyInstance } from 'fastify'
import fs from 'fs'
import path from 'path'

const MODEL_EXTS = ['.fbx', '.dae', '.obj']
const TEXTURE_EXTS = ['.png', '.jpg', '.jpeg', '.bmp', '.tga']

interface Manifest {
  name: string
  dir: string
  assetsPath: string
  modelFile: string
  modelFormat: string
  textures: string[]
  mtlFile?: string
}

interface GenerateBody {
  inputDir: string
  outputDir?: string // defaults to inputDir (in-place)
  formats?: string[] // e.g. ["fbx","dae","obj"] — defaults to all
  overwrite?: boolean // overwrite existing manifests — defaults to true
}

function isModel(file: string, formats?: string[]): boolean {
  const ext = path.extname(file).toLowerCase()
  if (formats && formats.length > 0) {
    return formats.some(f => ext === `.${f.toLowerCase()}`)
  }
  return MODEL_EXTS.includes(ext)
}

function isTexture(file: string): boolean {
  return TEXTURE_EXTS.includes(path.extname(file).toLowerCase())
}

function generateManifestForFolder(
  folderPath: string,
  assetsDir: string,
  formats?: string[],
): Manifest | null {
  const entries = fs.readdirSync(folderPath)
  const files = entries.filter(e => fs.statSync(path.join(folderPath, e)).isFile())

  const modelFile = files.find(f => isModel(f, formats))
  if (!modelFile) return null

  const textureFiles = files.filter(f => isTexture(f))
  const ext = path.extname(modelFile).toLowerCase().slice(1)
  const mtlFile = files.find(f => path.extname(f).toLowerCase() === '.mtl')

  const manifest: Manifest = {
    name: path.basename(folderPath),
    dir: folderPath.replace(/\\/g, '/'),
    assetsPath: path.relative(assetsDir, folderPath).replace(/\\/g, '/'),
    modelFile,
    modelFormat: ext,
    textures: textureFiles,
  }
  if (mtlFile) {
    manifest.mtlFile = mtlFile
  }
  return manifest
}

function scanAndGenerate(
  folderPath: string,
  assetsDir: string,
  outputDir: string,
  formats?: string[],
  overwrite?: boolean,
): string[] {
  const generated: string[] = []
  const entries = fs.readdirSync(folderPath)
  const dirs = entries.filter(e => fs.statSync(path.join(folderPath, e)).isDirectory())

  const manifest = generateManifestForFolder(folderPath, assetsDir, formats)
  if (manifest) {
    // Compute output path — mirror folder structure under outputDir
    const rel = path.relative(assetsDir, folderPath)
    const outFolder = outputDir === assetsDir ? folderPath : path.join(outputDir, rel)
    const outPath = path.join(outFolder, 'manifest.json')

    if (!overwrite && fs.existsSync(outPath)) {
      // skip
    } else {
      if (outFolder !== folderPath) {
        fs.mkdirSync(outFolder, { recursive: true })
      }
      // Update dir in manifest to point to the output location
      if (outputDir !== assetsDir) {
        manifest.dir = outFolder.replace(/\\/g, '/')
        manifest.assetsPath = path.relative(outputDir, outFolder).replace(/\\/g, '/')
      }
      fs.writeFileSync(outPath, JSON.stringify(manifest, null, 2))
      generated.push(manifest.assetsPath || manifest.name)
    }
  }

  for (const dir of dirs) {
    generated.push(...scanAndGenerate(path.join(folderPath, dir), assetsDir, outputDir, formats, overwrite))
  }
  return generated
}

function collectManifests(folderPath: string): Manifest[] {
  const manifests: Manifest[] = []
  let entries: string[]
  try { entries = fs.readdirSync(folderPath) } catch { return manifests }

  const manifestFile = entries.find(e => e === 'manifest.json')
  if (manifestFile) {
    try {
      const content = fs.readFileSync(path.join(folderPath, 'manifest.json'), 'utf-8')
      manifests.push(JSON.parse(content))
    } catch { /* skip malformed */ }
  }

  const dirs = entries.filter(e => {
    try { return fs.statSync(path.join(folderPath, e)).isDirectory() }
    catch { return false }
  })

  for (const dir of dirs) {
    manifests.push(...collectManifests(path.join(folderPath, dir)))
  }
  return manifests
}

export default async function manifestRoutes(app: FastifyInstance, opts: { assetsDir: string }) {
  const { assetsDir } = opts

  // Get current config defaults
  app.get('/api/manifests/config', async () => {
    return {
      defaultInputDir: assetsDir.replace(/\\/g, '/'),
      defaultOutputDir: assetsDir.replace(/\\/g, '/'),
      supportedFormats: MODEL_EXTS.map(e => e.slice(1)),
    }
  })

  app.post<{ Body: GenerateBody }>('/api/manifests/generate', async (request) => {
    const { inputDir, outputDir, formats, overwrite } = request.body || {} as GenerateBody
    const scanDir = inputDir || assetsDir
    const outDir = outputDir || scanDir
    const doOverwrite = overwrite !== false

    if (!fs.existsSync(scanDir)) {
      return { error: `Input directory not found: ${scanDir}`, generated: 0, folders: [] }
    }
    if (outDir !== scanDir && !fs.existsSync(outDir)) {
      fs.mkdirSync(outDir, { recursive: true })
    }

    const folders = scanAndGenerate(scanDir, scanDir, outDir, formats, doOverwrite)
    return { generated: folders.length, folders }
  })

  app.get<{ Querystring: { dir?: string } }>('/api/manifests', async (request) => {
    const dir = request.query.dir || assetsDir
    if (!fs.existsSync(dir)) {
      return []
    }
    return collectManifests(dir)
  })
}
