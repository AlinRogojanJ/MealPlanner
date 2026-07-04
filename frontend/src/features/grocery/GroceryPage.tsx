import { useMemo, useState } from 'react'
import type { GroceryItemDto } from '../../api/types'
import { formatWeekRange } from '../../lib/dates'
import { useGroceryList } from './useGroceryList'

function formatQuantity(grams: number): string {
  return grams >= 1000 ? `${(grams / 1000).toFixed(2).replace(/\.?0+$/, '')} kg` : `${Math.round(grams)} g`
}

function asPlainText(items: GroceryItemDto[], weekRange: string): string {
  const byAisle = new Map<string, GroceryItemDto[]>()
  for (const item of items) {
    byAisle.set(item.aisle, [...(byAisle.get(item.aisle) ?? []), item])
  }
  const lines = [`MacroSync grocery list — week of ${weekRange}`, '']
  for (const [aisle, aisleItems] of byAisle) {
    lines.push(`${aisle}:`)
    for (const item of aisleItems) lines.push(`  - ${item.name} — ${formatQuantity(item.totalQuantityG)}`)
    lines.push('')
  }
  return lines.join('\n')
}

/** Weekly grocery list aggregated across everyone's portions, grouped by aisle. */
export function GroceryPage() {
  const { data: list, isLoading } = useGroceryList()
  const [checked, setChecked] = useState<Set<string>>(new Set())
  const [copied, setCopied] = useState(false)

  const aisles = useMemo(() => {
    if (!list) return []
    const groups = new Map<string, GroceryItemDto[]>()
    for (const item of list.items) {
      groups.set(item.aisle, [...(groups.get(item.aisle) ?? []), item])
    }
    return [...groups.entries()]
  }, [list])

  if (isLoading || !list) {
    return <div className="p-10 text-center text-slate-400">Loading grocery list…</div>
  }

  const weekRange = formatWeekRange(list.weekStartDate)

  const toggle = (name: string) => {
    setChecked((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  const copyList = async () => {
    await navigator.clipboard.writeText(asPlainText(list.items, weekRange))
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-slate-800">Grocery list</h2>
          <p className="text-sm text-slate-500">
            Week of {weekRange} · everyone's portions summed · {checked.size}/{list.items.length} checked
          </p>
        </div>
        <div className="flex gap-2 print:hidden">
          <button
            onClick={copyList}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            {copied ? 'Copied ✓' : 'Copy as text'}
          </button>
          <button
            onClick={() => window.print()}
            className="rounded-lg bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
          >
            Print
          </button>
        </div>
      </div>

      <div className="space-y-4">
        {aisles.map(([aisle, items]) => (
          <div key={aisle} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <h3 className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">{aisle}</h3>
            <ul className="divide-y divide-slate-100">
              {items.map((item) => {
                const done = checked.has(item.name)
                return (
                  <li key={item.name}>
                    <label className="flex cursor-pointer items-center justify-between gap-3 py-2">
                      <span className="flex items-center gap-3">
                        <input
                          type="checkbox"
                          checked={done}
                          onChange={() => toggle(item.name)}
                          className="h-4 w-4 accent-indigo-600 print:hidden"
                        />
                        <span className={`text-sm ${done ? 'text-slate-300 line-through' : 'text-slate-700'}`}>
                          {item.name}
                        </span>
                      </span>
                      <span className={`text-sm tabular-nums ${done ? 'text-slate-300' : 'font-medium text-slate-500'}`}>
                        {formatQuantity(item.totalQuantityG)}
                      </span>
                    </label>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </div>
    </div>
  )
}
