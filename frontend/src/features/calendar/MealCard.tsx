import type { MemberDto, PlannedMealDto } from '../../api/types'
import { memberColor } from '../../lib/memberColors'

const SLOT_STYLES: Record<PlannedMealDto['slotType'], string> = {
  Breakfast: 'text-amber-600 bg-amber-50',
  Lunch: 'text-sky-600 bg-sky-50',
  Dinner: 'text-violet-600 bg-violet-50',
  Snack: 'text-emerald-600 bg-emerald-50',
}

interface MealCardProps {
  meal: PlannedMealDto
  members: MemberDto[]
}

/** One dish in a calendar slot: recipe name + each person's portion and macros. */
export function MealCard({ meal, members }: MealCardProps) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-2.5 shadow-sm transition-shadow hover:shadow-md">
      <div className="mb-1.5 flex items-center justify-between gap-2">
        <span className={`rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${SLOT_STYLES[meal.slotType]}`}>
          {meal.slotType}
        </span>
      </div>
      <p className="mb-2 text-sm font-semibold leading-snug text-slate-800">{meal.recipeName}</p>
      <div className="space-y-1.5">
        {meal.portions.map((portion) => {
          const idx = members.findIndex((m) => m.userId === portion.userId)
          const member = members[idx]
          const color = memberColor(idx)
          return (
            <div key={portion.userId} className={`rounded-md px-2 py-1.5 ${color.chipBg}`}>
              <div className="flex items-center justify-between gap-2">
                <span className={`flex items-center gap-1.5 text-xs font-medium ${color.text}`}>
                  <span className={`h-1.5 w-1.5 rounded-full ${color.dot}`} />
                  {member?.displayName ?? '—'}
                </span>
                <span className="text-xs font-semibold tabular-nums text-slate-700">
                  {Math.round(portion.kcal)} kcal
                </span>
              </div>
              <p className="mt-0.5 text-[11px] leading-snug text-slate-500">{portion.summary}</p>
              <p className="mt-0.5 text-[10px] tabular-nums text-slate-400">
                P {Math.round(portion.proteinG)}g · C {Math.round(portion.carbsG)}g · F {Math.round(portion.fatG)}g
              </p>
            </div>
          )
        })}
      </div>
    </div>
  )
}
