import * as THREE from 'three'
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js'
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js'
import { MTLLoader } from 'three/examples/jsm/loaders/MTLLoader.js'
import type { LoadedTexture } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'

export interface LoadResult {
  scene: THREE.Group
  textures: LoadedTexture[]
}

/**
 * Load a scene from a set of dropped files.
 * Supports .dae (Collada) and .obj (Wavefront) formats.
 * Textures (PNGs) are matched by filename from the dropped file set.
 */
export async function loadSceneFromFiles(files: File[]): Promise<LoadResult> {
  const modelFile = files.find(f =>
    f.name.endsWith('.dae') || f.name.endsWith('.obj')
  )
  const mtlFile = files.find(f => f.name.endsWith('.mtl'))

  if (!modelFile) {
    throw new Error('No .dae or .obj file found in the dropped files.')
  }

  console.log(`[SceneService] Loading model: ${modelFile.name}`)
  console.log(`[SceneService] Files provided:`, files.map(f => f.name))

  // Build blob URLs for texture files so Three.js can resolve them
  const textureUrls = new Map<string, string>()
  for (const f of files) {
    if (/\.(png|jpe?g|bmp|tga)$/i.test(f.name)) {
      const url = URL.createObjectURL(f)
      textureUrls.set(f.name.toLowerCase(), url)
      console.log(`[SceneService] Texture available: ${f.name.toLowerCase()} -> ${url}`)
    }
  }

  const modelText = await modelFile.text()
  let scene: THREE.Group

  if (modelFile.name.endsWith('.dae')) {
    scene = await loadCollada(modelText, textureUrls)
  } else {
    const mtlText = mtlFile ? await mtlFile.text() : undefined
    scene = await loadObj(modelText, mtlText, textureUrls)
  }

  const textures = extractTextures(scene)
  console.log(`[SceneService] Extracted ${textures.length} textures`)

  return { scene, textures }
}

function makeManager(textureUrls: Map<string, string>): THREE.LoadingManager {
  const manager = new THREE.LoadingManager()
  manager.setURLModifier((url: string) => {
    // Extract just the filename from whatever URL the loader constructs
    const filename = url.split('/').pop()?.split('?')[0]?.toLowerCase() ?? ''
    const resolved = textureUrls.get(filename)
    console.log(`[SceneService] URL modifier: "${filename}" -> ${resolved ? 'FOUND' : 'NOT FOUND'} (full URL: ${url.substring(0, 80)})`)
    return resolved ?? url
  })
  return manager
}

async function loadCollada(
  daeText: string,
  textureUrls: Map<string, string>,
): Promise<THREE.Group> {
  const loader = new ColladaLoader(makeManager(textureUrls))
  const blob = new Blob([daeText], { type: 'text/xml' })
  const blobUrl = URL.createObjectURL(blob)

  return new Promise((resolve, reject) => {
    loader.load(
      blobUrl,
      (collada) => {
        URL.revokeObjectURL(blobUrl)
        console.log('[SceneService] Collada loaded successfully')
        resolve(collada.scene as THREE.Group)
      },
      undefined,
      (err) => {
        URL.revokeObjectURL(blobUrl)
        console.error('[SceneService] Collada load failed:', err)
        reject(err)
      },
    )
  })
}

async function loadObj(
  objText: string,
  mtlText: string | undefined,
  textureUrls: Map<string, string>,
): Promise<THREE.Group> {
  const manager = makeManager(textureUrls)

  let materials: MTLLoader.MaterialCreator | undefined
  if (mtlText) {
    const mtlLoader = new MTLLoader(manager)
    materials = mtlLoader.parse(mtlText, '')
    materials.preload()
  }

  const objLoader = new OBJLoader(manager)
  if (materials) {
    objLoader.setMaterials(materials)
  }

  return objLoader.parse(objText)
}

function extractTextures(scene: THREE.Group): LoadedTexture[] {
  const textures: LoadedTexture[] = []
  const seen = new Set<string>()

  scene.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) return
    const mats = Array.isArray(node.material) ? node.material : [node.material]

    for (const mat of mats) {
      if (!mat || !('map' in mat)) continue
      const tex = (mat as THREE.MeshPhongMaterial).map
      if (!tex) {
        console.log(`[SceneService] Material has null map:`, mat.name || mat.type)
        continue
      }
      if (!tex.image) {
        console.log(`[SceneService] Texture has null image:`, tex.name || tex.uuid)
        continue
      }

      const key = tex.uuid
      if (seen.has(key)) continue
      seen.add(key)

      const img = tex.image as HTMLImageElement
      const w = img.naturalWidth || img.width
      const h = img.naturalHeight || img.height
      const name = tex.name || img.src?.split('/').pop() || `texture_${textures.length}`

      console.log(`[SceneService] Extracting texture: ${name} (${w}x${h}), src: ${img.src?.substring(0, 60) || 'none'}`)

      // Capture original as data URL
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      const ctx = canvas.getContext('2d')!
      ctx.drawImage(img, 0, 0)
      const dataUrl = canvas.toDataURL('image/png')

      textures.push({
        name,
        originalImage: img,
        originalDataUrl: dataUrl,
        modifiedDataUrl: dataUrl,
        threeTexture: tex,
        adjustment: { ...DEFAULT_ADJUSTMENT },
      })
    }
  })

  return textures
}
