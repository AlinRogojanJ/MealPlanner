namespace MacroSync.Domain;

// The wedge (Technical Design §6): pure, deterministic, no EF, no HTTP.
// v0 implementation: indivisible ingredients split by rule, divisible ones
// scaled proportionally to each eater's remaining kcal, rounded to cookable
// increments. Residual macro delta is reported honestly.

public record EaterTarget(Guid UserId, decimal RemainingKcal);

public record SolverIngredient(
    Guid IngredientId,
    string Name,
    decimal QuantityG,
    bool IsDivisible,
    IndivisibleSplitRule SplitRule,
    decimal KcalPer100G,
    decimal ProteinPer100G,
    decimal CarbsPer100G,
    decimal FatPer100G,
    decimal RoundingStepG = 5m);

public record RecipeSnapshot(Guid RecipeId, string Name, IReadOnlyList<SolverIngredient> Ingredients);

public record PortionResult(
    Guid UserId,
    Dictionary<Guid, decimal> IngredientGrams,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal KcalDelta);

public record SolveResult(IReadOnlyList<PortionResult> Portions);

public static class PortionSolver
{
    public static SolveResult Solve(RecipeSnapshot recipe, IReadOnlyList<EaterTarget> eaters)
    {
        var totalRemaining = eaters.Sum(e => e.RemainingKcal);
        var results = new List<PortionResult>();

        foreach (var eater in eaters)
        {
            // Share of the dish this eater should get, driven by remaining kcal.
            var share = totalRemaining > 0 ? eater.RemainingKcal / totalRemaining : 1m / eaters.Count;
            var grams = new Dictionary<Guid, decimal>();
            decimal kcal = 0, protein = 0, carbs = 0, fat = 0;

            foreach (var ing in recipe.Ingredients)
            {
                var raw = ing.IsDivisible
                    ? ing.QuantityG * share
                    : ing.SplitRule switch
                    {
                        IndivisibleSplitRule.Even => ing.QuantityG / eaters.Count,
                        IndivisibleSplitRule.Ratio => ing.QuantityG * share,
                        _ => ing.QuantityG / eaters.Count,
                    };

                // Round to cookable increments — never output 187 g.
                var step = ing.RoundingStepG <= 0 ? 5m : ing.RoundingStepG;
                var rounded = Math.Max(step, Math.Round(raw / step) * step);

                grams[ing.IngredientId] = rounded;
                kcal += rounded * ing.KcalPer100G / 100m;
                protein += rounded * ing.ProteinPer100G / 100m;
                carbs += rounded * ing.CarbsPer100G / 100m;
                fat += rounded * ing.FatPer100G / 100m;
            }

            results.Add(new PortionResult(
                eater.UserId, grams,
                Math.Round(kcal), Math.Round(protein, 1), Math.Round(carbs, 1), Math.Round(fat, 1),
                KcalDelta: Math.Round(kcal - eater.RemainingKcal)));
        }

        return new SolveResult(results);
    }
}
