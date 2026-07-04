using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/meals")]
public class MealsController(IMealPlanService plans) : ApiControllerBase
{
    /// <summary>Re-run the portion split (targets changed, eater skipped).</summary>
    [HttpPost("{id:guid}/solve")]
    public async Task<ActionResult<PlannedMealDto>> Solve(Guid id, SolveMealRequest request, CancellationToken ct)
    {
        var meal = await plans.SolveMealAsync(id, request, ct);
        return meal is null ? NotFound() : Ok(meal);
    }
}
