import type { EditorRegistryTileDefinition } from '../types/registry'

interface HslColor {
  h: number
  s: number
  l: number
}

interface RgbColor {
  r: number
  g: number
  b: number
}

interface HslShift {
  hueShift?: number
  saturationShift?: number
  lightnessShift?: number
}

interface CategoryProfile {
  hueCenter: number
  hueSpread: number
  saturationShift: number
  lightnessShift: number
}

const ANCHOR_TILE_IDS = new Set<number>([0, 1, 2])

const DEFAULT_CATEGORY_PROFILE: CategoryProfile = {
  hueCenter: 0,
  hueSpread: 18,
  saturationShift: 0.03,
  lightnessShift: 0.02,
}

const KNOWN_CATEGORY_PROFILES: Record<string, CategoryProfile> = {
  terrain: { hueCenter: -2, hueSpread: 20, saturationShift: 0.02, lightnessShift: 0.01 },
  encounter: { hueCenter: 8, hueSpread: 22, saturationShift: 0.04, lightnessShift: 0.03 },
  interactive: { hueCenter: -10, hueSpread: 18, saturationShift: 0.03, lightnessShift: 0.02 },
  entity: { hueCenter: 12, hueSpread: 24, saturationShift: 0.05, lightnessShift: 0.02 },
  trainer: { hueCenter: -14, hueSpread: 16, saturationShift: 0.06, lightnessShift: 0.01 },
}

const clamp01 = (value: number): number => Math.max(0, Math.min(1, value))

const normalizeHue = (hue: number): number => {
  const normalized = hue % 360
  return normalized < 0 ? normalized + 360 : normalized
}

const hexToRgb = (hexColor: string): RgbColor => {
  const normalized = hexColor.replace('#', '').trim()
  const expanded =
    normalized.length === 3
      ? normalized
          .split('')
          .map((value) => `${value}${value}`)
          .join('')
      : normalized

  const r = Number.parseInt(expanded.slice(0, 2), 16)
  const g = Number.parseInt(expanded.slice(2, 4), 16)
  const b = Number.parseInt(expanded.slice(4, 6), 16)

  return {
    r: Number.isNaN(r) ? 0 : r,
    g: Number.isNaN(g) ? 0 : g,
    b: Number.isNaN(b) ? 0 : b,
  }
}

const rgbToHex = ({ r, g, b }: RgbColor): string => {
  const toHex = (value: number): string => Math.round(value).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

const rgbToHsl = ({ r, g, b }: RgbColor): HslColor => {
  const red = r / 255
  const green = g / 255
  const blue = b / 255

  const max = Math.max(red, green, blue)
  const min = Math.min(red, green, blue)
  const delta = max - min

  let hue = 0
  const lightness = (max + min) / 2

  if (delta !== 0) {
    if (max === red) {
      hue = ((green - blue) / delta) % 6
    } else if (max === green) {
      hue = (blue - red) / delta + 2
    } else {
      hue = (red - green) / delta + 4
    }
  }

  const saturation =
    delta === 0
      ? 0
      : delta / (1 - Math.abs(2 * lightness - 1))

  return {
    h: normalizeHue(hue * 60),
    s: clamp01(saturation),
    l: clamp01(lightness),
  }
}

const hslToRgb = ({ h, s, l }: HslColor): RgbColor => {
  const normalizedHue = normalizeHue(h)
  const chroma = (1 - Math.abs(2 * l - 1)) * s
  const hueSection = normalizedHue / 60
  const secondary = chroma * (1 - Math.abs((hueSection % 2) - 1))

  let redPrime = 0
  let greenPrime = 0
  let bluePrime = 0

  if (hueSection >= 0 && hueSection < 1) {
    redPrime = chroma
    greenPrime = secondary
  } else if (hueSection >= 1 && hueSection < 2) {
    redPrime = secondary
    greenPrime = chroma
  } else if (hueSection >= 2 && hueSection < 3) {
    greenPrime = chroma
    bluePrime = secondary
  } else if (hueSection >= 3 && hueSection < 4) {
    greenPrime = secondary
    bluePrime = chroma
  } else if (hueSection >= 4 && hueSection < 5) {
    redPrime = secondary
    bluePrime = chroma
  } else {
    redPrime = chroma
    bluePrime = secondary
  }

  const match = l - chroma / 2
  return {
    r: (redPrime + match) * 255,
    g: (greenPrime + match) * 255,
    b: (bluePrime + match) * 255,
  }
}

export const shiftHexColorHsl = (hexColor: string, shift: HslShift): string => {
  const baseHsl = rgbToHsl(hexToRgb(hexColor))
  const shiftedHsl: HslColor = {
    h: normalizeHue(baseHsl.h + (shift.hueShift ?? 0)),
    s: clamp01(baseHsl.s + (shift.saturationShift ?? 0)),
    l: clamp01(baseHsl.l + (shift.lightnessShift ?? 0)),
  }

  return rgbToHex(hslToRgb(shiftedHsl))
}

const toCategoryProfileKey = (categoryId: string): string => categoryId.trim().toLowerCase()

const hashCategory = (categoryId: string): number => {
  let hash = 0
  for (let index = 0; index < categoryId.length; index += 1) {
    hash = (hash * 31 + categoryId.charCodeAt(index)) >>> 0
  }

  return hash
}

const resolveCategoryProfile = (categoryId: string): CategoryProfile => {
  const normalizedId = toCategoryProfileKey(categoryId)
  const knownProfile = KNOWN_CATEGORY_PROFILES[normalizedId]
  if (knownProfile) {
    return knownProfile
  }

  const hash = hashCategory(normalizedId)
  const hueCenterOffset = (hash % 31) - 15
  const hueSpreadOffset = ((Math.floor(hash / 31) % 7) - 3) * 2
  const saturationOffset = ((Math.floor(hash / 217) % 5) - 2) * 0.005
  const lightnessOffset = ((Math.floor(hash / 1085) % 5) - 2) * 0.005

  return {
    hueCenter: DEFAULT_CATEGORY_PROFILE.hueCenter + hueCenterOffset,
    hueSpread: DEFAULT_CATEGORY_PROFILE.hueSpread + hueSpreadOffset,
    saturationShift: DEFAULT_CATEGORY_PROFILE.saturationShift + saturationOffset,
    lightnessShift: DEFAULT_CATEGORY_PROFILE.lightnessShift + lightnessOffset,
  }
}

const getRelativePosition = (index: number, size: number): number => {
  if (size <= 1) {
    return 0
  }

  return index / (size - 1) - 0.5
}

const getCategoryHueShift = (tileId: number, index: number, profile: CategoryProfile, groupSize: number): number => {
  const position = getRelativePosition(index, groupSize)
  const idJitter = ((tileId * 17) % 7) - 3
  return profile.hueCenter + position * profile.hueSpread + idJitter * 1.4
}

const getCategoryLightnessShift = (tileId: number, index: number, profile: CategoryProfile, groupSize: number): number => {
  const position = getRelativePosition(index, groupSize)
  const parityJitter = (tileId + index) % 2 === 0 ? -0.03 : 0.03
  return profile.lightnessShift + position * 0.1 + parityJitter
}

export const createDistinctTileColorMap = (tiles: EditorRegistryTileDefinition[]): Record<number, string> => {
  const displayColors: Record<number, string> = {}

  for (const tile of tiles) {
    displayColors[tile.id] = tile.color
  }

  const groupedTiles: Record<string, EditorRegistryTileDefinition[]> = {}

  for (const tile of tiles) {
    if (ANCHOR_TILE_IDS.has(tile.id)) {
      continue
    }

    groupedTiles[tile.category] ??= []
    groupedTiles[tile.category].push(tile)
  }

  for (const category of Object.keys(groupedTiles)) {
    const tilesInCategory = [...groupedTiles[category]].sort((a, b) => a.id - b.id)
    const profile = resolveCategoryProfile(category)

    tilesInCategory.forEach((tile, index) => {
      const sourceHsl = rgbToHsl(hexToRgb(tile.color))
      const isNearMonochrome = sourceHsl.s < 0.08

      const hueShift = isNearMonochrome ? 0 : getCategoryHueShift(tile.id, index, profile, tilesInCategory.length)
      const saturationShift = isNearMonochrome ? 0.02 : profile.saturationShift
      const lightnessShift = getCategoryLightnessShift(tile.id, index, profile, tilesInCategory.length)

      displayColors[tile.id] = shiftHexColorHsl(tile.color, {
        hueShift,
        saturationShift,
        lightnessShift,
      })
    })
  }

  return displayColors
}

export const getDisplayTileColor = (
  tile: EditorRegistryTileDefinition,
  displayColorMap: Record<number, string>,
  useDistinctColors: boolean,
): string => {
  if (!useDistinctColors) {
    return tile.color
  }

  return displayColorMap[tile.id] ?? tile.color
}
