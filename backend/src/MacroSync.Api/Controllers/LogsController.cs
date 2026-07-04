using FluentValidation;
using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/logs")]
public class LogsController(IFoodLogService logs) : ApiControllerBase
{
    /// <summary>Log off-plan food → creates a pending RecalcSuggestion (§5.4).</summary>
    [HttpPost]
    public async Task<ActionResult<LogFoodResponse>> Log(
        LogFoodRequest request, IValidator<LogFoodRequest> validator, CancellationToken ct)
    {
        if (await ValidateAsync(request, validator, ct) is { } invalid) return invalid;
        return Ok(await logs.LogAsync(request, ct));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FoodLogDto>>> GetForDay(
        [FromQuery] Guid userId, [FromQuery] string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var parsed))
            return BadRequest(new ProblemDetails { Title = "date must be a valid yyyy-MM-dd date." });
        return Ok(await logs.GetForDayAsync(userId, parsed, ct));
    }
}
