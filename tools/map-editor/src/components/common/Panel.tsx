import type { HTMLAttributes, ReactNode } from 'react'

interface PanelProps extends HTMLAttributes<HTMLDivElement> {
  title?: string
  subtitle?: string
  actions?: ReactNode
}

function Panel({ title, subtitle, actions, className, children, ...props }: PanelProps) {
  const classes = [
    'rounded-lg border border-[var(--border-soft)] bg-[var(--surface)] shadow-[0_1px_2px_rgba(17,34,27,0.08)]',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <section className={classes} {...props}>
      {(title ?? subtitle ?? actions) && (
        <header className="flex items-start justify-between gap-3 border-b border-[var(--surface-strong)] px-4 py-3">
          <div>
            {title && <h2 className="text-sm font-semibold text-[var(--text-main)]">{title}</h2>}
            {subtitle && <p className="mt-1 text-xs text-[var(--text-muted)]">{subtitle}</p>}
          </div>
          {actions && <div className="shrink-0">{actions}</div>}
        </header>
      )}
      <div className="p-4">{children}</div>
    </section>
  )
}

export default Panel
