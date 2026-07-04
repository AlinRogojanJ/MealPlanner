using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/users")]
public class UsersController(IProfileService profiles) : ApiControllerBase
{
    /// <summary>Create a new ACTIVE nutrition profile (versioned — history kept, §3.2).</summary>
    [HttpPut("me/profile")]
    public async Task<ActionResult<MemberDto>> UpdateProfile(
        UpdateProfileRequest request, IValidator<UpdateProfileRequest> validator,
        [FromQuery] Guid? userId, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;

        // userId query param is a dev fallback until auth is enforced on this route.
        var id = CurrentUserId(fallback: userId);
        if (id is null) return Unauthorized();

        var member = await profiles.UpdateProfileAsync(id.Value, request, ct);
        return member is null ? NotFound() : Ok(member);
    }
}
