using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/plans")]
public class PlansController(IMealPlanService plans, IRecommendationService recommendations) : ApiControllerBase
{
    /// <summary>Dishes that fit everyone's remaining targets for a slot — AI-ranked when configured (Phase 2).</summary>
    [HttpGet("{planId:guid}/recommendations")]
    public async Task<ActionResult<IReadOnlyList<MealRecommendationDto>>> GetRecommendations(
        Guid planId, [FromQuery] string date, [FromQuery] string slot, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var parsed))
            return BadRequest(new ProblemDetails { Title = "date must be a valid yyyy-MM-dd date." });
        if (!new[] { "Breakfast", "Lunch", "Dinner", "Snack" }.Contains(slot))
            return BadRequest(new ProblemDetails { Title = "slot must be Breakfast, Lunch, Dinner or Snack." });

        return Ok(await recommendations.RecommendAsync(planId, parsed, slot, ct));
    }

    /// <summary>Add dish to a slot → triggers portion solve for all members.</summary>
    [Authorize]
    [HttpPost("{planId:guid}/meals")]
    public async Task<ActionResult<PlannedMealDto>> AddMeal(
        Guid planId, AddMealRequest request, IValidator<AddMealRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        var meal = await plans.AddMealAsync(planId, request, ct);
        return meal is null ? NotFound() : Ok(meal);
    }

    [HttpGet("{planId:guid}/grocery-list")]
    public async Task<ActionResult<GroceryListDto>> GetGroceryList(Guid planId, CancellationToken ct)
    {
        var list = await plans.GetGroceryListAsync(planId, ct);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>Create (or return) the anonymous share link for the plan's grocery list.</summary>
    [Authorize]
    [HttpPost("{planId:guid}/grocery-list/share")]
    public async Task<ActionResult<ShareLinkDto>> CreateShareLink(Guid planId, CancellationToken ct)
    {
        var link = await plans.CreateShareLinkAsync(planId, ct);
        return link is null ? NotFound() : Ok(link);
    }
}
