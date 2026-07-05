import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import { useRecipes } from '../recipes/useRecipes'
import { formatDayHeader } from '../../lib/dates'

const SLOTS = ['Breakfast', 'Lunch', 'Dinner', 'Snack'] as const

interface AddMealModalProps {
  planId: string
  date: string // preselected day (yyyy-MM-dd)
  onClose: () => void
}

/** Pick a recipe + slot → POST /plans/{id}/meals → solver splits portions for everyone.
 *  "Suggest for us" ranks dishes that fit everyone's remaining targets (AI when configured). */
export function AddMealModal({ planId, date, onClose }: AddMealModalProps) {
  const { data: recipes } = useRecipes()
  const [slotType, setSlotType] = useState<(typeof SLOTS)[number]>('Dinner')
  const [recipeId, setRecipeId] = useState<string>()
  const [showSuggestions, setShowSuggestions] = useState(false)
  const queryClient = useQueryClient()

  const suggestions = useQuery({
    queryKey: ['plans', planId, 'recommendations', date, slotType],
    queryFn: () => api.getRecommendations(planId, date, slotType),
    enabled: showSuggestions,
  })

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

        <div className="mb-1 flex items-center justify-between">
          <label className="block text-xs font-medium text-slate-500">Recipe</label>
          <button
            onClick={() => setShowSuggestions((s) => !s)}
            className={`rounded-lg px-2 py-1 text-xs font-semibold transition-colors ${
              showSuggestions
                ? 'bg-violet-100 text-violet-700'
                : 'bg-violet-50 text-violet-600 hover:bg-violet-100'
            }`}
          >
            ✨ Suggest for us
          </button>
        </div>

        {showSuggestions && (
          <div className="mb-3 space-y-1.5 rounded-xl border border-violet-200 bg-violet-50/50 p-2">
            {suggestions.isLoading && (
              <p className="px-2 py-3 text-center text-xs text-slate-400">
                Finding dishes that fit everyone's targets…
              </p>
            )}
            {suggestions.data?.map((rec) => (
              <button
                key={rec.recipeId}
                onClick={() => setRecipeId(rec.recipeId)}
                className={`w-full rounded-lg border px-3 py-2 text-left transition-colors ${
                  recipeId === rec.recipeId
                    ? 'border-violet-400 bg-white'
                    : 'border-transparent bg-white/70 hover:bg-white'
                }`}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-semibold text-slate-800">{rec.recipeName}</span>
                  <span
                    className={`shrink-0 rounded-full px-1.5 py-0.5 text-[9px] font-bold uppercase ${
                      rec.source === 'AI'
                        ? 'bg-violet-100 text-violet-700'
                        : 'bg-slate-100 text-slate-500'
                    }`}
                  >
                    {rec.source === 'AI' ? '✨ AI' : 'best fit'}
                  </span>
                </div>
                <p className="mt-0.5 text-[11px] leading-snug text-slate-500">{rec.reason}</p>
                <p className="mt-0.5 text-[10px] tabular-nums text-slate-400">
                  {rec.portions.map((p) => `${Math.round(p.kcal)} kcal`).join(' / ')}
                </p>
              </button>
            ))}
            {suggestions.data?.length === 0 && !suggestions.isLoading && (
              <p className="px-2 py-3 text-center text-xs text-slate-400">No suggestions available.</p>
            )}
          </div>
        )}

        <div className="mb-4 max-h-56 space-y-1.5 overflow-y-auto pr-1">
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
