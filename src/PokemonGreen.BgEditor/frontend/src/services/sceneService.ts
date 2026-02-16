import * as THREE from 'three'
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js'
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js'
import { MTLLoader } from 'three/examples/jsm/loaders/MTLLoader.js'
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js'
import type { LoadedTexture } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'
import { parseColladaAnimations } from './colladaAnimationParser'

const API_BASE = 'http://localhost:3001'

export interface LoadResult {
  scene: THREE.Group
  textures: LoadedTexture[]
  animations: THREE.AnimationClip[]
}

export interface Manifest {
  name: string
  dir: string
  assetsPath: string
  modelFile: string
  modelFormat: string
  textures: string[]
  mtlFile?: string
}

/**
 * Load a scene from a manifest.
 * Uses a LoadingManager so FBXLoader waits for all textures to finish loading,
 * then converts materials to MeshBasicMaterial for reliable rendering.
 */
export async function loadScene(manifest: Manifest): Promise<LoadResult> {
  console.log(`[SceneService] Loading: ${manifest.name} (${manifest.modelFormat}, ${manifest.textures.length} textures)`)

  const baseUrl = `${API_BASE}/assets/${manifest.assetsPath}/`
  const modelUrl = `${baseUrl}${manifest.modelFile}`
  console.log(`[SceneService] Model URL: ${modelUrl}`)

  const fmt = manifest.modelFormat
  let scene: THREE.Group
  let animations: THREE.AnimationClip[] = []

  if (fmt === 'fbx') {
    scene = await loadFbxWithManager(modelUrl)
    animations = scene.animations || []
  } else if (fmt === 'dae') {
    const collada = await loadDaeWithManager(modelUrl)
    scene = collada.scene
    // The ColladaLoader's built-in animation parser doesn't handle per-axis
    // rotation/translation channels (rotation.X, rotation.Y, rotation.Z, etc.)
    // which are used by these Pokemon DAE files. It only handles 'matrix' type
    // animations. So we parse the raw XML ourselves.
    animations = collada.animations
    console.log(`[SceneService] DAE loaded: ${animations.length} clip(s) from ColladaLoader`)
    if (animations.length === 0 || (animations.length === 1 && animations[0].tracks.length === 0)) {
      console.log('[SceneService] ColladaLoader returned no/empty animations, trying custom parser...')
      try {
        const customAnims = await parseColladaAnimations(modelUrl, scene)
        if (customAnims.length > 0) {
          animations = customAnims
          console.log(`[SceneService] Custom parser found ${animations.length} clip(s)`)
        }
      } catch (err) {
        console.warn('[SceneService] Custom animation parser failed:', err)
      }
    }
    // NOTE: UV Y-flip is already applied by the DAE exporter (1-v in the UV data).
    // Do NOT flip again here — that would undo the exporter's correction.
  } else {
    const objLoader = new OBJLoader()
    if (manifest.mtlFile) {
      const mtlUrl = `${baseUrl}${manifest.mtlFile}`
      const materials = await loadWithPromise(new MTLLoader(), mtlUrl)
      ;(materials as MTLLoader.MaterialCreator).preload()
      objLoader.setMaterials(materials as MTLLoader.MaterialCreator)
    }
    scene = await loadWithPromise(objLoader, modelUrl)
  }

  // Convert MeshPhongMaterial → MeshBasicMaterial, keeping whatever the loader assigned.
  fixMaterials(scene)

  // Extract textures from the scene AND load any manifest textures the FBX didn't use.
  // The FBX files often only embed one texture (e.g., body) while the eye texture
  // exists as a separate file but isn't connected to any material in the FBX.
  const textures = extractTextures(scene)
  await loadExtraManifestTextures(textures, scene, manifest)

  return { scene, textures, animations }
}

/**
 * Load FBX using a LoadingManager that waits for all sub-resources.
 * The manager's onLoad fires once the FBX AND all its textures are loaded.
 */
function loadFbxWithManager(modelUrl: string): Promise<THREE.Group> {
  return new Promise((resolve, reject) => {
    const manager = new THREE.LoadingManager()
    const loader = new FBXLoader(manager)

    let fbxScene: THREE.Group | null = null
    let managerDone = false

    function tryResolve() {
      if (fbxScene && managerDone) {
        resolve(fbxScene)
      }
    }

    manager.onStart = (url) => {
      console.log(`[SceneService] LoadingManager: started loading ${url}`)
    }

    manager.onLoad = () => {
      console.log('[SceneService] LoadingManager: all resources loaded')
      managerDone = true
      tryResolve()
    }

    manager.onError = (url) => {
      console.warn(`[SceneService] LoadingManager: failed to load ${url}`)
    }

    loader.load(
      modelUrl,
      (group) => {
        console.log('[SceneService] FBXLoader: model parsed')
        fbxScene = group
        // If manager already finished (e.g., no sub-resources), resolve now
        tryResolve()
      },
      undefined,
      (err) => reject(err),
    )

    // Safety timeout: if manager never fires onLoad (e.g., texture errors),
    // resolve after 5 seconds with whatever we have.
    setTimeout(() => {
      if (!managerDone && fbxScene) {
        console.warn('[SceneService] LoadingManager timeout — resolving with partial textures')
        managerDone = true
        tryResolve()
      }
    }, 5000)
  })
}

/**
 * Load DAE (Collada) using a LoadingManager that waits for all sub-resources
 * (textures) to finish loading before resolving.
 */
interface ColladaResult {
  scene: THREE.Group
  animations: THREE.AnimationClip[]
}

function loadDaeWithManager(modelUrl: string): Promise<ColladaResult> {
  return new Promise((resolve, reject) => {
    const manager = new THREE.LoadingManager()
    const loader = new ColladaLoader(manager)
    loader.setCrossOrigin('anonymous')

    let colladaResult: ColladaResult | null = null
    let managerDone = false

    function tryResolve() {
      if (colladaResult && managerDone) {
        resolve(colladaResult)
      }
    }

    manager.onStart = (url) => {
      console.log(`[SceneService] DAE LoadingManager: started loading ${url}`)
    }

    manager.onLoad = () => {
      console.log('[SceneService] DAE LoadingManager: all resources loaded')
      managerDone = true
      tryResolve()
    }

    manager.onError = (url) => {
      console.warn(`[SceneService] DAE LoadingManager: failed to load ${url}`)
    }

    loader.load(
      modelUrl,
      (collada) => {
        console.log('[SceneService] ColladaLoader: model parsed')
        // Animations may be on the Collada result or on the scene
        const anims = (collada as any).animations || collada.scene.animations || []
        colladaResult = {
          scene: collada.scene as unknown as THREE.Group,
          animations: anims,
        }
        // If manager already finished (no sub-resources), resolve now
        tryResolve()
      },
      undefined,
      (err) => reject(err),
    )

    // Safety timeout
    setTimeout(() => {
      if (!managerDone && colladaResult) {
        console.warn('[SceneService] DAE LoadingManager timeout — resolving with partial textures')
        managerDone = true
        tryResolve()
      }
    }, 5000)
  })
}

/**
 * Load any manifest textures that the FBX didn't use and add them to the
 * textures array for the sidebar. These are available for manual assignment
 * but aren't auto-assigned to meshes (since the FBX doesn't tell us which
 * faces should use them).
 */
async function loadExtraManifestTextures(
  textures: LoadedTexture[],
  scene: THREE.Group,
  manifest: Manifest,
): Promise<void> {
  // Collect texture filenames already used by the FBX loader
  const usedTextures = new Set<string>()
  scene.traverse(node => {
    if (!(node instanceof THREE.Mesh)) return
    const mats = Array.isArray(node.material) ? node.material : [node.material]
    for (const mat of mats) {
      const tex = (mat as THREE.MeshBasicMaterial).map
      if (tex?.name) usedTextures.add(tex.name.toLowerCase())
    }
  })

  const unusedTexNames = manifest.textures.filter(t => !usedTextures.has(t.toLowerCase()))
  if (unusedTexNames.length === 0) return

  console.log(`[SceneService] Loading extra manifest textures: ${unusedTexNames.join(', ')}`)

  const baseUrl = `${API_BASE}/assets/${manifest.assetsPath}/`
  for (const texName of unusedTexNames) {
    try {
      const img = await loadImage(`${baseUrl}${texName}`)
      const tex = new THREE.Texture(img)
      tex.colorSpace = THREE.SRGBColorSpace
      tex.name = texName
      tex.needsUpdate = true

      const canvas = document.createElement('canvas')
      canvas.width = img.naturalWidth
      canvas.height = img.naturalHeight
      canvas.getContext('2d')!.drawImage(img, 0, 0)
      const dataUrl = canvas.toDataURL('image/png')

      textures.push({
        name: texName,
        originalImage: img,
        originalDataUrl: dataUrl,
        modifiedDataUrl: dataUrl,
        threeTexture: tex,
        adjustment: { ...DEFAULT_ADJUSTMENT },
      })
      console.log(`[SceneService] Added extra texture: ${texName} (${img.naturalWidth}x${img.naturalHeight})`)
    } catch (e) {
      console.warn(`[SceneService] Failed to load: ${texName}`, e)
    }
  }
}

/**
 * Load an image from the backend.
 */
function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.crossOrigin = 'anonymous'
    img.onload = () => resolve(img)
    img.onerror = () => reject(new Error(`Failed to load: ${url}`))
    img.src = url
  })
}

/**
 * Convert all mesh materials to MeshBasicMaterial, keeping the loader's texture assignments.
 * MeshPhongMaterial renders black in our setup, MeshBasicMaterial works reliably.
 */
function fixMaterials(scene: THREE.Group): void {
  scene.traverse(node => {
    if (!(node instanceof THREE.Mesh)) return

    // Debug: check geometry groups (multi-material face ranges)
    const groups = node.geometry?.groups
    const mats = Array.isArray(node.material) ? node.material : [node.material]
    console.log(`[SceneService] Mesh "${node.name}": ${mats.length} material(s), ${groups?.length ?? 0} geometry group(s)`)
    if (groups?.length) {
      groups.forEach((g: { materialIndex: number; start: number; count: number }, i: number) => console.log(`[SceneService]   group[${i}]: materialIndex=${g.materialIndex}, start=${g.start}, count=${g.count}`))
    }

    const newMats = mats.map((mat, i) => {
      const tex = (mat as THREE.MeshPhongMaterial).map
      if (tex) {
        tex.colorSpace = THREE.SRGBColorSpace
        tex.needsUpdate = true
      }
      const basic = new THREE.MeshBasicMaterial({
        map: tex || null,
        side: THREE.DoubleSide,
      })
      basic.name = mat.name
      console.log(`[SceneService]   mat[${i}] "${mat.name}" → texture: ${tex?.name || 'none'}, image: ${tex?.image ? 'OK' : 'pending'}`)
      return basic
    })
    node.material = newMats.length === 1 ? newMats[0] : newMats
  })
}

/**
 * Wrap any Three.js loader's load() method in a Promise.
 */
function loadWithPromise<T>(loader: { load: (url: string, onLoad: (result: T) => void, onProgress?: (e: ProgressEvent) => void, onError?: (e: unknown) => void) => void }, url: string): Promise<T> {
  return new Promise((resolve, reject) => {
    loader.load(url, resolve, undefined, reject)
  })
}

function extractTextures(scene: THREE.Group): LoadedTexture[] {
  const textures: LoadedTexture[] = []
  const seen = new Set<string>()

  scene.traverse(node => {
    if (!(node instanceof THREE.Mesh)) return
    const mats = Array.isArray(node.material) ? node.material : [node.material]
    for (const mat of mats) {
      if (!mat || !('map' in mat)) continue
      const tex = (mat as THREE.MeshBasicMaterial).map
      if (!tex?.image || seen.has(tex.uuid)) continue
      seen.add(tex.uuid)

      const img = tex.image as (HTMLImageElement | ImageBitmap)
      const w = img.width
      const h = img.height
      const name = tex.name || ('src' in img ? img.src?.split('/').pop() : null) || `texture_${textures.length}`

      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0)
      const dataUrl = canvas.toDataURL('image/png')

      textures.push({
        name,
        originalImage: img as HTMLImageElement,
        originalDataUrl: dataUrl,
        modifiedDataUrl: dataUrl,
        threeTexture: tex,
        adjustment: { ...DEFAULT_ADJUSTMENT },
      })
    }
  })

  return textures
}
