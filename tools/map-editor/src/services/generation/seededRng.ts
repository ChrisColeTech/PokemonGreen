import type { SeededRng } from '../../types/generation'

const clamp01 = (value: number): number => {
  if (value <= 0) {
    return 0
  }

  if (value >= 1) {
    return 1
  }

  return value
}

const hashSeed = (input: string): [number, number, number, number] => {
  let h1 = 0x9e3779b9
  let h2 = 0x243f6a88
  let h3 = 0xb7e15162
  let h4 = 0xdeadbeef

  for (let index = 0; index < input.length; index += 1) {
    const code = input.charCodeAt(index)
    h1 = Math.imul(h1 ^ code, 0x85ebca6b)
    h2 = Math.imul(h2 ^ code, 0xc2b2ae35)
    h3 = Math.imul(h3 ^ code, 0x27d4eb2f)
    h4 = Math.imul(h4 ^ code, 0x165667b1)
  }

  h1 = Math.imul(h1 ^ (h1 >>> 16), 0x85ebca6b)
  h2 = Math.imul(h2 ^ (h2 >>> 13), 0xc2b2ae35)
  h3 = Math.imul(h3 ^ (h3 >>> 16), 0x27d4eb2f)
  h4 = Math.imul(h4 ^ (h4 >>> 13), 0x165667b1)

  return [h1 >>> 0, h2 >>> 0, h3 >>> 0, h4 >>> 0]
}

const createSfc32 = (a0: number, b0: number, c0: number, d0: number): (() => number) => {
  let a = a0 >>> 0
  let b = b0 >>> 0
  let c = c0 >>> 0
  let d = d0 >>> 0

  return () => {
    const t = (a + b + d) >>> 0
    d = (d + 1) >>> 0
    a = b ^ (b >>> 9)
    b = (c + (c << 3)) >>> 0
    c = ((c << 21) | (c >>> 11)) >>> 0
    c = (c + t) >>> 0
    return (t >>> 0) / 4294967296
  }
}

const createRngFromSeed = (seed: string): SeededRng => {
  const stream = createSfc32(...hashSeed(seed))

  const next = (): number => stream()

  const int = (minInclusive: number, maxInclusive: number): number => {
    const min = Math.ceil(Math.min(minInclusive, maxInclusive))
    const max = Math.floor(Math.max(minInclusive, maxInclusive))
    if (max <= min) {
      return min
    }

    return min + Math.floor(next() * (max - min + 1))
  }

  const chance = (probability: number): boolean => next() < clamp01(probability)

  const pick = <T>(items: readonly T[]): T => {
    if (items.length === 0) {
      throw new Error('Cannot pick from an empty array.')
    }

    return items[int(0, items.length - 1)]
  }

  const shuffle = <T>(items: readonly T[]): T[] => {
    const nextItems = [...items]
    for (let index = nextItems.length - 1; index > 0; index -= 1) {
      const swapIndex = int(0, index)
      const swapValue = nextItems[index]
      nextItems[index] = nextItems[swapIndex]
      nextItems[swapIndex] = swapValue
    }

    return nextItems
  }

  const fork = (streamName: string): SeededRng => createRngFromSeed(`${seed}::${streamName}`)

  return {
    next,
    int,
    chance,
    pick,
    shuffle,
    fork,
  }
}

export const createSeededRng = (seed: string): SeededRng => createRngFromSeed(seed)
