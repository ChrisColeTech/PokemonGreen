import { GENERATION_STARTER_TEMPLATES_BY_ID } from '../../data/generation/templates'
import { clamp } from './shared/math'
import type {
  GenerationContext,
  GenerationTemplate,
  GenerationTemplateHints,
  GenerationTemplateId,
} from '../../types/generation'

const normalizeTemplateHints = (hints: GenerationTemplateHints): GenerationTemplateHints => {
  const primaryPath = hints.primaryPath
  const town = hints.town

  const normalizedPrimaryPath = primaryPath
    ? {
        startYRatio: clamp(primaryPath.startYRatio, 0.1, 0.9),
        minYRatio: clamp(Math.min(primaryPath.minYRatio, primaryPath.maxYRatio), 0.08, 0.92),
        maxYRatio: clamp(Math.max(primaryPath.minYRatio, primaryPath.maxYRatio), 0.08, 0.92),
        meanderChance: clamp(primaryPath.meanderChance, 0.05, 0.85),
      }
    : undefined

  const normalizedTown = town
    ? {
        anchorXRatio: clamp(town.anchorXRatio, 0.05, 0.9),
        anchorYRatio: clamp(town.anchorYRatio, 0.1, 0.9),
        widthRatio: clamp(town.widthRatio, 0.2, 0.5),
        heightRatio: clamp(town.heightRatio, 0.25, 0.65),
      }
    : undefined

  return {
    primaryPath: normalizedPrimaryPath,
    town: normalizedTown,
    encounterZone: hints.encounterZone ?? 'balanced',
  }
}

export const resolveGenerationTemplate = (templateId: GenerationTemplateId | null): GenerationTemplate | null => {
  if (!templateId) {
    return null
  }

  return GENERATION_STARTER_TEMPLATES_BY_ID[templateId] ?? null
}

export const applyGenerationTemplatePrepass = (context: GenerationContext): GenerationContext => {
  const template = resolveGenerationTemplate(context.config.templateId)
  context.state.activeTemplateId = template?.id ?? null
  context.state.templateHints = template ? normalizeTemplateHints(template.hints) : null
  return context
}
