import type { GenerationDiagnostics, GenerationPassId } from '../../types/generation'

export interface GenerationDiagnosticsSummary {
  warningCount: number
  majorIssueCount: number
  passCount: number
  totalPassDurationMs: number
  slowestPassId: GenerationPassId | null
  slowestPassDurationMs: number
}

export const summarizeGenerationDiagnostics = (
  diagnostics: GenerationDiagnostics | null,
): GenerationDiagnosticsSummary | null => {
  if (!diagnostics) {
    return null
  }

  const passEntries = (Object.entries(diagnostics.passDurationsMs) as Array<[GenerationPassId, number | undefined]>).filter(
    (entry): entry is [GenerationPassId, number] => typeof entry[1] === 'number',
  )

  let slowestPassId: GenerationPassId | null = null
  let slowestPassDurationMs = 0
  let totalPassDurationMs = 0

  for (const [passId, durationMs] of passEntries) {
    totalPassDurationMs += durationMs
    if (durationMs > slowestPassDurationMs) {
      slowestPassDurationMs = durationMs
      slowestPassId = passId
    }
  }

  return {
    warningCount: diagnostics.warnings.length,
    majorIssueCount: diagnostics.hardConstraintIssues.filter((issue) => issue.severity === 'error').length,
    passCount: passEntries.length,
    totalPassDurationMs,
    slowestPassId,
    slowestPassDurationMs,
  }
}
