import Badge from '../common/Badge'
import Panel from '../common/Panel'

function RightSidebar() {
  return (
    <aside>
      <Panel
        title="Inspector"
        subtitle="Tile details and map metadata"
        className="h-full min-h-[220px] md:min-h-0"
        actions={<Badge tone="warn">Stub</Badge>}
      >
        <div className="space-y-3 text-sm">
          <div className="rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3">
            <p className="font-semibold text-[var(--text-main)]">Selection</p>
            <p className="mt-1 text-[var(--text-muted)]">No tile selected yet.</p>
          </div>
          <div className="rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3">
            <p className="font-semibold text-[var(--text-main)]">Metadata</p>
            <p className="mt-1 text-[var(--text-muted)]">Map info and properties will be shown here.</p>
          </div>
        </div>
      </Panel>
    </aside>
  )
}

export default RightSidebar
