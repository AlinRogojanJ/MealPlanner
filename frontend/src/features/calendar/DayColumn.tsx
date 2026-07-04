import type { DayPlanDto, MemberDto } from '../../api/types'
import { formatDayHeader, isToday } from '../../lib/dates'
import { memberColor } from '../../lib/memberColors'
import { MacroBar } from '../../components/MacroBar'
import { MealCard } from './MealCard'

interface DayColumnProps {
  day: DayPlanDto
  members: MemberDto[]
}

/** One weekday: header, running totals per person, and the day's meal cards. */
export function DayColumn({ day, members }: DayColumnProps) {
  const { weekday, date } = formatDayHeader(day.date)
  const today = isToday(day.date)

  return (
    <div className={`flex w-64 shrink-0 flex-col rounded-xl border p-2 ${today ? 'border-indigo-300 bg-indigo-50/50' : 'border-slate-200 bg-slate-50'}`}>
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
      </div>
    </div>
  )
}
