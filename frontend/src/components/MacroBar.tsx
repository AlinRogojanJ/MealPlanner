interface MacroBarProps {
  consumed: number
  target: number
  colorClass: string // tailwind bg-* class for the fill
}

/** Progress bar of consumed kcal vs target, turns red when over. */
export function MacroBar({ consumed, target, colorClass }: MacroBarProps) {
  const pct = target > 0 ? Math.min((consumed / target) * 100, 100) : 0
  const over = consumed > target
  return (
    <div className="h-1.5 w-full rounded-full bg-slate-200">
      <div
        className={`h-1.5 rounded-full transition-all ${over ? 'bg-red-500' : colorClass}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
