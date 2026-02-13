import { HARD_CONSTRAINT_VALIDATOR_REGISTRY } from './constraints/registry'
import { findSpawnCell } from './constraints/shared'
import type { GenerationContext, HardConstraintId, ValidationIssue } from '../../../types/generation'
import type { GridPoint } from '../../../types/editor'

const getActiveConstraintIds = (context: GenerationContext): HardConstraintId[] =>
  context.archetype.requiredHardConstraints.filter((constraintId) => context.config.hardConstraintPolicy[constraintId])

export const validateHardConstraints = (context: GenerationContext): ValidationIssue[] => {
  const issues: ValidationIssue[] = []

  for (const constraintId of getActiveConstraintIds(context)) {
    const validator = HARD_CONSTRAINT_VALIDATOR_REGISTRY[constraintId]
    if (!validator) {
      continue
    }

    issues.push(...validator(context))
  }

  return issues
}

export const resolveSpawnCell = (context: GenerationContext): GridPoint | null => findSpawnCell(context)
