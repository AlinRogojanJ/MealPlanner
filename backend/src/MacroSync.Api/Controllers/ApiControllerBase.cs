using System.Security.Claims;
using FluentValidation;
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
}
