using MacroSync.Domain;

namespace MacroSync.Infrastructure;

/// <summary>Helpers shared by the mock and SQL services around the PortionSolver.</summary>
public static class Solving
{
    /// <summary>Slot budget as a share of the daily target; used as "remaining kcal" for the solver.</summary>
    public static decimal SlotShare(SlotType slot) => slot switch
    {
        SlotType.Breakfast => 0.25m,
        SlotType.Lunch => 0.33m,
        SlotType.Dinner => 0.34m,
        _ => 0.08m,
    };

    /// <summary>Rounding steps that feel cookable per ingredient (§6: never output 187 g).</summary>
    public static decimal RoundingStep(string ingredientName) => ingredientName switch
    {
        "Egg" => 60m,                 // whole eggs
        "Chicken breast" or "Beef sirloin" or "Salmon fillet" or "Turkey mince" => 25m,
        "Olive oil" or "Honey" or "Soy sauce" => 5m,
        _ => 10m,
    };

    public static RecipeSnapshot BuildSnapshot(Recipe recipe, IReadOnlyDictionary<Guid, Ingredient> ingredientsById) => new(
        recipe.Id, recipe.Name,
        recipe.Ingredients.Select(ri =>
        {
            var ing = ingredientsById[ri.IngredientId];
            return new SolverIngredient(ing.Id, ing.Name, ri.QuantityG, ri.IsDivisible, ri.SplitRule,
                ing.KcalPer100G, ing.ProteinPer100G, ing.CarbsPer100G, ing.FatPer100G, RoundingStep(ing.Name));
        }).ToList());

    /// <summary>"250g chicken breast + 200g sweet potato" — top ingredients by weight.</summary>
    public static string PortionSummary(IReadOnlyDictionary<Guid, decimal> grams, IReadOnlyDictionary<Guid, Ingredient> ingredientsById)
    {
        var parts = grams
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv =>
            {
                var ing = ingredientsById[kv.Key];
                return ing.Name == "Egg"
                    ? $"{(int)(kv.Value / 60)} egg{(kv.Value > 60 ? "s" : "")}"
                    : $"{kv.Value:0}g {ing.Name.ToLowerInvariant()}";
            });
        return string.Join(" + ", parts);
    }

    public static PlannedMeal SolvePlannedMeal(
        Guid planId, DateOnly date, SlotType slot, Recipe recipe,
        IReadOnlyDictionary<Guid, Ingredient> ingredientsById,
        IReadOnlyList<(Guid UserId, int CalorieTarget)> eaters)
    {
        var targets = eaters
            .Select(e => new EaterTarget(e.UserId, e.CalorieTarget * SlotShare(slot)))
            .ToList();

        var solved = PortionSolver.Solve(BuildSnapshot(recipe, ingredientsById), targets);

        var meal = new PlannedMeal { Id = Guid.NewGuid(), MealPlanId = planId, Date = date, SlotType = slot, RecipeId = recipe.Id };
        meal.Portions.AddRange(solved.Portions.Select(p => new MealPortion
        {
            PlannedMealId = meal.Id,
            UserId = p.UserId,
            IngredientGrams = p.IngredientGrams,
            Kcal = p.Kcal,
            ProteinG = p.ProteinG,
            CarbsG = p.CarbsG,
            FatG = p.FatG,
        }));
        return meal;
    }
}
