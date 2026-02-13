import { RotateCcw, RotateCw } from 'lucide-react'
import Button from '../common/Button'
import SectionTitle from './SectionTitle'
import type { SidebarControls } from './types'

type BuildingControlsSectionProps = Pick<
  SidebarControls,
  'selectedBuildingId' | 'buildingRotation' | 'selectedBuilding' | 'rotatedBuilding' | 'handleRotateBuilding'
>

function BuildingControlsSection({
  selectedBuildingId,
  buildingRotation,
  selectedBuilding,
  rotatedBuilding,
  handleRotateBuilding,
}: BuildingControlsSectionProps) {
  return (
    <section className="space-y-2 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-2.5">
      <SectionTitle>Building Controls</SectionTitle>
      <div className="grid grid-cols-2 gap-2">
        <Button
          size="sm"
          className="justify-center gap-1.5 text-[11px]"
          disabled={!selectedBuildingId}
          onClick={() => handleRotateBuilding(-1)}
        >
          <RotateCcw size={13} />
          Rotate Left
        </Button>
        <Button
          size="sm"
          className="justify-center gap-1.5 text-[11px]"
          disabled={!selectedBuildingId}
          onClick={() => handleRotateBuilding(1)}
        >
          <RotateCw size={13} />
          Rotate Right
        </Button>
      </div>
      <div className="rounded border border-dashed border-[var(--border-strong)] bg-[var(--surface-muted)] px-2 py-1.5">
        {selectedBuilding && rotatedBuilding ? (
          <>
            <p className="text-xs font-semibold text-[var(--text-main)]">{selectedBuilding.name}</p>
            <p className="mt-0.5 text-[11px] text-[var(--text-muted)]">
              Size: {rotatedBuilding.width}x{rotatedBuilding.height} ({buildingRotation * 90}deg)
            </p>
          </>
        ) : (
          <>
            <p className="text-xs font-semibold text-[var(--text-main)]">No Building Selected</p>
            <p className="mt-0.5 text-[11px] text-[var(--text-muted)]">Pick a building from the Buildings category to place it.</p>
          </>
        )}
      </div>
    </section>
  )
}

export default BuildingControlsSection
