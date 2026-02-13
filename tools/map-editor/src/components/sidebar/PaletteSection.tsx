import { TILE_CATEGORIES, TILES } from '../../data/tiles'
import { createDistinctTileColorMap, getDisplayTileColor } from '../../services/tileColorService'
import SectionTitle from './SectionTitle'
import type { SidebarControls } from './types'

const DISTINCT_TILE_COLOR_MAP = createDistinctTileColorMap(TILES)

type PaletteSectionProps = Pick<
  SidebarControls,
  'selectedCategory' | 'selectedTileId' | 'selectedBuildingId' | 'visibleTiles' | 'buildings' | 'handleSelectCategory' | 'handleSelectTile' | 'handleToggleBuilding'
> & {
  useDistinctColors: boolean
  setUseDistinctColors: (enabled: boolean) => void
}

function PaletteSection({
  selectedCategory,
  selectedTileId,
  selectedBuildingId,
  visibleTiles,
  buildings,
  handleSelectCategory,
  handleSelectTile,
  handleToggleBuilding,
  useDistinctColors,
  setUseDistinctColors,
}: PaletteSectionProps) {
  return (
    <section className="space-y-2 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-2.5">
      <div className="flex items-center justify-between">
        <SectionTitle>Palette</SectionTitle>
        <label className="inline-flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-[0.04em] text-[var(--text-muted)]">
          <input
            type="checkbox"
            checked={useDistinctColors}
            onChange={(event) => setUseDistinctColors(event.target.checked)}
            className="h-3.5 w-3.5 rounded border border-[var(--border-soft)] bg-[var(--surface-muted)] accent-[var(--accent)]"
          />
          Distinct
        </label>
      </div>
      <div className="flex flex-wrap gap-1.5">
        {TILE_CATEGORIES.map((category) => (
          <button
            type="button"
            key={category.id}
            onClick={() => handleSelectCategory(category.id)}
            className={[
              'inline-flex h-6 items-center rounded border px-2 text-[11px] font-semibold',
              selectedCategory === category.id
                ? 'border-[var(--accent-strong)] bg-[var(--accent)] text-white'
                : 'border-[var(--border-soft)] bg-[var(--surface-muted)] text-[var(--text-muted)] hover:border-[var(--border-strong)] hover:text-[var(--text-main)]',
            ].join(' ')}
          >
            {category.label}
          </button>
        ))}
      </div>
      <div className="grid grid-cols-2 gap-1.5 rounded border border-[var(--border-soft)] bg-[var(--surface-muted)] p-1.5">
        {selectedCategory === 'buildings'
          ? buildings.map((building) => (
              <button
                type="button"
                key={building.id}
                className={[
                  'col-span-2 flex min-h-9 items-center justify-between rounded border bg-[var(--surface)] px-2 text-left text-[11px] font-semibold text-[var(--text-main)]',
                  selectedBuildingId === building.id
                    ? 'border-[var(--accent-strong)] ring-2 ring-[var(--focus-ring)]'
                    : 'border-[var(--border-soft)] hover:border-[var(--border-strong)]',
                ].join(' ')}
                onClick={() => handleToggleBuilding(building.id)}
              >
                <span className="truncate">{building.name}</span>
                <span className="ml-2 text-[10px] text-[var(--text-muted)]">
                  {building.width}x{building.height}
                </span>
              </button>
            ))
          : visibleTiles.map((tile) => {
              const tileColor = getDisplayTileColor(tile, DISTINCT_TILE_COLOR_MAP, useDistinctColors)

              return (
                <button
                  type="button"
                  key={tile.id}
                  className={[
                    'flex min-h-9 items-center gap-2 rounded border px-2 text-left text-[11px] font-semibold text-[#f4faf7] [text-shadow:0_1px_1px_rgba(0,0,0,0.55)]',
                    tile.id === selectedTileId
                      ? 'border-[var(--accent-strong)] ring-2 ring-[var(--focus-ring)]'
                      : 'border-[var(--border-soft)] hover:border-[var(--border-strong)]',
                  ].join(' ')}
                  style={{
                    backgroundImage: `linear-gradient(rgba(0,0,0,0.24), rgba(0,0,0,0.24)), linear-gradient(${tileColor}, ${tileColor})`,
                  }}
                  onClick={() => handleSelectTile(tile.id)}
                >
                  <span className="truncate">{tile.name}</span>
                </button>
              )
            })}
      </div>
    </section>
  )
}

export default PaletteSection
