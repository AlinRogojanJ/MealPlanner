import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import type { DayPlanDto, MemberDto } from '../../api/types'
import { formatDayHeader, isToday } from '../../lib/dates'
import { memberColor } from '../../lib/memberColors'
import { MacroBar } from '../../components/MacroBar'
import { MealCard } from './MealCard'

interface DayColumnProps {
  day: DayPlanDto
  members: MemberDto[]
  onAddMeal: (date: string) => void
}

/** One weekday: header, running totals per person, and the day's meal cards.
 *  Accepts meal cards dragged from other days (keeps the slot, re-solves portions). */
export function DayColumn({ day, members, onAddMeal }: DayColumnProps) {
  const { weekday, date } = formatDayHeader(day.date)
  const today = isToday(day.date)
  const [dragOver, setDragOver] = useState(false)
  const queryClient = useQueryClient()

  const move = useMutation({
    mutationFn: ({ mealId, slotType }: { mealId: string; slotType: string }) =>
      api.moveMeal(mealId, day.date, slotType),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans'] })
      queryClient.invalidateQueries({ queryKey: ['plans'] })
    },
  })

  return (
    <div
      onDragOver={(e) => {
        if (e.dataTransfer.types.includes('text/meal-id')) {
          e.preventDefault()
          e.dataTransfer.dropEffect = 'move'
          setDragOver(true)
        }
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e) => {
        e.preventDefault()
        setDragOver(false)
        const mealId = e.dataTransfer.getData('text/meal-id')
        const slotType = e.dataTransfer.getData('text/slot-type') || 'Dinner'
        if (mealId) move.mutate({ mealId, slotType })
      }}
      className={`flex w-64 shrink-0 flex-col rounded-xl border p-2 transition-colors ${
        dragOver
          ? 'border-indigo-400 bg-indigo-100/60'
          : today
            ? 'border-indigo-300 bg-indigo-50/50'
            : 'border-slate-200 bg-slate-50'
      }`}
    >
      <div className="mb-2 flex items-baseline justify-between px-1">
        <span className={`text-sm font-bold ${today ? 'text-indigo-700' : 'text-slate-700'}`}>{weekday}</span>
        <span className="text-xs text-slate-400">{date}</span>
        {today && <span className="rounded-full bg-indigo-600 px-2 py-0.5 text-[10px] font-semibold text-white">Today</span>}
      </div>

      {/* Running daily totals vs target, with over/under indicator */}
      <div className="mb-2 space-y-1.5 rounded-lg border border-slate-200 bg-white p-2">
        {day.totals.map((total) => {
          const idx = members.findIndex((m) => m.userId === total.userId)
          const member = members[idx]
          const color = memberColor(idx)
          const over = total.deltaKcal > 0
          return (
            <div key={total.userId}>
              <div className="mb-0.5 flex items-center justify-between text-[11px]">
                <span className={`font-medium ${color.text}`}>{member?.displayName}</span>
                <span className="tabular-nums text-slate-500">
                  {Math.round(total.consumedKcal)} / {total.targetKcal} kcal{' '}
                  <span className={over ? 'font-semibold text-red-600' : 'font-semibold text-emerald-600'}>
                    ({over ? '+' : ''}{Math.round(total.deltaKcal)})
                  </span>
                </span>
              </div>
              <MacroBar consumed={total.consumedKcal} target={total.targetKcal} colorClass={color.bar} />
            </div>
          )
        })}
      </div>

      <div className="flex flex-col gap-2">
        {day.meals.map((meal) => (
          <MealCard key={meal.id} meal={meal} members={members} />
        ))}
        <button
          onClick={() => onAddMeal(day.date)}
          className="rounded-lg border border-dashed border-slate-300 py-1.5 text-xs font-medium text-slate-400 transition-colors hover:border-indigo-300 hover:text-indigo-500"
        >
          + Add dish
        </button>
      </div>
    </div>
  )
}
