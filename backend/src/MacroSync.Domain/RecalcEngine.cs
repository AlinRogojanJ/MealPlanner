namespace MacroSync.Domain;

// Rules-based recalc (Product Plan §5.4): after an off-plan log, propose
// shrinking that person's LATER meals to absorb the overage. Pure and
// deterministic like the solver; the Phase 2 AI layer proposes candidates
// through the same shapes.

public record RemainingMeal(Guid PlannedMealId, string RecipeName, string SlotType, decimal PlannedKcal);

public record MealAdjustment(Guid PlannedMealId, string RecipeName, string SlotType, decimal OldKcal, decimal NewKcal, decimal Scale);

public record RecalcProposal(
    decimal OverageKcal,
    IReadOnlyList<MealAdjustment> Adjustments,
    decimal AbsorbedKcal,
    /// <summary>Overage that could NOT be absorbed without unhealthy cuts — shown honestly.</summary>
    decimal UnabsorbedKcal,
    /// <summary>"Rules" (v1 engine) or "AI" (advisor-proposed mix, validated by the rules bounds).</summary>
    string Source = "Rules");

public static class RecalcEngine
{
    /// <summary>Never shrink a remaining meal below this fraction of its planned size (edge case: overage too large to fix).</summary>
    public const decimal MinMealScale = 0.6m;

    public static RecalcProposal Propose(decimal overageKcal, IReadOnlyList<RemainingMeal> remainingMeals)
    {
        if (overageKcal <= 0 || remainingMeals.Count == 0)
            return new RecalcProposal(overageKcal, [], 0, Math.Max(0, overageKcal));

        var totalRemaining = remainingMeals.Sum(m => m.PlannedKcal);
        var maxAbsorbable = totalRemaining * (1 - MinMealScale);
        var absorbed = Math.Min(overageKcal, maxAbsorbable);
        var scale = totalRemaining > 0 ? 1 - absorbed / totalRemaining : 1;

        var adjustments = remainingMeals
            .Select(m => new MealAdjustment(
                m.PlannedMealId, m.RecipeName, m.SlotType,
                OldKcal: Math.Round(m.PlannedKcal),
                NewKcal: Math.Round(m.PlannedKcal * scale),
                Scale: Math.Round(scale, 3)))
            .ToList();

        return new RecalcProposal(
            Math.Round(overageKcal),
            adjustments,
            AbsorbedKcal: Math.Round(absorbed),
            UnabsorbedKcal: Math.Round(overageKcal - absorbed));
    }

    /// <summary>
    /// Prices an externally proposed per-meal scale mix (e.g. from the AI advisor).
    /// Scales are clamped to the safe band; the arithmetic here stays the truth.
    /// </summary>
    public static RecalcProposal PriceCandidates(
        decimal overageKcal,
        IReadOnlyList<RemainingMeal> remainingMeals,
        IReadOnlyDictionary<Guid, decimal> proposedScales,
        string source)
    {
        var adjustments = remainingMeals
            .Select(m =>
            {
                var scale = proposedScales.TryGetValue(m.PlannedMealId, out var s)
                    ? Math.Clamp(s, MinMealScale, 1m)
                    : 1m;
                return new MealAdjustment(
                    m.PlannedMealId, m.RecipeName, m.SlotType,
                    OldKcal: Math.Round(m.PlannedKcal),
                    NewKcal: Math.Round(m.PlannedKcal * scale),
                    Scale: Math.Round(scale, 3));
            })
            .Where(a => a.Scale < 1m)
            .ToList();

        var absorbed = adjustments.Sum(a => a.OldKcal - a.NewKcal);
        return new RecalcProposal(
            Math.Round(overageKcal),
            adjustments,
            AbsorbedKcal: Math.Round(absorbed),
            UnabsorbedKcal: Math.Max(0, Math.Round(overageKcal - absorbed)),
            Source: source);
    }
}
