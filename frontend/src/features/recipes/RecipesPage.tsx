import type { RecipeDto } from '../../api/types'
import { useRecipes } from './useRecipes'

function recipeKcalPerServing(recipe: RecipeDto): number {
  const total = recipe.ingredients.reduce(
    (sum, ing) => sum + (ing.quantityG * ing.kcalPer100G) / 100,
    0,
  )
  return Math.round(total / recipe.servings)
}

function RecipeCard({ recipe }: { recipe: RecipeDto }) {
  return (
    <div className="flex flex-col rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition-shadow hover:shadow-md">
      <div className="mb-1 flex items-start justify-between gap-2">
        <h3 className="text-sm font-bold leading-snug text-slate-800">{recipe.name}</h3>
        {recipe.isCurated && (
          <span className="shrink-0 rounded-full bg-indigo-50 px-2 py-0.5 text-[10px] font-semibold text-indigo-600">
            Curated
          </span>
        )}
      </div>
      <p className="mb-3 text-xs text-slate-500">
        {recipe.servings} servings · ~{recipeKcalPerServing(recipe)} kcal / serving
      </p>
      <ul className="mb-3 space-y-1">
        {recipe.ingredients.map((ing) => (
          <li key={ing.ingredientId} className="flex items-center justify-between text-xs">
            <span className="flex items-center gap-1.5 text-slate-600">
              {ing.name}
              {!ing.isDivisible && (
                <span
                  className="rounded bg-amber-50 px-1 py-px text-[9px] font-semibold uppercase text-amber-600"
                  title="Split by rule, not scaled per person"
                >
                  indivisible
                </span>
              )}
            </span>
            <span className="tabular-nums text-slate-400">{ing.quantityG} g</span>
          </li>
        ))}
      </ul>
      <p className="mt-auto border-t border-slate-100 pt-2 text-[11px] leading-relaxed text-slate-400">
        {recipe.instructions}
      </p>
    </div>
  )
}

/** Curated recipe library — user recipes and food-DB lookup arrive later. */
export function RecipesPage() {
  const { data: recipes, isLoading } = useRecipes()

  if (isLoading) return <div className="p-10 text-center text-slate-400">Loading recipes…</div>
  if (!recipes?.length)
    return <div className="p-10 text-center text-slate-400">No recipes yet.</div>

  return (
    <div>
      <div className="mb-4">
        <h2 className="text-lg font-bold text-slate-800">Recipe library</h2>
        <p className="text-sm text-slate-500">
          Curated starter recipes with divisibility tags so portion splitting works automatically.
        </p>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {recipes.map((recipe) => (
          <RecipeCard key={recipe.id} recipe={recipe} />
        ))}
      </div>
    </div>
  )
}
