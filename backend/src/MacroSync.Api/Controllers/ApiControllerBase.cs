using System.Security.Claims;
using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Validates a write DTO; returns RFC 9457 problem+json with field errors on failure (§4.1).</summary>
    protected async Task<ActionResult?> ValidateAsync<T>(T request, IValidator<T> validator, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (result.IsValid) return null;

        foreach (var error in result.Errors)
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return ValidationProblem(ModelState);
    }

    /// <summary>User id from the JWT; falls back to an explicit query/body id in unauthenticated dev calls.</summary>
    protected Guid? CurrentUserId(Guid? fallback = null)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : fallback;
    }

    /// <summary>Household-membership policy (§5.2): 401 without a user, 403 when the caller
    /// isn't a member of the household owning the resource; null when access is allowed.</summary>
    protected async Task<ActionResult?> DenyUnlessMemberAsync(
        IMembershipService membership, Guid householdId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return await membership.IsMemberAsync(userId.Value, householdId, ct) ? null : Forbid();
    }

    /// <summary>Same policy for user-scoped resources (logs, suggestions): the caller must share
    /// a household with the target user — partners see each other's data, strangers don't.</summary>
    protected async Task<ActionResult?> DenyUnlessSameHouseholdAsync(
        IMembershipService membership, Guid targetUserId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (userId.Value == targetUserId) return null;
        return await membership.ShareHouseholdAsync(userId.Value, targetUserId, ct) ? null : Forbid();
    }
}
