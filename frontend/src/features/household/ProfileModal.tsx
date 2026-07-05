import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import type { MemberDto } from '../../api/types'

const DIET_TYPES = ['Cut', 'Maintain', 'Bulk'] as const

interface ProfileModalProps {
  member: MemberDto
  onClose: () => void
}

/** Edit a member's targets — creates a new versioned profile row (§3.2).
 *  Portions already planned keep their old split until a meal is re-solved. */
export function ProfileModal({ member, onClose }: ProfileModalProps) {
  const [calorieTarget, setCalorieTarget] = useState(String(member.calorieTarget))
  const [proteinG, setProteinG] = useState(String(member.proteinG))
  const [carbsG, setCarbsG] = useState(String(member.carbsG))
  const [fatG, setFatG] = useState(String(member.fatG))
  const [dietType, setDietType] = useState<string>(member.dietType)
  const queryClient = useQueryClient()

  const save = useMutation({
    mutationFn: () =>
      api.updateProfile(member.userId, {
        calorieTarget: Number(calorieTarget),
        proteinG: Number(proteinG),
        carbsG: Number(carbsG),
        fatG: Number(fatG),
        dietType,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID] })
      onClose()
    },
  })

  const inputCls =
    'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 focus:border-indigo-400 focus:outline-none'
  const valid = Number(calorieTarget) >= 800 && Number(calorieTarget) <= 8000

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4" onClick={onClose}>
      <div className="w-full max-w-sm rounded-2xl bg-white p-5 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-base font-bold text-slate-800">{member.displayName}'s targets</h3>
          <button onClick={onClose} className="rounded-lg p-1 text-slate-400 hover:bg-slate-100">✕</button>
        </div>

        <div className="space-y-3">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-500">Diet type</label>
            <div className="flex gap-1.5">
              {DIET_TYPES.map((diet) => (
                <button
                  key={diet}
                  onClick={() => setDietType(diet)}
                  className={`flex-1 rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${
                    dietType === diet ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  {diet}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-500">Daily calories (kcal)</label>
            <input type="number" value={calorieTarget} onChange={(e) => setCalorieTarget(e.target.value)} className={inputCls} />
          </div>

          <div className="grid grid-cols-3 gap-2">
            {(
              [
                ['Protein g', proteinG, setProteinG],
                ['Carbs g', carbsG, setCarbsG],
                ['Fat g', fatG, setFatG],
              ] as const
            ).map(([label, value, setter]) => (
              <div key={label}>
                <label className="mb-1 block text-xs font-medium text-slate-500">{label}</label>
                <input type="number" min="0" value={value} onChange={(e) => setter(e.target.value)} className={inputCls} />
              </div>
            ))}
          </div>

          <p className="text-[11px] leading-relaxed text-slate-400">
            Saving creates a new profile version — days already planned keep the targets they were
            planned against. Re-solve a meal (skip → + everyone) to apply the new targets to it.
          </p>

          <button
            disabled={!valid || save.isPending}
            onClick={() => save.mutate()}
            className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {save.isPending ? 'Saving…' : 'Save targets'}
          </button>
          {save.isError && (
            <p className="text-xs text-red-600">Couldn't save — {String(save.error)}</p>
          )}
        </div>
      </div>
    </div>
  )
}
