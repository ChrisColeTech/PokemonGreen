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
 * Load a scene from dropped files. Supports .dae, .obj, and .fbx.
 * Textures are matched by filename from the dropped file set.
 */
export async function loadSceneFromFiles(files: File[]): Promise<LoadResult> {
  const modelFile = files.find(f => /\.(dae|obj|fbx)$/i.test(f.name))
  if (!modelFile) throw new Error('No .dae, .obj, or .fbx file found.')

  // Map filename → blob URL for every image in the drop
  const blobUrls = new Map<string, string>()
  for (const f of files) {
    if (/\.(png|jpe?g|bmp|tga)$/i.test(f.name)) {
      blobUrls.set(f.name.toLowerCase(), URL.createObjectURL(f))
    }
  }

  console.log(`[SceneService] Loading ${modelFile.name} with ${blobUrls.size} texture(s): [${[...blobUrls.keys()].join(', ')}]`)

  // LoadingManager redirects texture requests to our blob URLs
  const manager = new THREE.LoadingManager()
  manager.setURLModifier(url => {
    const name = url.split('/').pop()?.split('\\').pop()?.split('?')[0]?.toLowerCase() ?? ''
    const resolved = blobUrls.get(name)
    console.log(`[SceneService] URL modifier: "${name}" → ${resolved ? 'FOUND' : 'MISS'} (input: ${url.substring(0, 80)})`)
    return resolved ?? url
  })

  const scene = await loadModel(modelFile, files, manager)

  // Wait a tick for any async texture loads, then fix up anything that failed
  await new Promise(r => setTimeout(r, 100))
  await fixupTextures(scene, blobUrls)

  return { scene, textures: extractTextures(scene) }
}

async function loadModel(
  modelFile: File,
  allFiles: File[],
  manager: THREE.LoadingManager,
): Promise<THREE.Group> {
  const name = modelFile.name.toLowerCase()

  if (name.endsWith('.fbx')) {
    const buffer = await modelFile.arrayBuffer()
    return new FBXLoader(manager).parse(buffer, '')
  }

  if (name.endsWith('.dae')) {
    const daeText = await modelFile.text()
    return new Promise((resolve, reject) => {
      const blob = new Blob([daeText], { type: 'text/xml' })
      const url = URL.createObjectURL(blob)
      new ColladaLoader(manager).load(
        url,
        col => { URL.revokeObjectURL(url); resolve(col.scene as THREE.Group) },
        undefined,
        err => { URL.revokeObjectURL(url); reject(err) },
      )
    })
  }

  // OBJ
  const objText = await modelFile.text()
  const mtlFile = allFiles.find(f => /\.mtl$/i.test(f.name))
  const objLoader = new OBJLoader(manager)

  if (mtlFile) {
    const mtlLoader = new MTLLoader(manager)
    const materials = mtlLoader.parse(await mtlFile.text(), '')
    materials.preload()
    objLoader.setMaterials(materials)
  }

  return objLoader.parse(objText)
}

/**
 * For any texture on the scene that has a null image, try loading it
 * from our blob URLs by matching the texture name.
 */
async function fixupTextures(
  scene: THREE.Group,
  blobUrls: Map<string, string>,
): Promise<void> {
  if (blobUrls.size === 0) return

  const promises: Promise<void>[] = []
  const seen = new Set<string>()

  scene.traverse(node => {
    if (!(node instanceof THREE.Mesh)) return
    const mats = Array.isArray(node.material) ? node.material : [node.material]
    for (const mat of mats) {
      if (!mat || !('map' in mat)) continue
      const tex = (mat as THREE.MeshPhongMaterial).map
      if (!tex || tex.image || seen.has(tex.uuid)) continue
      seen.add(tex.uuid)

      const texName = (tex.name || '').split('/').pop()?.split('\\').pop()?.toLowerCase() ?? ''
      const url = blobUrls.get(texName)
        || [...blobUrls.values()][seen.size - 1] // positional fallback
      if (!url) continue

      console.log(`[SceneService] Fixup: loading "${texName}" from blob`)
      promises.push(new Promise<void>(resolve => {
        const img = new Image()
        img.onload = () => { tex.image = img; tex.needsUpdate = true; resolve() }
        img.onerror = () => { console.warn(`[SceneService] Failed: ${texName}`); resolve() }
        img.src = url
      }))
    }
  })

  await Promise.all(promises)
}

function extractTextures(scene: THREE.Group): LoadedTexture[] {
  const textures: LoadedTexture[] = []
  const seen = new Set<string>()

  scene.traverse(node => {
    if (!(node instanceof THREE.Mesh)) return
    const mats = Array.isArray(node.material) ? node.material : [node.material]
    for (const mat of mats) {
      if (!mat || !('map' in mat)) continue
      const tex = (mat as THREE.MeshPhongMaterial).map
      if (!tex?.image || seen.has(tex.uuid)) continue
      seen.add(tex.uuid)

      const img = tex.image as HTMLImageElement
      const w = img.naturalWidth || img.width
      const h = img.naturalHeight || img.height
      const name = tex.name || img.src?.split('/').pop() || `texture_${textures.length}`

      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0)
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
