import type { HTMLAttributes } from 'react'

type BadgeTone = 'neutral' | 'success' | 'warn'

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone
}

const toneClasses: Record<BadgeTone, string> = {
  neutral: 'border-[var(--border-soft)] bg-[var(--surface-muted)] text-[var(--text-main)]',
  success: 'border-[#68a87f] bg-[#e5f3ea] text-[var(--status-ok)]',
  warn: 'border-[#d4a15e] bg-[#fcf0df] text-[var(--status-warn)]',
}

function Badge({ tone = 'neutral', className, children, ...props }: BadgeProps) {
  const classes = [
    'inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-semibold uppercase tracking-wide',
    toneClasses[tone],
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <span className={classes} {...props}>
      {children}
    </span>
  )
}

export default Badge
