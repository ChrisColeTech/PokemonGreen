import { validateHardConstraints } from '../validation/hardConstraintValidationService'
import type { GenerationPass } from '../../../types/generation'

export const validatePass: GenerationPass = {
  id: 'validate',
  run: (context) => {
    context.diagnostics.hardConstraintIssues = validateHardConstraints(context)
    return context
  },
}
