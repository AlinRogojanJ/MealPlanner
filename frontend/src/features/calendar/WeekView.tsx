import { useWeekPlan } from './useWeekPlan'
import { formatWeekRange } from '../../lib/dates'
import { memberColor } from '../../lib/memberColors'
import { DayColumn } from './DayColumn'

/** The main page: weekly calendar with per-person portions and running totals. */
export function WeekView() {
  const { data: plan, isLoading } = useWeekPlan()

  if (isLoading) {
    return <div className="p-10 text-center text-slate-400">Loading week…</div>
  }
  if (!plan) {
    return <div className="p-10 text-center text-slate-400">No plan for this week yet.</div>
  }

  return (
    <div className="flex h-full flex-col">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-slate-800">Week of {formatWeekRange(plan.weekStartDate)}</h2>
          <p className="text-sm text-slate-500">One dish, everyone's own portion.</p>
        </div>
        {/* Member legend with targets */}
        <div className="flex gap-2">
          {plan.members.map((member, idx) => {
            const color = memberColor(idx)
            return (
              <div key={member.userId} className={`flex items-center gap-2 rounded-lg px-3 py-1.5 ${color.chipBg}`}>
                <span className={`h-2 w-2 rounded-full ${color.dot}`} />
                <div className="leading-tight">
                  <p className={`text-sm font-semibold ${color.text}`}>{member.displayName}</p>
                  <p className="text-[11px] text-slate-500">
                    {member.calorieTarget} kcal · {member.dietType} · P{member.proteinG} C{member.carbsG} F{member.fatG}
                  </p>
                </div>
              </div>
            )
          })}
        </div>
      </div>

      <div className="flex gap-3 overflow-x-auto pb-4">
        {plan.days.map((day) => (
          <DayColumn key={day.date} day={day} members={plan.members} />
        ))}
      </div>
    </div>
  )
}
