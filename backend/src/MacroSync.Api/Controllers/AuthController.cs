using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/auth")]
public class AuthController(IAuthService auth) : ApiControllerBase
{
    /// <summary>Primary sign-in: verify Google ID token → issue app JWT + refresh (§5.2).</summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(
        GoogleSignInRequest request, IValidator<GoogleSignInRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        var response = await auth.GoogleSignInAsync(request, ct);
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request, IValidator<RegisterRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        try
        {
            return Ok(await auth.RegisterAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request, IValidator<LoginRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        var response = await auth.LoginAsync(request, ct);
        return response is null ? Unauthorized() : Ok(response);
    }

    /// <summary>Token rotation with reuse detection (§5.3).</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var response = await auth.RefreshAsync(request, ct);
        return response is null ? Unauthorized() : Ok(response);
    }
}
