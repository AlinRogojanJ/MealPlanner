import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import { useRecipes } from '../recipes/useRecipes'
import { formatDayHeader } from '../../lib/dates'

const SLOTS = ['Breakfast', 'Lunch', 'Dinner', 'Snack'] as const

interface AddMealModalProps {
  planId: string
  date: string // preselected day (yyyy-MM-dd)
  onClose: () => void
}

/** Pick a recipe + slot → POST /plans/{id}/meals → solver splits portions for everyone. */
export function AddMealModal({ planId, date, onClose }: AddMealModalProps) {
  const { data: recipes } = useRecipes()
  const [slotType, setSlotType] = useState<(typeof SLOTS)[number]>('Dinner')
  const [recipeId, setRecipeId] = useState<string>()
  const queryClient = useQueryClient()

  const addMeal = useMutation({
    mutationFn: () => api.addMeal(planId, { date, slotType, recipeId: recipeId! }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans'] })
      queryClient.invalidateQueries({ queryKey: ['plans', planId, 'grocery-list'] })
      onClose()
    },
  })

  const { weekday, date: dayLabel } = formatDayHeader(date)

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-2xl bg-white p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-base font-bold text-slate-800">
            Add dish — {weekday} {dayLabel}
          </h3>
          <button onClick={onClose} className="rounded-lg p-1 text-slate-400 hover:bg-slate-100">
            ✕
          </button>
        </div>

        <label className="mb-1 block text-xs font-medium text-slate-500">Meal slot</label>
        <div className="mb-4 flex gap-1.5">
          {SLOTS.map((slot) => (
            <button
              key={slot}
              onClick={() => setSlotType(slot)}
              className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${
                slotType === slot
                  ? 'bg-indigo-600 text-white'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              {slot}
            </button>
          ))}
        </div>

        <label className="mb-1 block text-xs font-medium text-slate-500">Recipe</label>
        <div className="mb-4 max-h-64 space-y-1.5 overflow-y-auto pr-1">
          {(recipes ?? []).map((recipe) => (
            <button
              key={recipe.id}
              onClick={() => setRecipeId(recipe.id)}
              className={`w-full rounded-lg border px-3 py-2 text-left text-sm transition-colors ${
                recipeId === recipe.id
                  ? 'border-indigo-400 bg-indigo-50 text-indigo-800'
                  : 'border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <span className="font-medium">{recipe.name}</span>
              <span className="ml-2 text-xs text-slate-400">
                {recipe.ingredients.length} ingredients
              </span>
            </button>
          ))}
        </div>

        <button
          disabled={!recipeId || addMeal.isPending}
          onClick={() => addMeal.mutate()}
          className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {addMeal.isPending ? 'Solving portions…' : 'Add & split portions'}
        </button>
        {addMeal.isError && (
          <p className="mt-2 text-xs text-red-600">
            Couldn't add — is the API running? ({String(addMeal.error)})
          </p>
        )}
      </div>
    </div>
  )
}
