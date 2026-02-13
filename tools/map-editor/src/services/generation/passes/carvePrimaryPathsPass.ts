import { pointKey } from '../shared/geometry'
import { clamp, lerp, smoothstep } from '../shared/math'
import { getPrimaryPathTileId } from '../shared/tileRoles'
import type { GridPoint } from '../../../types/editor'
import type { GenerationContext, GenerationPass } from '../../../types/generation'

type RouteStyle = 'straightSpine' | 'gentleSCurve' | 'segmentedBends' | 'branchSpurs'

interface PathStyleWeights {
  straightSpine: number
  gentleSCurve: number
  segmentedBends: number
  branchSpurs: number
}

interface PathProfile {
  style: RouteStyle
  amplitude: number
  segmentCount: number
  meanderChance: number
  spurChance: number
  maxSpurs: number
  spurLengthRange: readonly [number, number]
  spurLateralChance: number
}

const buildStyleWeights = (
  context: GenerationContext,
  meanderChance: number,
  encounterZone: 'balanced' | 'west' | 'east',
): PathStyleWeights => {
  const routeReadability = context.archetype.softGoalWeights.routeReadability
  const biomeVariety = context.archetype.softGoalWeights.biomeVariety
  const encounterPacing = context.archetype.softGoalWeights.encounterPacing

  const readabilityBias = clamp(routeReadability - 1, 0, 0.5)
  const varietyBias = clamp(biomeVariety - 1, 0, 0.6)
  const pacingBias = clamp(encounterPacing - 1, 0, 0.6)

  return {
    straightSpine: 1.1 + (1 - meanderChance) * 1.3 + readabilityBias * 1.8,
    gentleSCurve: 0.8 + meanderChance * 1.05 + (encounterZone === 'balanced' ? 0.2 : 0.45),
    segmentedBends: 0.75 + meanderChance * 1.35 + pacingBias * 1.25,
    branchSpurs: 0.45 + meanderChance * 0.95 + varietyBias * 1.15,
  }
}

const pickRouteStyle = (rng: GenerationContext['rng'], weights: PathStyleWeights): RouteStyle => {
  const entries: Array<[RouteStyle, number]> = [
    ['straightSpine', Math.max(0.01, weights.straightSpine)],
    ['gentleSCurve', Math.max(0.01, weights.gentleSCurve)],
    ['segmentedBends', Math.max(0.01, weights.segmentedBends)],
    ['branchSpurs', Math.max(0.01, weights.branchSpurs)],
  ]
  const totalWeight = entries.reduce((sum, [, weight]) => sum + weight, 0)
  const roll = rng.next() * totalWeight
  let cumulative = 0

  for (const [style, weight] of entries) {
    cumulative += weight
    if (roll <= cumulative) {
      return style
    }
  }

  return entries[entries.length - 1][0]
}

const buildTargetYSeries = (
  width: number,
  yMin: number,
  yMax: number,
  startY: number,
  centerY: number,
  encounterZone: 'balanced' | 'west' | 'east',
  profile: PathProfile,
  rng: GenerationContext['rng'],
): number[] => {
  const targets: number[] = []
  const span = Math.max(1, yMax - yMin)

  if (profile.style === 'straightSpine') {
    let current = startY
    for (let x = 0; x < width; x += 1) {
      if (x > 0 && rng.chance(profile.meanderChance * 0.3)) {
        const toCenter = centerY - current
        const preferredStep = toCenter === 0 ? rng.pick([-1, 1]) : toCenter > 0 ? 1 : -1
        current = clamp(current + preferredStep, yMin, yMax)
      }
      targets.push(current)
    }
    return targets
  }

  if (profile.style === 'gentleSCurve') {
    const zonePhaseShift = encounterZone === 'east' ? Math.PI * 0.2 : encounterZone === 'west' ? -Math.PI * 0.2 : 0
    const direction = rng.chance(0.5) ? 1 : -1
    for (let x = 0; x < width; x += 1) {
      const progress = width <= 1 ? 0 : x / (width - 1)
      const zoneBias = encounterZone === 'east' ? lerp(0.8, 1.2, progress) : encounterZone === 'west' ? lerp(1.2, 0.8, progress) : 1
      const wave = Math.sin(progress * Math.PI * 2 + zonePhaseShift)
      const jitter = rng.chance(profile.meanderChance * 0.25) ? rng.pick([-1, 0, 1]) : 0
      const next = clamp(Math.round(centerY + direction * wave * profile.amplitude * zoneBias + jitter), yMin, yMax)
      targets.push(next)
    }
    return targets
  }

  if (profile.style === 'segmentedBends') {
    const controlPoints: Array<{ x: number; y: number }> = [{ x: 0, y: startY }]
    const segmentCount = Math.max(2, profile.segmentCount)
    const widthSegments = Math.max(1, segmentCount - 1)
    let previousY = startY

    for (let index = 1; index < segmentCount; index += 1) {
      const x = Math.round((index * (width - 1)) / widthSegments)
      const zoneMultiplier = encounterZone === 'east'
        ? lerp(0.8, 1.25, x / Math.max(1, width - 1))
        : encounterZone === 'west'
          ? lerp(1.25, 0.8, x / Math.max(1, width - 1))
          : 1
      const offset = rng.int(-profile.amplitude, profile.amplitude)
      const boundedOffset = clamp(Math.round(offset * zoneMultiplier), -Math.floor(span * 0.45), Math.floor(span * 0.45))
      const targetY = clamp(previousY + boundedOffset, yMin, yMax)
      controlPoints.push({ x, y: targetY })
      previousY = targetY
    }

    controlPoints[controlPoints.length - 1] = {
      x: width - 1,
      y: clamp(Math.round((controlPoints[controlPoints.length - 1].y + centerY) / 2), yMin, yMax),
    }

    for (let x = 0; x < width; x += 1) {
      let segmentIndex = 0
      while (segmentIndex < controlPoints.length - 2 && x > controlPoints[segmentIndex + 1].x) {
        segmentIndex += 1
      }

      const left = controlPoints[segmentIndex]
      const right = controlPoints[Math.min(controlPoints.length - 1, segmentIndex + 1)]
      const denominator = Math.max(1, right.x - left.x)
      const t = smoothstep((x - left.x) / denominator)
      const blended = Math.round(lerp(left.y, right.y, t))
      targets.push(clamp(blended, yMin, yMax))
    }

    return targets
  }

  let current = startY
  for (let x = 0; x < width; x += 1) {
    const progress = width <= 1 ? 0 : x / (width - 1)
    const centerPull = clamp(Math.round(lerp(current, centerY, 0.18)), yMin, yMax)
    if (rng.chance(profile.meanderChance * (encounterZone === 'balanced' ? 0.55 : 0.75))) {
      const zoneStep = encounterZone === 'east' ? (progress > 0.5 ? 1 : -1) : encounterZone === 'west' ? (progress < 0.5 ? 1 : -1) : rng.pick([-1, 1])
      current = clamp(centerPull + zoneStep, yMin, yMax)
    } else {
      current = centerPull
    }
    targets.push(current)
  }

  return targets
}

export const carvePrimaryPathsPass: GenerationPass = {
  id: 'carvePrimaryPaths',
  run: (context) => {
    const styleSeed = `${context.config.archetypeId}:${context.state.activeTemplateId ?? 'none'}`
    const profileRng = context.rng.fork(`carvePrimaryPaths:${styleSeed}:profile`)
    const width = context.config.dimensions.width
    const height = context.config.dimensions.height
    const pathTemplate = context.state.templateHints?.primaryPath
    const townTemplate = context.state.templateHints?.town
    const encounterZone = context.state.templateHints?.encounterZone ?? 'balanced'
    const pathTileId = getPrimaryPathTileId(context)
    const defaultYMin = Math.max(1, Math.floor(height * 0.2))
    const defaultYMax = Math.min(height - 2, Math.ceil(height * 0.8))
    const yMin = pathTemplate ? clamp(Math.floor(height * pathTemplate.minYRatio), 1, height - 2) : defaultYMin
    const yMax = pathTemplate ? clamp(Math.ceil(height * pathTemplate.maxYRatio), yMin, height - 2) : defaultYMax
    const seededStartY = pathTemplate ? Math.round(height * pathTemplate.startYRatio) : Math.floor(height / 2)
    const townCenterY = townTemplate
      ? clamp(Math.round(height * townTemplate.anchorYRatio), yMin, yMax)
      : clamp(Math.round(height / 2), yMin, yMax)
    const meanderChance = pathTemplate?.meanderChance ?? 0.35
    const styleWeights = buildStyleWeights(context, meanderChance, encounterZone)
    const routeStyle = pickRouteStyle(profileRng, styleWeights)
    const routeReadability = context.archetype.softGoalWeights.routeReadability
    const readabilityScale = clamp(1.3 - (routeReadability - 1) * 0.75, 0.7, 1.3)
    const ySpan = Math.max(1, yMax - yMin)
    const amplitudeBase = Math.max(1, Math.floor(ySpan * (0.12 + meanderChance * 0.3) * readabilityScale))
    const segmentBase = clamp(Math.round(2 + meanderChance * 5 + context.archetype.softGoalWeights.encounterPacing), 2, 6)
    const spurBase = clamp(meanderChance * 0.35 + (context.archetype.softGoalWeights.biomeVariety - 1) * 0.2, 0.08, 0.45)
    const pathProfile: PathProfile = {
      style: routeStyle,
      amplitude: routeStyle === 'straightSpine' ? Math.max(1, Math.floor(amplitudeBase * 0.55)) : amplitudeBase,
      segmentCount: routeStyle === 'segmentedBends' ? segmentBase : Math.max(2, segmentBase - 1),
      meanderChance,
      spurChance: routeStyle === 'branchSpurs' ? clamp(spurBase + 0.12, 0.12, 0.6) : spurBase,
      maxSpurs: clamp(Math.floor(width / 8), 1, 5),
      spurLengthRange: [2, clamp(Math.round(3 + meanderChance * 6), 3, Math.max(4, Math.floor(height * 0.35)))],
      spurLateralChance: routeStyle === 'branchSpurs' ? 0.35 : 0.2,
    }
    const carveRng = context.rng.fork(`carvePrimaryPaths:${styleSeed}:${routeStyle}:carve`)
    const startY = clamp(seededStartY + carveRng.int(-2, 2), yMin, yMax)
    const centerY = clamp(Math.round((startY * 2 + townCenterY) / 3), yMin, yMax)
    const targetYSeries = buildTargetYSeries(
      width,
      yMin,
      yMax,
      startY,
      centerY,
      encounterZone,
      pathProfile,
      carveRng,
    )
    const pathCells: GridPoint[] = []
    const spineAnchors: GridPoint[] = []
    let currentY = targetYSeries[0] ?? startY

    const addPathCell = (x: number, y: number): void => {
      context.grid[y][x] = pathTileId
      pathCells.push({ x, y })
    }

    for (let x = 0; x < width; x += 1) {
      const westTownInfluence = townTemplate
        ? clamp(townTemplate.anchorXRatio + townTemplate.widthRatio * 0.6, 0.15, 0.7)
        : 0.24
      const townInfluenceProgress = 1 - clamp((x / Math.max(1, width - 1)) / westTownInfluence, 0, 1)
      const targetY = clamp(
        Math.round(lerp(targetYSeries[x] ?? currentY, townCenterY, townInfluenceProgress * 0.35)),
        yMin,
        yMax,
      )

      const step = targetY > currentY ? 1 : -1
      while (currentY !== targetY) {
        addPathCell(x, currentY)
        currentY += step
      }

      addPathCell(x, currentY)
      spineAnchors.push({ x, y: currentY })
    }

    const canCarveSpurs = pathProfile.style === 'branchSpurs'
    if (canCarveSpurs) {
      const candidateAnchors = carveRng.shuffle(spineAnchors.filter((cell) => cell.x > 2 && cell.x < width - 3))
      let carvedSpurs = 0

      for (const anchor of candidateAnchors) {
        if (carvedSpurs >= pathProfile.maxSpurs) {
          break
        }
        if (!carveRng.chance(pathProfile.spurChance)) {
          continue
        }

        const preferredDirection = anchor.y < centerY ? 1 : -1
        let direction = carveRng.chance(0.2) ? preferredDirection * -1 : preferredDirection
        const [minLength, maxLength] = pathProfile.spurLengthRange
        const length = carveRng.int(minLength, Math.max(minLength, maxLength))
        let x = anchor.x
        let y = anchor.y

        for (let stepIndex = 0; stepIndex < length; stepIndex += 1) {
          y = clamp(y + direction, yMin, yMax)
          if (y === yMin || y === yMax) {
            direction *= -1
          }

          addPathCell(x, y)

          if (carveRng.chance(pathProfile.spurLateralChance)) {
            const lateral = encounterZone === 'east'
              ? carveRng.pick([0, 1, 1])
              : encounterZone === 'west'
                ? carveRng.pick([0, -1, -1])
                : carveRng.pick([-1, 0, 1])
            x = clamp(x + lateral, 1, width - 2)
            addPathCell(x, y)
          }
        }

        carvedSpurs += 1
      }
    }

    const deduped = new Map<string, GridPoint>()
    for (const cell of pathCells) {
      deduped.set(pointKey(cell.x, cell.y), cell)
    }
    context.state.primaryPathCells = Array.from(deduped.values())
    return context
  },
}
