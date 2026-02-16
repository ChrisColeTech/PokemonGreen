import Fastify from 'fastify'
import fastifyStatic from '@fastify/static'
import cors from '@fastify/cors'
import fs from 'fs'
import path from 'path'
import manifestRoutes from './routes/manifests.js'
import textureRoutes from './routes/textures.js'

const ASSETS_DIR = "D:/Projects/PokemonGreen/src/PokemonGreen.Assets/Pokemon3D"
const PORT = 3001

const app = Fastify({ logger: true, bodyLimit: 100 * 1024 * 1024 })

await app.register(cors, { origin: true })
await app.register(manifestRoutes, { assetsDir: path.resolve(ASSETS_DIR) })
await app.register(textureRoutes)

// Serve the entire Assets directory as static files under /assets/
// This lets Three.js loaders resolve texture paths naturally.
// e.g., /assets/Pokemon/pm0001_00_Rig/pm0001_00_Rig.fbx
//       /assets/Pokemon/pm0001_00_Rig/pm0001_00_Eye1.png
await app.register(fastifyStatic, {
  root: path.resolve(ASSETS_DIR),
  prefix: '/assets/',
})

// Keep the query-based endpoint as a fallback
app.get<{ Querystring: { dir: string; name: string } }>('/api/file', async (request, reply) => {
  const { dir, name } = request.query

  if (!dir || !name) {
    return reply.status(400).send({ error: 'Missing dir or name parameter' })
  }

  const fullPath = path.resolve(dir, name)

  if (!fullPath.startsWith(path.resolve(dir))) {
    return reply.status(403).send({ error: 'Forbidden' })
  }

  if (!fs.existsSync(fullPath)) {
    return reply.status(404).send({ error: `File not found: ${name}` })
  }

  const ext = path.extname(name).toLowerCase()
  const mime = mimeForExt(ext)

  const stream = fs.createReadStream(fullPath)
  return reply.type(mime).send(stream)
})

function mimeForExt(ext: string): string {
  switch (ext) {
    case '.png': return 'image/png'
    case '.jpg': case '.jpeg': return 'image/jpeg'
    case '.bmp': return 'image/bmp'
    case '.tga': return 'application/octet-stream'
    case '.fbx': return 'application/octet-stream'
    case '.dae': return 'text/xml'
    case '.obj': return 'text/plain'
    case '.mtl': return 'text/plain'
    default: return 'application/octet-stream'
  }
}

app.listen({ port: PORT }, (err) => {
  if (err) {
    app.log.error(err)
    process.exit(1)
  }
  console.log(`Backend listening on http://localhost:${PORT}`)
})
