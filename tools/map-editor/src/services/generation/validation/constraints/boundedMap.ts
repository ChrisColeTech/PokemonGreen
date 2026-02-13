import type { GenerationContext, ValidationIssue } from '../../../../types/generation'

export const boundedMapValidator = (context: GenerationContext): ValidationIssue[] => {
  const expectedWidth = context.config.dimensions.width
  const expectedHeight = context.config.dimensions.height
  const issues: ValidationIssue[] = []

  if (context.grid.length !== expectedHeight) {
    issues.push({
      id: 'boundedMap',
      severity: 'error',
      message: `Grid height ${context.grid.length} does not match expected ${expectedHeight}.`,
    })
  }

  for (let y = 0; y < Math.min(context.grid.length, expectedHeight); y += 1) {
    const rowWidth = context.grid[y]?.length ?? 0
    if (rowWidth !== expectedWidth) {
      issues.push({
        id: 'boundedMap',
        severity: 'error',
        message: `Grid row ${y} width ${rowWidth} does not match expected ${expectedWidth}.`,
        cells: [{ x: Math.min(expectedWidth - 1, Math.max(0, rowWidth - 1)), y }],
      })
    }
  }

  return issues
}
