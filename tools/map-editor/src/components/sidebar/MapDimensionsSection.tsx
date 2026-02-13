import { Eraser, Ruler } from 'lucide-react'
import Button from '../common/Button'
import SectionTitle from './SectionTitle'
import type { SidebarControls } from './types'

type MapDimensionsSectionProps = Pick<
  SidebarControls,
  | 'mapName'
  | 'widthInput'
  | 'heightInput'
  | 'cellSize'
  | 'setMapName'
  | 'setWidthInput'
  | 'setHeightInput'
  | 'setCellSize'
  | 'resizeGridFromInputs'
  | 'clearGrid'
  | 'handleResetSaved'
>

function MapDimensionsSection({
  mapName,
  widthInput,
  heightInput,
  cellSize,
  setMapName,
  setWidthInput,
  setHeightInput,
  setCellSize,
  resizeGridFromInputs,
  clearGrid,
  handleResetSaved,
}: MapDimensionsSectionProps) {
  return (
    <section className="space-y-2 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-2.5">
      <SectionTitle>Map Dimensions</SectionTitle>
      <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
        <span>Map Name</span>
        <input
          type="text"
          value={mapName}
          maxLength={80}
          onChange={(event) => setMapName(event.target.value)}
          className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
        />
      </label>
      <div className="grid grid-cols-3 gap-2">
        <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
          <span>Width</span>
          <input
            type="number"
            value={widthInput}
            min={5}
            max={100}
            onChange={(event) => setWidthInput(event.target.value)}
            className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
          />
        </label>
        <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
          <span>Height</span>
          <input
            type="number"
            value={heightInput}
            min={5}
            max={100}
            onChange={(event) => setHeightInput(event.target.value)}
            className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
          />
        </label>
        <label className="space-y-1 text-[11px] text-[var(--text-muted)]">
          <span>Cell Size</span>
          <input
            type="number"
            value={cellSize}
            min={8}
            max={48}
            onChange={(event) => setCellSize(event.target.value)}
            className="h-8 w-full rounded border border-[var(--border-soft)] bg-[var(--surface)] px-2 text-xs text-[var(--text-main)] outline-none transition-colors focus:border-[var(--border-strong)] focus:ring-2 focus:ring-[var(--focus-ring)]"
          />
        </label>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <Button size="sm" className="justify-center gap-1.5 text-[11px]" onClick={resizeGridFromInputs}>
          <Ruler size={13} />
          Resize
        </Button>
        <Button size="sm" className="justify-center gap-1.5 text-[11px]" onClick={clearGrid}>
          <Eraser size={13} />
          Clear All
        </Button>
      </div>
      <Button size="sm" className="w-full justify-center gap-1.5 text-[11px]" onClick={handleResetSaved}>
        Reset Saved
      </Button>
    </section>
  )
}

export default MapDimensionsSection
