using MacroSync.Application;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<HouseholdDto>> Join(Guid id, JoinRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId(fallback: request.UserId);
        if (userId is null) return Unauthorized();

        var household = await households.JoinAsync(id, request.InviteCode, userId.Value, ct);
        return household is null ? NotFound() : Ok(household);
    }

    public record CreatePlanRequest(string WeekStartDate, string? CopyFromWeekStartDate = null);

    /// <summary>Create a plan for a week (idempotent). Optionally copy another week's menu,
    /// re-solved against current targets (save-week-as-template §5.2).</summary>
    [Authorize]
    [HttpPost("{id:guid}/plans")]
    public async Task<ActionResult<WeekPlanDto>> CreateWeekPlan(Guid id, CreatePlanRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.WeekStartDate, out var weekStart))
            return BadRequest(new ProblemDetails { Title = "weekStartDate must be a valid yyyy-MM-dd date." });

        DateOnly? copyFrom = null;
        if (request.CopyFromWeekStartDate is not null)
        {
            if (!DateOnly.TryParse(request.CopyFromWeekStartDate, out var parsed))
                return BadRequest(new ProblemDetails { Title = "copyFromWeekStartDate must be a valid yyyy-MM-dd date." });
            copyFrom = parsed;
        }

        var plan = await plans.CreateWeekPlanAsync(id, weekStart, copyFrom, ct);
        return plan is null ? NotFound() : Ok(plan);
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
