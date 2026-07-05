import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
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

/** One dish in a calendar slot: recipe name + each person's portion and macros.
 *  Skipping an eater re-solves the dish for whoever's left (edge case §6).
 *  Drag the card onto another day to move it. */
export function MealCard({ meal, members }: MealCardProps) {
  const queryClient = useQueryClient()
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans'] })
    queryClient.invalidateQueries({ queryKey: ['plans'] }) // grocery list totals change too
  }
  const solve = useMutation({
    mutationFn: (skippedUserIds: string[]) => api.solveMeal(meal.id, skippedUserIds),
    onSuccess: invalidate,
  })
  const remove = useMutation({
    mutationFn: () => api.deleteMeal(meal.id),
    onSuccess: invalidate,
  })

  const eatingUserIds = meal.portions.map((p) => p.userId)
  const skippedMembers = members.filter((m) => !eatingUserIds.includes(m.userId))

  const skip = (userId: string) =>
    solve.mutate([...skippedMembers.map((m) => m.userId), userId])

  const busy = solve.isPending || remove.isPending

  return (
    <div
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData('text/meal-id', meal.id)
        e.dataTransfer.setData('text/slot-type', meal.slotType)
        e.dataTransfer.effectAllowed = 'move'
      }}
      className={`group/card cursor-grab rounded-lg border border-slate-200 bg-white p-2.5 shadow-sm transition-shadow hover:shadow-md active:cursor-grabbing ${busy ? 'opacity-60' : ''}`}
    >
      <div className="mb-1.5 flex items-center justify-between gap-2">
        <span className={`rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${SLOT_STYLES[meal.slotType]}`}>
          {meal.slotType}
        </span>
        <span className="flex items-center gap-1">
          {skippedMembers.length > 0 && (
            <button
              onClick={() => solve.mutate([])}
              disabled={busy}
              title="Re-solve portions for everyone"
              className="rounded px-1.5 py-0.5 text-[10px] font-semibold text-indigo-500 hover:bg-indigo-50"
            >
              + everyone
            </button>
          )}
          <button
            onClick={() => remove.mutate()}
            disabled={busy}
            title="Remove this dish"
            className="hidden rounded px-1 py-0.5 text-[11px] font-bold text-slate-300 hover:bg-red-50 hover:text-red-500 group-hover/card:inline"
          >
            ✕
          </button>
        </span>
      </div>
      <p className="mb-2 text-sm font-semibold leading-snug text-slate-800">{meal.recipeName}</p>
      <div className="space-y-1.5">
        {meal.portions.map((portion) => {
          const idx = members.findIndex((m) => m.userId === portion.userId)
          const member = members[idx]
          const color = memberColor(idx)
          return (
            <div key={portion.userId} className={`group rounded-md px-2 py-1.5 ${color.chipBg}`}>
              <div className="flex items-center justify-between gap-2">
                <span className={`flex items-center gap-1.5 text-xs font-medium ${color.text}`}>
                  <span className={`h-1.5 w-1.5 rounded-full ${color.dot}`} />
                  {member?.displayName ?? '—'}
                </span>
                <span className="flex items-center gap-1">
                  <span className="text-xs font-semibold tabular-nums text-slate-700">
                    {Math.round(portion.kcal)} kcal
                  </span>
                  {meal.portions.length > 1 && (
                    <button
                      onClick={() => skip(portion.userId)}
                      disabled={solve.isPending}
                      title={`${member?.displayName} skips this meal — re-solve for the rest`}
                      className="hidden rounded px-1 text-[10px] font-bold text-slate-400 hover:bg-white hover:text-red-500 group-hover:inline"
                    >
                      skip
                    </button>
                  )}
                </span>
              </div>
              <p className="mt-0.5 text-[11px] leading-snug text-slate-500">{portion.summary}</p>
              <p className="mt-0.5 text-[10px] tabular-nums text-slate-400">
                P {Math.round(portion.proteinG)}g · C {Math.round(portion.carbsG)}g · F {Math.round(portion.fatG)}g
              </p>
            </div>
          )
        })}
        {skippedMembers.map((member) => (
          <div key={member.userId} className="rounded-md bg-slate-50 px-2 py-1 text-[11px] text-slate-400">
            <span className="line-through">{member.displayName}</span> skips this meal
          </div>
        ))}
      </div>
    </div>
  )
}
