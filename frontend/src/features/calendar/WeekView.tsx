import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import type { MemberDto } from '../../api/types'
import { useWeekPlan } from './useWeekPlan'
import { formatWeekRange, shiftWeek } from '../../lib/dates'
import { memberColor } from '../../lib/memberColors'
import { useUiStore } from '../../stores/uiStore'
import { DayColumn } from './DayColumn'
import { AddMealModal } from './AddMealModal'
import { ProfileModal } from '../household/ProfileModal'

const DEMO_WEEK = '2026-06-29' // week seeded in the backend store

/** The main page: weekly calendar with per-person portions and running totals. */
export function WeekView() {
  const { data: plan, isLoading } = useWeekPlan()
  const selectedWeek = useUiStore((s) => s.selectedWeek)
  const setSelectedWeek = useUiStore((s) => s.setSelectedWeek)
  const [addMealDate, setAddMealDate] = useState<string>()
  const [editMember, setEditMember] = useState<MemberDto>()
  const queryClient = useQueryClient()

  const createWeek = useMutation({
    mutationFn: (copyFrom?: string) => api.createWeekPlan(DEMO_HOUSEHOLD_ID, selectedWeek, copyFrom),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans'] }),
  })

  const weekNav = (
    <div className="flex items-center gap-1">
      <button
        onClick={() => setSelectedWeek(shiftWeek(selectedWeek, -1))}
        className="rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm text-slate-600 hover:bg-slate-50"
        title="Previous week"
      >
        ←
      </button>
      <button
        onClick={() => setSelectedWeek(DEMO_WEEK)}
        disabled={selectedWeek === DEMO_WEEK}
        className="rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-40"
      >
        Demo week
      </button>
      <button
        onClick={() => setSelectedWeek(shiftWeek(selectedWeek, 1))}
        className="rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm text-slate-600 hover:bg-slate-50"
        title="Next week"
      >
        →
      </button>
    </div>
  )

  if (isLoading) {
    return <div className="p-10 text-center text-slate-400">Loading week…</div>
  }

  if (!plan) {
    return (
      <div className="flex h-full flex-col">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold text-slate-800">Week of {formatWeekRange(selectedWeek)}</h2>
            <p className="text-sm text-slate-500">One dish, everyone's own portion.</p>
          </div>
          {weekNav}
        </div>
        <div className="rounded-xl border border-dashed border-slate-300 p-16 text-center">
          <p className="mb-4 text-sm text-slate-400">No plan for this week yet.</p>
          <div className="flex items-center justify-center gap-2">
            <button
              onClick={() => createWeek.mutate(undefined)}
              disabled={createWeek.isPending}
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {createWeek.isPending ? 'Creating…' : 'Start from scratch'}
            </button>
            <button
              onClick={() => createWeek.mutate(shiftWeek(selectedWeek, -1))}
              disabled={createWeek.isPending}
              title="Copy last week's menu, re-solved against current targets"
              className="rounded-lg border border-indigo-200 bg-white px-4 py-2 text-sm font-semibold text-indigo-600 hover:bg-indigo-50 disabled:opacity-50"
            >
              Copy previous week
            </button>
          </div>
          {createWeek.isError && (
            <p className="mt-3 text-xs text-red-600">
              Couldn't create — is the API running? ({String(createWeek.error)})
            </p>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="flex h-full flex-col">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-slate-800">Week of {formatWeekRange(plan.weekStartDate)}</h2>
          <p className="text-sm text-slate-500">One dish, everyone's own portion.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {/* Member legend with targets */}
          <div className="flex gap-2">
            {plan.members.map((member, idx) => {
              const color = memberColor(idx)
              return (
                <button
                  key={member.userId}
                  onClick={() => setEditMember(member)}
                  title="Edit targets"
                  className={`flex items-center gap-2 rounded-lg px-3 py-1.5 text-left transition-shadow hover:shadow ${color.chipBg}`}
                >
                  <span className={`h-2 w-2 rounded-full ${color.dot}`} />
                  <div className="leading-tight">
                    <p className={`text-sm font-semibold ${color.text}`}>{member.displayName}</p>
                    <p className="text-[11px] text-slate-500">
                      {member.calorieTarget} kcal · {member.dietType} · P{member.proteinG} C{member.carbsG} F{member.fatG}
                    </p>
                  </div>
                </button>
              )
            })}
          </div>
          {weekNav}
        </div>
      </div>

      <div className="flex gap-3 overflow-x-auto pb-4">
        {plan.days.map((day) => (
          <DayColumn key={day.date} day={day} members={plan.members} onAddMeal={setAddMealDate} />
        ))}
      </div>

      {addMealDate && (
        <AddMealModal planId={plan.planId} date={addMealDate} onClose={() => setAddMealDate(undefined)} />
      )}

      {editMember && <ProfileModal member={editMember} onClose={() => setEditMember(undefined)} />}
    </div>
  )
}
