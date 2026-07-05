import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import { memberColor } from '../../lib/memberColors'
import { useLogFood, useLogsForDay, usePendingSuggestions } from './useLogging'
import { SuggestionCard } from './SuggestionCard'

const DEMO_WEEK_DATES = Array.from({ length: 7 }, (_, i) => {
  const d = new Date('2026-06-29T00:00:00')
  d.setDate(d.getDate() + i)
  return d.toISOString().slice(0, 10)
})

/** Off-plan logging (§5.4): log the random dessert, get a recalc suggestion for YOUR day only. */
export function LoggingPage() {
  const { data: household } = useQuery({
    queryKey: ['households', DEMO_HOUSEHOLD_ID],
    queryFn: () => api.getHousehold(DEMO_HOUSEHOLD_ID),
  })

  const members = household?.members ?? []
  const [selectedUserId, setSelectedUserId] = useState<string>()
  const userId = selectedUserId ?? members[0]?.userId
  const [date, setDate] = useState(DEMO_WEEK_DATES[3]) // Thursday of the demo week

  const [description, setDescription] = useState('')
  const [kcal, setKcal] = useState('')
  const [proteinG, setProteinG] = useState('')
  const [carbsG, setCarbsG] = useState('')
  const [fatG, setFatG] = useState('')

  const logFood = useLogFood()
  const { data: logs } = useLogsForDay(userId ?? '', date)
  const { data: suggestions } = usePendingSuggestions(userId ?? '')

  if (!userId) return <div className="p-10 text-center text-slate-400">Loading household…</div>

  const canSubmit = description.trim() !== '' && Number(kcal) > 0 && !logFood.isPending

  const submit = () => {
    logFood.mutate(
      {
        userId,
        date,
        description: description.trim(),
        kcal: Number(kcal),
        proteinG: Number(proteinG) || 0,
        carbsG: Number(carbsG) || 0,
        fatG: Number(fatG) || 0,
      },
      {
        onSuccess: () => {
          setDescription('')
          setKcal('')
          setProteinG('')
          setCarbsG('')
          setFatG('')
        },
      },
    )
  }

  const inputCls =
    'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 focus:border-indigo-400 focus:outline-none'

  return (
    <div className="mx-auto grid max-w-5xl gap-6 lg:grid-cols-2">
      {/* Left: the log form */}
      <div>
        <h2 className="text-lg font-bold text-slate-800">Log off-plan food</h2>
        <p className="mb-4 text-sm text-slate-500">
          The random dessert. Only this person's day gets recalculated — the partner's plan is untouched.
        </p>

        <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex gap-2">
            {members.map((member, idx) => {
              const color = memberColor(idx)
              const selected = member.userId === userId
              return (
                <button
                  key={member.userId}
                  onClick={() => setSelectedUserId(member.userId)}
                  className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ${
                    selected
                      ? `border-transparent ${color.chipBg} ${color.text}`
                      : 'border-slate-200 text-slate-500 hover:bg-slate-50'
                  }`}
                >
                  <span className={`h-2 w-2 rounded-full ${color.dot}`} />
                  {member.displayName}
                </button>
              )
            })}
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-500">Day (demo week)</label>
            <select value={date} onChange={(e) => setDate(e.target.value)} className={inputCls}>
              {DEMO_WEEK_DATES.map((d) => (
                <option key={d} value={d}>
                  {new Date(d + 'T00:00:00').toLocaleDateString('en-GB', {
                    weekday: 'long',
                    day: 'numeric',
                    month: 'short',
                  })}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-500">What did you eat?</label>
            <input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Chocolate cake slice"
              className={inputCls}
            />
          </div>

          <div className="grid grid-cols-4 gap-2">
            {(
              [
                ['kcal', kcal, setKcal],
                ['Protein g', proteinG, setProteinG],
                ['Carbs g', carbsG, setCarbsG],
                ['Fat g', fatG, setFatG],
              ] as const
            ).map(([label, value, setter]) => (
              <div key={label}>
                <label className="mb-1 block text-xs font-medium text-slate-500">{label}</label>
                <input
                  type="number"
                  min="0"
                  value={value}
                  onChange={(e) => setter(e.target.value)}
                  placeholder="0"
                  className={inputCls}
                />
              </div>
            ))}
          </div>

          <button
            disabled={!canSubmit}
            onClick={submit}
            className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {logFood.isPending ? 'Logging…' : 'Log it'}
          </button>

          {logFood.isError && (
            <p className="text-xs text-red-600">
              Couldn't log — is the API running? ({String(logFood.error)})
            </p>
          )}
        </div>

        {/* Today's log for the selected person/day */}
        {logs && logs.length > 0 && (
          <div className="mt-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <h3 className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">
              Logged on {date}
            </h3>
            <ul className="divide-y divide-slate-100">
              {logs.map((log) => (
                <li key={log.id} className="flex items-center justify-between py-2 text-sm">
                  <span className="text-slate-700">{log.description}</span>
                  <span className="tabular-nums text-slate-500">
                    {Math.round(log.kcal)} kcal · P{Math.round(log.proteinG)} C{Math.round(log.carbsG)} F{Math.round(log.fatG)}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      {/* Right: pending suggestions */}
      <div>
        <h2 className="text-lg font-bold text-slate-800">Suggestions</h2>
        <p className="mb-4 text-sm text-slate-500">
          Rules-based in v1: later meals shrink (never below 60%) to absorb the overage.
        </p>
        {suggestions && suggestions.length > 0 ? (
          <div className="space-y-3">
            {suggestions.map((suggestion) => (
              <SuggestionCard key={suggestion.id} suggestion={suggestion} userId={userId} />
            ))}
          </div>
        ) : (
          <div className="rounded-xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-400">
            Nothing pending. Log something off-plan and the recalc proposal appears here.
          </div>
        )}
      </div>
    </div>
  )
}
