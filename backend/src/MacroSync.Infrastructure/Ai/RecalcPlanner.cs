using MacroSync.Application;
using MacroSync.Domain;

namespace MacroSync.Infrastructure.Ai;

/// <summary>
/// Chooses between the AI advisor and the v1 rules engine for a recalc.
/// The advisor only proposes the *mix* (per-meal scales); pricing and safety
/// bounds always run through RecalcEngine — the rules stay the truth.
/// </summary>
public class RecalcPlanner(IAiAdvisor advisor)
{
    public async Task<RecalcProposal> ProposeAsync(
        string offPlanDescription,
        decimal overageKcal,
        string dietType,
        IReadOnlyList<RemainingMeal> remainingMeals,
        CancellationToken ct = default)
    {
        if (advisor.IsEnabled && overageKcal > 0 && remainingMeals.Count > 0)
        {
            var candidates = await advisor.ProposeRecalcAsync(new AiRecalcRequest(
                offPlanDescription, overageKcal, dietType,
                remainingMeals.Select(m => new AiRemainingMeal(m.PlannedMealId, m.RecipeName, m.SlotType, m.PlannedKcal)).ToList()), ct);

            if (candidates is { Count: > 0 })
            {
                return RecalcEngine.PriceCandidates(
                    overageKcal, remainingMeals,
                    candidates.ToDictionary(c => c.PlannedMealId, c => c.Scale),
                    source: "AI");
            }
        }

        return RecalcEngine.Propose(overageKcal, remainingMeals);
    }
}
