import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const minRequiredStructuresValidator = (context: GenerationContext): ValidationIssue[] => {
  const missing: string[] = []

  for (const [buildingId, target] of Object.entries(context.archetype.buildingTargets)) {
    if (!target || target.min <= 0) {
      continue
    }

    const placedCount = context.buildingPlacements.filter((placement) => placement.buildingId === buildingId).length
    if (placedCount < target.min) {
      missing.push(`${buildingId} (${placedCount}/${target.min})`)
    }
  }

  if (missing.length === 0) {
    return []
  }

  return [
    {
      id: 'minRequiredStructures',
      severity: 'error',
      message: `Missing required structures: ${missing.join(', ')}.`,
    },
  ]
}
