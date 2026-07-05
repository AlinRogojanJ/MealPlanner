using MacroSync.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Authorize]
[Route("api/v1/meals")]
public class MealsController(IMealPlanService plans) : ApiControllerBase
{
    public record MoveMealRequest(string Date, string SlotType);

    /// <summary>Re-run the portion split (targets changed, eater skipped).</summary>
    [HttpPost("{id:guid}/solve")]
    public async Task<ActionResult<PlannedMealDto>> Solve(Guid id, SolveMealRequest request, CancellationToken ct)
    {
        var meal = await plans.SolveMealAsync(id, request, ct);
        return meal is null ? NotFound() : Ok(meal);
    }

    /// <summary>Move a dish to another day/slot — portions re-solve for the new slot budget.</summary>
    [HttpPost("{id:guid}/move")]
    public async Task<ActionResult<PlannedMealDto>> Move(Guid id, MoveMealRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest(new ProblemDetails { Title = "date must be a valid yyyy-MM-dd date." });
        if (!new[] { "Breakfast", "Lunch", "Dinner", "Snack" }.Contains(request.SlotType))
            return BadRequest(new ProblemDetails { Title = "slotType must be Breakfast, Lunch, Dinner or Snack." });

        var meal = await plans.MoveMealAsync(id, date, request.SlotType, ct);
        return meal is null ? NotFound() : Ok(meal);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await plans.DeleteMealAsync(id, ct) ? NoContent() : NotFound();
}
