using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Authorize]
[Route("api/v1/plans")]
public class PlansController(IMealPlanService plans, IRecommendationService recommendations, IMembershipService membership) : ApiControllerBase
{
    /// <summary>404 for unknown plans, 401/403 for callers outside the owning household, null when allowed.</summary>
    private async Task<ActionResult?> DenyUnlessPlanMemberAsync(Guid planId, CancellationToken ct)
    {
        var householdId = await membership.GetHouseholdIdForPlanAsync(planId, ct);
        if (householdId is null) return NotFound();
        return await DenyUnlessMemberAsync(membership, householdId.Value, ct);
    }

    /// <summary>Dishes that fit everyone's remaining targets for a slot — AI-ranked when configured (Phase 2).</summary>
    [HttpGet("{planId:guid}/recommendations")]
    public async Task<ActionResult<IReadOnlyList<MealRecommendationDto>>> GetRecommendations(
        Guid planId, [FromQuery] string date, [FromQuery] string slot, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var parsed))
            return BadRequest(new ProblemDetails { Title = "date must be a valid yyyy-MM-dd date." });
        if (!new[] { "Breakfast", "Lunch", "Dinner", "Snack" }.Contains(slot))
            return BadRequest(new ProblemDetails { Title = "slot must be Breakfast, Lunch, Dinner or Snack." });
        if (await DenyUnlessPlanMemberAsync(planId, ct) is { } denied) return denied;

        return Ok(await recommendations.RecommendAsync(planId, parsed, slot, ct));
    }

    /// <summary>Add dish to a slot → triggers portion solve for all members.</summary>
    [HttpPost("{planId:guid}/meals")]
    public async Task<ActionResult<PlannedMealDto>> AddMeal(
        Guid planId, AddMealRequest request, IValidator<AddMealRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        if (await DenyUnlessPlanMemberAsync(planId, ct) is { } denied) return denied;

        var meal = await plans.AddMealAsync(planId, request, ct);
        return meal is null ? NotFound() : Ok(meal);
    }

    public record CopyDayRequest(string FromDate, string ToDate);

    /// <summary>Replace one day's menu with another day's from the same week, re-solved for current targets.</summary>
    [HttpPost("{planId:guid}/copy-day")]
    public async Task<ActionResult<WeekPlanDto>> CopyDay(Guid planId, CopyDayRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.FromDate, out var fromDate) || !DateOnly.TryParse(request.ToDate, out var toDate))
            return BadRequest(new ProblemDetails { Title = "fromDate and toDate must be valid yyyy-MM-dd dates." });
        if (await DenyUnlessPlanMemberAsync(planId, ct) is { } denied) return denied;

        var week = await plans.CopyDayAsync(planId, fromDate, toDate, ct);
        return week is null ? NotFound() : Ok(week);
    }

    [HttpGet("{planId:guid}/grocery-list")]
    public async Task<ActionResult<GroceryListDto>> GetGroceryList(Guid planId, CancellationToken ct)
    {
        if (await DenyUnlessPlanMemberAsync(planId, ct) is { } denied) return denied;

        var list = await plans.GetGroceryListAsync(planId, ct);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>Create (or return) the anonymous share link for the plan's grocery list.</summary>
    [HttpPost("{planId:guid}/grocery-list/share")]
    public async Task<ActionResult<ShareLinkDto>> CreateShareLink(Guid planId, CancellationToken ct)
    {
        if (await DenyUnlessPlanMemberAsync(planId, ct) is { } denied) return denied;

        var link = await plans.CreateShareLinkAsync(planId, ct);
        return link is null ? NotFound() : Ok(link);
    }
}
