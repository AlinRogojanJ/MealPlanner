import type { SuggestionDto } from '../../api/types'
import { useAcceptSuggestion, useDismissSuggestion } from './useLogging'

interface SuggestionCardProps {
  suggestion: SuggestionDto
  userId: string
}

/** A pending recalc proposal: shrink later meals to absorb the overage. Accept in one tap or dismiss. */
export function SuggestionCard({ suggestion, userId }: SuggestionCardProps) {
  const accept = useAcceptSuggestion(userId)
  const dismiss = useDismissSuggestion(userId)
  const busy = accept.isPending || dismiss.isPending

  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <p className="text-sm font-semibold text-slate-800">
          Get back on track — {suggestion.date}
        </p>
        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold uppercase text-amber-700">
          +{suggestion.overageKcal} kcal over
        </span>
      </div>

      {suggestion.adjustments.length > 0 && (
        <ul className="mb-2 space-y-1">
          {suggestion.adjustments.map((adj) => (
            <li
              key={adj.plannedMealId}
              className="flex items-center justify-between rounded-lg bg-white px-3 py-1.5 text-xs"
            >
              <span className="text-slate-600">
                <span className="font-medium text-slate-700">{adj.recipeName}</span>
                <span className="text-slate-400"> · {adj.slotType}</span>
              </span>
              <span className="tabular-nums">
                <span className="text-slate-400 line-through">{adj.oldKcal}</span>
                <span className="mx-1 text-slate-300">→</span>
                <span className="font-semibold text-slate-700">{adj.newKcal} kcal</span>
              </span>
            </li>
          ))}
        </ul>
      )}

      <p className="mb-3 text-xs text-slate-500">
        {suggestion.unabsorbedKcal > 0 ? (
          <>
            Later meals can absorb {suggestion.absorbedKcal} kcal;{' '}
            <span className="font-semibold text-red-600">{suggestion.unabsorbedKcal} kcal can't be fixed today</span>{' '}
            without unhealthy cuts.
          </>
        ) : (
          <>Shrinking the meals above absorbs the full {suggestion.absorbedKcal} kcal.</>
        )}
      </p>

      <div className="flex gap-2">
        <button
          disabled={busy || suggestion.adjustments.length === 0}
          onClick={() => accept.mutate(suggestion.id)}
          className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {accept.isPending ? 'Applying…' : 'Accept adjustments'}
        </button>
        <button
          disabled={busy}
          onClick={() => dismiss.mutate(suggestion.id)}
          className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50"
        >
          Dismiss
        </button>
      </div>
    </div>
  )
}
