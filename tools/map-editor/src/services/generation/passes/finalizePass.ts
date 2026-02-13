import type { GenerationPass } from '../../../types/generation'

export const finalizePass: GenerationPass = {
  id: 'finalize',
  run: (context) => context,
}
