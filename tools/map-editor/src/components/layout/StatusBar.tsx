import Badge from '../common/Badge'

function StatusBar() {
  return (
    <footer className="border-t border-[var(--border-soft)] bg-[var(--surface)] px-3 py-2 text-xs text-[var(--text-muted)] md:px-4">
      <div className="mx-auto flex w-full max-w-[1400px] flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-3">
          <Badge tone="neutral">Layer: Base</Badge>
          <span>Pointer: -, -</span>
        </div>
        <span>Ready for Phase 2 editor features</span>
      </div>
    </footer>
  )
}

export default StatusBar
