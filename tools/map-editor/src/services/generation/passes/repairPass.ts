import { applyHardConstraintRepairs } from '../repair/hardConstraintRepairService'
import { validateHardConstraints } from '../validation/hardConstraintValidationService'
import type { GenerationPass } from '../../../types/generation'

export const repairPass: GenerationPass = {
  id: 'repair',
  run: (context) => {
    let issues = validateHardConstraints(context)
    context.diagnostics.hardConstraintIssues = issues

    const maxAttempts = Math.max(0, context.config.pipeline.maxRepairAttempts)
    for (let attempt = 1; attempt <= maxAttempts && issues.length > 0; attempt += 1) {
      const actions = applyHardConstraintRepairs(context, issues, attempt)
      context.diagnostics.appliedRepairs.push(...actions)

      const appliedAny = actions.some((action) => action.applied)
      issues = validateHardConstraints(context)
      context.diagnostics.hardConstraintIssues = issues

      if (!appliedAny) {
        break
      }
    }

    if (context.diagnostics.hardConstraintIssues.length > 0) {
      context.diagnostics.warnings.push(
        `Generation completed with ${context.diagnostics.hardConstraintIssues.length} unresolved hard constraint issue(s).`,
      )
    }

    return context
  },
}
