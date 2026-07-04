using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/households")]
public class HouseholdsController(IHouseholdService households, IMealPlanService plans) : ApiControllerBase
{
    public record JoinRequest(string InviteCode, Guid UserId);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HouseholdDto>> Get(Guid id, CancellationToken ct)
    {
        var household = await households.GetAsync(id, ct);
        return household is null ? NotFound() : Ok(household);
    }

    /// <summary>Invite/join via invite code.</summary>
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<HouseholdDto>> Join(Guid id, JoinRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId(fallback: request.UserId);
        if (userId is null) return Unauthorized();

        var household = await households.JoinAsync(id, request.InviteCode, userId.Value, ct);
        return household is null ? NotFound() : Ok(household);
    }

    /// <summary>Weekly calendar with all portions and running totals — the main page.</summary>
    [HttpGet("{id:guid}/plans")]
    public async Task<ActionResult<WeekPlanDto>> GetWeekPlan(Guid id, [FromQuery] string? week, CancellationToken ct)
    {
        var weekStart = week is not null && DateOnly.TryParse(week, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow); // v0 mock ignores it anyway

        var plan = await plans.GetWeekPlanAsync(id, weekStart, ct);
        return plan is null ? NotFound() : Ok(plan);
    }
}
