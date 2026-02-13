import { applyGenerationBalancePass } from '../balance/generationBalanceService'
import type { GenerationPass } from '../../../types/generation'

export const balancePass: GenerationPass = {
  id: 'balance',
  run: (context) => applyGenerationBalancePass(context),
}
