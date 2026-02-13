import { buildJsonExportPayload } from '../mapSerializer'
import { parseMapJsonPayload } from '../mapParser'
import { describe, expect, it } from 'vitest'
import type { ValidMapData } from '../mapSchema'

const assertParseSuccess = (result: ReturnType<typeof parseMapJsonPayload>): ValidMapData => {
  expect(result.ok).toBe(true)
  if (!result.ok) {
    throw new Error(result.error)
  }
  return result.data
}

describe('map parser and serializer', () => {
  it('round-trips v2 payloads from serializer output', () => {
    const sourceMap: ValidMapData = {
      displayName: 'Round Trip Test',
      width: 5,
      height: 5,
      tiles: [
        [1, 1, 2, 3, 1],
        [1, 7, 2, 1, 1],
        [1, 1, 8, 1, 1],
        [1, 1, 2, 1, 19],
        [1, 1, 1, 1, 1],
      ],
    }

    const payload = buildJsonExportPayload(sourceMap)
    const parsed = assertParseSuccess(parseMapJsonPayload(JSON.stringify(payload)))

    expect(payload.schemaVersion).toBe(2)
    expect(parsed.width).toBe(sourceMap.width)
    expect(parsed.height).toBe(sourceMap.height)
    expect(parsed.tiles).toEqual(sourceMap.tiles)
    expect(parsed.displayName).toBe('Map')
  })

  it('parses legacy v1 payloads and upgrades cleanly through v2 serialization', () => {
    const legacyPayload = {
      schemaVersion: 1,
      width: 5,
      height: 5,
      displayName: ' Legacy Route ',
      tiles: [
        [1, 1, 2, 2, 1],
        [1, 7, 2, 7, 1],
        [1, 1, 1, 1, 1],
        [1, 2, 2, 2, 1],
        [1, 1, 1, 1, 1],
      ],
    }

    const parsedV1 = assertParseSuccess(parseMapJsonPayload(JSON.stringify(legacyPayload)))
    expect(parsedV1.displayName).toBe('Legacy Route')
    expect(parsedV1.tiles).toEqual(legacyPayload.tiles)

    const v2Payload = buildJsonExportPayload(parsedV1)
    const reparsedV2 = assertParseSuccess(parseMapJsonPayload(JSON.stringify(v2Payload)))

    expect(v2Payload.schemaVersion).toBe(2)
    expect(reparsedV2.tiles).toEqual(legacyPayload.tiles)
  })
})
