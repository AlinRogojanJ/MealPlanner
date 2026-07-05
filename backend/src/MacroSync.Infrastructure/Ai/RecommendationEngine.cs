using MacroSync.Application;
using MacroSync.Domain;

namespace MacroSync.Infrastructure.Ai;

/// <summary>One eater as the recommender sees them.</summary>
public record RecEater(Guid UserId, string DisplayName, string DietType, int CalorieTarget);

/// <summary>
/// Meal recommendations: the AI ranks and explains when available; otherwise a
/// fit heuristic ranks by how close the default solve lands to everyone's slot
/// budget. Either way, portions are priced by the real PortionSolver.
/// </summary>
public static class RecommendationEngine
{
    public static async Task<IReadOnlyList<MealRecommendationDto>> RecommendAsync(
        IAiAdvisor advisor,
        string slotType,
        IReadOnlyList<Recipe> recipes,
        IReadOnlyDictionary<Guid, Ingredient> ingredientsById,
        IReadOnlyList<RecEater> eaters,
        IReadOnlyList<string> recentRecipeNames,
        CancellationToken ct)
    {
        if (recipes.Count == 0 || eaters.Count == 0) return [];

        var slot = Enum.Parse<SlotType>(slotType);
        var share = Solving.SlotShare(slot);
        var eaterTuples = eaters.Select(e => (e.UserId, e.CalorieTarget)).ToList();

        // Price every candidate through the solver once — used for both paths.
        var priced = recipes.ToDictionary(
            r => r.Id,
            r => Solving.SolvePlannedMeal(Guid.Empty, default, slot, r, ingredientsById, eaterTuples));

        List<(Recipe Recipe, string Reason, string Source)> ranked;

        var aiRanked = advisor.IsEnabled
            ? await advisor.RecommendMealsAsync(new AiRecommendationRequest(
                slotType,
                eaters.Select(e => (e.DisplayName, e.DietType, e.CalorieTarget * share)).ToList(),
                recipes.Select(r => new AiRecommendationCandidateRecipe(
                    r.Id, r.Name,
                    KcalPerServing: RecipeKcal(r, ingredientsById) / Math.Max(1, r.Servings),
                    MainIngredients: MainIngredients(r, ingredientsById))).ToList(),
                recentRecipeNames), ct)
            : null;

        if (aiRanked is { Count: > 0 })
        {
            var byId = recipes.ToDictionary(r => r.Id);
            ranked = aiRanked.Select(a => (byId[a.RecipeId], a.Reason, "AI")).ToList();
        }
        else
        {
            // Heuristic: smallest total |kcal delta| between solved portions and slot budgets,
            // with a small penalty for recently eaten dishes (variety).
            ranked = recipes
                .Select(r =>
                {
                    var delta = priced[r.Id].Portions.Sum(p =>
                    {
                        var target = eaters.First(e => e.UserId == p.UserId).CalorieTarget * share;
                        return Math.Abs(p.Kcal - target);
                    });
                    var repeatPenalty = recentRecipeNames.Contains(r.Name) ? 150m : 0m;
                    return (Recipe: r, Score: delta + repeatPenalty);
                })
                .OrderBy(x => x.Score)
                .Take(3)
                .Select(x => (x.Recipe, $"Closest fit to everyone's {slotType.ToLowerInvariant()} budget.", "Rules"))
                .ToList();
        }

        return ranked.Select(x => new MealRecommendationDto(
            x.Recipe.Id, x.Recipe.Name, x.Reason,
            priced[x.Recipe.Id].Portions.Select(p => new PortionDto(
                p.UserId,
                Solving.PortionSummary(p.IngredientGrams, ingredientsById),
                p.Kcal, p.ProteinG, p.CarbsG, p.FatG, KcalDelta: 0)).ToList(),
            x.Source)).ToList();
    }

    private static decimal RecipeKcal(Recipe recipe, IReadOnlyDictionary<Guid, Ingredient> ingredients) =>
        recipe.Ingredients.Sum(ri => ri.QuantityG * ingredients[ri.IngredientId].KcalPer100G / 100m);

    private static string MainIngredients(Recipe recipe, IReadOnlyDictionary<Guid, Ingredient> ingredients) =>
        string.Join(", ", recipe.Ingredients
            .OrderByDescending(ri => ri.QuantityG)
            .Take(3)
            .Select(ri => ingredients[ri.IngredientId].Name.ToLowerInvariant()));
}
