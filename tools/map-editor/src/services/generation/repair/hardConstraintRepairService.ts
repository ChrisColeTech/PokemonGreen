import { HARD_CONSTRAINT_REPAIR_ACTION_REGISTRY } from './actions/registry'
import { refreshPrimaryPathState } from './actions/shared'
import type { GenerationContext, GenerationRepairAction, ValidationIssue } from '../../../types/generation'

export const applyHardConstraintRepairs = (
  context: GenerationContext,
  issues: ValidationIssue[],
  attempt: number,
): GenerationRepairAction[] => {
  const issueIds = new Set(issues.map((issue) => issue.id))
  const actions: GenerationRepairAction[] = []

  for (const repair of HARD_CONSTRAINT_REPAIR_ACTION_REGISTRY) {
    const result = issueIds.has(repair.issueId) ? repair.run(context) : repair.defaultResult
    actions.push({
      id: `${repair.idPrefix}-${attempt}`,
      description: repair.describe(result),
      applied: repair.isApplied(result),
    })
  }

  refreshPrimaryPathState(context)
  return actions
}
