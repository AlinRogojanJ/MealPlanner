namespace MacroSync.Application;

// Phase 2 AI layer (Product Plan §5.4/§8): the LLM proposes candidate
// adjustments and recommendations; the domain rules validate and price them.
// The solver/rules stay the single source of arithmetic truth.

/// <summary>What the AI sees about one remaining meal when proposing a recalc.</summary>
public record AiRemainingMeal(Guid PlannedMealId, string RecipeName, string SlotType, decimal PlannedKcal);

/// <summary>AI's proposed scale (0..1] per meal — validated and re-priced by the rules engine.</summary>
public record AiRecalcCandidate(Guid PlannedMealId, decimal Scale);

public record AiRecalcRequest(
    string OffPlanDescription,
    decimal OverageKcal,
    string DietType,
    IReadOnlyList<AiRemainingMeal> RemainingMeals);

/// <summary>One AI-ranked recipe with the human-readable why.</summary>
public record AiRecommendation(Guid RecipeId, string Reason);

public record AiRecommendationCandidateRecipe(Guid RecipeId, string Name, decimal KcalPerServing, string MainIngredients);

public record AiRecommendationRequest(
    string SlotType,
    IReadOnlyList<(string DisplayName, string DietType, decimal RemainingKcal)> Eaters,
    IReadOnlyList<AiRecommendationCandidateRecipe> Recipes,
    IReadOnlyList<string> RecentRecipeNames); // avoid recommending yesterday's dinner again

/// <summary>
/// LLM advisor. Implementations return null when the AI is unavailable
/// (no API key, network failure) — callers fall back to the rules engine.
/// </summary>
public interface IAiAdvisor
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<AiRecalcCandidate>?> ProposeRecalcAsync(AiRecalcRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<AiRecommendation>?> RecommendMealsAsync(AiRecommendationRequest request, CancellationToken ct = default);
}

public interface IRecommendationService
{
    /// <summary>Top dishes for a slot that fit everyone's remaining targets (AI-ranked when available).</summary>
    Task<IReadOnlyList<MealRecommendationDto>> RecommendAsync(Guid planId, DateOnly date, string slotType, CancellationToken ct = default);
}
