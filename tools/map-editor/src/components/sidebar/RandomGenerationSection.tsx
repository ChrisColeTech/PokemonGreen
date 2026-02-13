import Button from '../common/Button'
import SectionTitle from './SectionTitle'
import type { RandomGenerationControls } from './types'

type RandomGenerationSectionProps = Pick<
  RandomGenerationControls,
  | 'archetypeOptions'
  | 'templateOptions'
  | 'currentMapWidth'
  | 'currentMapHeight'
  | 'generationSeedInput'
  | 'generationArchetypeId'
  | 'generationTemplateId'
  | 'generationWidthInput'
  | 'generationHeightInput'
  | 'generationUseCurrentDimensions'
  | 'generationMaxRepairAttempts'
  | 'generationEnforceSpawnSafety'
  | 'generationEnforceDoorConnectivity'
  | 'lastGeneratedSeed'
  | 'lastGenerationDiagnostics'
  | 'diagnosticsSummary'
  | 'canRegenerate'
  | 'setGenerationSeedInput'
  | 'setGenerationArchetypeId'
  | 'setGenerationTemplateId'
  | 'setGenerationWidthInput'
  | 'setGenerationHeightInput'
  | 'setGenerationUseCurrentDimensions'
  | 'setGenerationMaxRepairAttempts'
  | 'setGenerationEnforceSpawnSafety'
  | 'setGenerationEnforceDoorConnectivity'
  | 'generateRandomMapFromControls'
  | 'regenerateRandomMap'
>

function RandomGenerationSection({
  archetypeOptions,
  templateOptions,
  currentMapWidth,
  currentMapHeight,
  generationSeedInput,
  generationArchetypeId,
  generationTemplateId,
  generationWidthInput,
  generationHeightInput,
  generationUseCurrentDimensions,
  generationMaxRepairAttempts,
  generationEnforceSpawnSafety,
  generationEnforceDoorConnectivity,
  lastGeneratedSeed,
  lastGenerationDiagnostics,
  diagnosticsSummary,
  canRegenerate,
  setGenerationSeedInput,
  setGenerationArchetypeId,
  setGenerationTemplateId,
  setGenerationWidthInput,
  setGenerationHeightInput,
  setGenerationUseCurrentDimensions,
  setGenerationMaxRepairAttempts,
  setGenerationEnforceSpawnSafety,
  setGenerationEnforceDoorConnectivity,
  generateRandomMapFromControls,
  regenerateRandomMap,
}: RandomGenerationSectionProps) {
  return (
    <section className="space-y-2 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-2.5">
      <SectionTitle>Random Generation</SectionTitle>
      <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
        <span>Seed</span>
        <input
          type="text"
          value={generationSeedInput}
          onChange={(event) => setGenerationSeedInput(event.target.value)}
          placeholder="auto"
          className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
        />
      </label>
      <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
        <span>Archetype</span>
        <select
          value={generationArchetypeId}
          onChange={(event) => setGenerationArchetypeId(event.target.value as typeof generationArchetypeId)}
          className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
        >
          {archetypeOptions.map((archetype) => (
            <option key={archetype.id} value={archetype.id}>
              {archetype.label}
            </option>
          ))}
        </select>
      </label>
      <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
        <span>Template</span>
        <select
          value={generationTemplateId ?? ''}
          onChange={(event) =>
            setGenerationTemplateId(
              event.target.value ? (event.target.value as (typeof templateOptions)[number]['id']) : null,
            )
          }
          className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
        >
          <option value="">None</option>
          {templateOptions.map((template) => (
            <option key={template.id} value={template.id}>
              {template.label}
            </option>
          ))}
        </select>
      </label>
      <label className="inline-flex items-center gap-1.5 text-[11px] text-[var(--text-muted)]">
        <input
          type="checkbox"
          checked={generationUseCurrentDimensions}
          onChange={(event) => setGenerationUseCurrentDimensions(event.target.checked)}
          className="h-3.5 w-3.5 rounded border border-[var(--border-soft)] bg-[var(--surface-muted)] accent-[var(--accent)]"
        />
        Use current map size ({currentMapWidth}x{currentMapHeight})
      </label>
      <div className="grid grid-cols-2 gap-2">
        <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
          <span>Gen Width</span>
          <input
            type="number"
            value={generationWidthInput}
            min={5}
            max={100}
            disabled={generationUseCurrentDimensions}
            onChange={(event) => setGenerationWidthInput(event.target.value)}
            className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)] disabled:opacity-60"
          />
        </label>
        <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
          <span>Gen Height</span>
          <input
            type="number"
            value={generationHeightInput}
            min={5}
            max={100}
            disabled={generationUseCurrentDimensions}
            onChange={(event) => setGenerationHeightInput(event.target.value)}
            className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)] disabled:opacity-60"
          />
        </label>
      </div>
      <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
        <span>Repair Attempts</span>
        <input
          type="number"
          value={generationMaxRepairAttempts}
          min={0}
          max={8}
          onChange={(event) => setGenerationMaxRepairAttempts(event.target.value)}
          className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
        />
      </label>
      <div className="grid grid-cols-1 gap-1">
        <label className="inline-flex items-center gap-1.5 text-[11px] text-[var(--text-muted)]">
          <input
            type="checkbox"
            checked={generationEnforceSpawnSafety}
            onChange={(event) => setGenerationEnforceSpawnSafety(event.target.checked)}
            className="h-3.5 w-3.5 rounded border border-[var(--border-soft)] bg-[var(--surface-muted)] accent-[var(--accent)]"
          />
          Enforce spawn safety
        </label>
        <label className="inline-flex items-center gap-1.5 text-[11px] text-[var(--text-muted)]">
          <input
            type="checkbox"
            checked={generationEnforceDoorConnectivity}
            onChange={(event) => setGenerationEnforceDoorConnectivity(event.target.checked)}
            className="h-3.5 w-3.5 rounded border border-[var(--border-soft)] bg-[var(--surface-muted)] accent-[var(--accent)]"
          />
          Enforce door connectivity
        </label>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <Button size="sm" variant="primary" className="justify-center text-[11px]" onClick={() => generateRandomMapFromControls()}>
          Generate
        </Button>
        <Button size="sm" className="justify-center text-[11px]" disabled={!canRegenerate} onClick={regenerateRandomMap}>
          Regenerate
        </Button>
      </div>
      <div className="rounded border border-dashed border-[var(--border-strong)] bg-[var(--surface-muted)] px-2 py-1.5">
        {diagnosticsSummary ? (
          <>
            <p className="text-xs font-semibold text-[var(--text-main)]">
              Warnings: {diagnosticsSummary.warningCount} | Major Issues: {diagnosticsSummary.majorIssueCount}
            </p>
            <p className="mt-0.5 text-[11px] text-[var(--text-muted)]">
              Passes: {diagnosticsSummary.passCount} | Total: {diagnosticsSummary.totalPassDurationMs.toFixed(1)}ms
            </p>
            {diagnosticsSummary.slowestPassId ? (
              <p className="mt-0.5 truncate text-[11px] text-[var(--text-muted)]">
                Slowest: {diagnosticsSummary.slowestPassId} ({diagnosticsSummary.slowestPassDurationMs.toFixed(1)}ms)
              </p>
            ) : null}
            {lastGeneratedSeed ? (
              <p className="mt-0.5 truncate text-[11px] text-[var(--text-muted)]">Seed: {lastGeneratedSeed}</p>
            ) : null}
            {lastGenerationDiagnostics?.warnings[0] ? (
              <p className="mt-0.5 truncate text-[11px] text-[var(--text-muted)]">
                Note: {lastGenerationDiagnostics.warnings[0]}
              </p>
            ) : null}
          </>
        ) : (
          <>
            <p className="text-xs font-semibold text-[var(--text-main)]">No Generation Run Yet</p>
            <p className="mt-0.5 text-[11px] text-[var(--text-muted)]">Run Generate to see diagnostics and timings.</p>
          </>
        )}
      </div>
    </section>
  )
}

export default RandomGenerationSection
