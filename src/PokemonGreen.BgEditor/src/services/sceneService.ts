import * as THREE from 'three'
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js'
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js'
import { MTLLoader } from 'three/examples/jsm/loaders/MTLLoader.js'
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js'
import type { LoadedTexture } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'

export interface LoadResult {
  scene: THREE.Group
  textures: LoadedTexture[]
}

/**
 * Load a scene from a set of dropped files.
 * Supports .dae (Collada), .obj (Wavefront), and .fbx formats.
 * Textures (PNGs) are matched by filename from the dropped file set.
 */
export async function loadSceneFromFiles(files: File[]): Promise<LoadResult> {
  const modelFile = files.find(f =>
    /\.(dae|obj|fbx)$/i.test(f.name)
  )

  if (!modelFile) {
    throw new Error('No .dae, .obj, or .fbx file found in the dropped files.')
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

  let scene: THREE.Group
  const nameLower = modelFile.name.toLowerCase()

  if (nameLower.endsWith('.fbx')) {
    const modelBuffer = await modelFile.arrayBuffer()
    scene = await loadFbx(modelBuffer, textureUrls)
  } else if (nameLower.endsWith('.dae')) {
    const modelText = await modelFile.text()
    scene = await loadCollada(modelText, textureUrls)
  } else {
    const modelText = await modelFile.text()
    // Find MTL: first try matching the mtllib reference in the OBJ, then fall back to any .mtl
    const mtlFile = findMtlFile(modelText, files)
    const mtlText = mtlFile ? await mtlFile.text() : undefined
    if (!mtlText) {
      console.warn('[SceneService] No .mtl file found — OBJ will have no textures. Drop the .mtl alongside the .obj.')
    } else {
      console.log(`[SceneService] Using MTL: ${mtlFile!.name}`)
    }
    scene = await loadObj(modelText, mtlText, textureUrls)
  }

  const textures = extractTextures(scene)
  console.log(`[SceneService] Extracted ${textures.length} textures`)

  return { scene, textures }
}

/**
 * Find the MTL file for an OBJ. First tries to match the `mtllib` directive
 * inside the OBJ text, then falls back to any .mtl file in the dropped set.
 */
function findMtlFile(objText: string, files: File[]): File | undefined {
  // Parse `mtllib filename.mtl` from the OBJ text
  const match = objText.match(/^mtllib\s+(.+)$/m)
  if (match) {
    const mtlName = match[1].trim().toLowerCase()
    console.log(`[SceneService] OBJ references mtllib: "${match[1].trim()}"`)
    const byName = files.find(f => f.name.toLowerCase() === mtlName)
    if (byName) return byName
    // Try matching just the filename part (strip paths)
    const baseName = mtlName.split('/').pop()!.split('\\').pop()!
    const byBase = files.find(f => f.name.toLowerCase() === baseName)
    if (byBase) return byBase
  }
  // Fallback: any .mtl file
  return files.find(f => /\.mtl$/i.test(f.name))
}

/**
 * Create a LoadingManager that redirects texture URLs to blob URLs
 * from dropped files, and tracks whether any items were started.
 */
function makeTrackedManager(textureUrls: Map<string, string>) {
  const manager = new THREE.LoadingManager()
  let itemCount = 0

  const origItemStart = manager.itemStart.bind(manager)
  manager.itemStart = (url: string) => {
    itemCount++
    origItemStart(url)
  }

  manager.setURLModifier((url: string) => {
    const filename = url.split('/').pop()?.split('?')[0]?.toLowerCase() ?? ''
    const resolved = textureUrls.get(filename)
    console.log(`[SceneService] URL modifier: "${filename}" -> ${resolved ? 'FOUND' : 'NOT FOUND'} (full URL: ${url.substring(0, 80)})`)
    return resolved ?? url
  })

  manager.onError = (url: string) => {
    console.error('[SceneService] Failed to load resource:', url)
  }

  return { manager, hasItems: () => itemCount > 0 }
}

/**
 * Wait for all resources tracked by the manager to finish loading.
 * Resolves immediately if no items were ever started.
 */
function waitForManager(
  manager: THREE.LoadingManager,
  hasItems: () => boolean,
): Promise<void> {
  return new Promise((resolve) => {
    manager.onLoad = () => {
      console.log('[SceneService] All resources loaded (including textures)')
      resolve()
    }
    // If nothing was queued after the initial load, onLoad already fired
    // or won't fire. Use queueMicrotask to check after the current call
    // stack unwinds (after parse/load callbacks finish).
    queueMicrotask(() => {
      if (!hasItems()) {
        console.log('[SceneService] No resources were queued, resolving')
        resolve()
      }
    })
  })
}

async function loadCollada(
  daeText: string,
  textureUrls: Map<string, string>,
): Promise<THREE.Group> {
  const { manager, hasItems } = makeTrackedManager(textureUrls)
  const loader = new ColladaLoader(manager)
  const blob = new Blob([daeText], { type: 'text/xml' })
  const blobUrl = URL.createObjectURL(blob)

  return new Promise((resolve, reject) => {
    let scene: THREE.Group | null = null

    loader.load(
      blobUrl,
      (collada) => {
        URL.revokeObjectURL(blobUrl)
        console.log('[SceneService] Collada parsed')
        scene = collada.scene as THREE.Group
      },
      undefined,
      (err) => {
        URL.revokeObjectURL(blobUrl)
        console.error('[SceneService] Collada load failed:', err)
        reject(err)
      },
    )

    // manager.onLoad fires after ALL tracked items (DAE fetch + textures)
    // are done. The ColladaLoader's own callback fires after DAE parsing,
    // but textures started by TextureLoader are still pending.
    waitForManager(manager, hasItems).then(() => {
      if (scene) resolve(scene)
    })
  })
}

async function loadObj(
  objText: string,
  mtlText: string | undefined,
  textureUrls: Map<string, string>,
): Promise<THREE.Group> {
  const { manager, hasItems } = makeTrackedManager(textureUrls)

  let materials: MTLLoader.MaterialCreator | undefined
  if (mtlText) {
    const mtlLoader = new MTLLoader(manager)
    materials = mtlLoader.parse(mtlText, '')
    const matNames = Object.keys(materials.materialsInfo)
    console.log(`[SceneService] MTL defines ${matNames.length} materials:`, matNames)
    materials.preload()
  }

  const objLoader = new OBJLoader(manager)
  if (materials) {
    objLoader.setMaterials(materials)
  }

  const scene = objLoader.parse(objText)
  // Log what materials ended up on the meshes
  scene.traverse((node) => {
    if (node instanceof THREE.Mesh) {
      const mats = Array.isArray(node.material) ? node.material : [node.material]
      for (const m of mats) {
        const hasMap = m && 'map' in m && (m as THREE.MeshPhongMaterial).map
        console.log(`[SceneService] OBJ mesh "${node.name}": material "${m?.name || m?.type}", hasMap: ${!!hasMap}`)
      }
    }
  })

  // materials.preload() starts async texture loads via the manager.
  // Wait for all pending loads to finish before returning.
  await waitForManager(manager, hasItems)
  return scene
}

async function loadFbx(
  buffer: ArrayBuffer,
  textureUrls: Map<string, string>,
): Promise<THREE.Group> {
  const { manager, hasItems } = makeTrackedManager(textureUrls)
  const loader = new FBXLoader(manager)
  const scene = loader.parse(buffer, '')

  // FBX embeds texture references that trigger async loads via the manager.
  // Wait for all pending loads to finish before returning.
  await waitForManager(manager, hasItems)
  return scene
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
