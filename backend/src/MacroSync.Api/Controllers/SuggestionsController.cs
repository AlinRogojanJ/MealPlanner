using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/suggestions")]
public class SuggestionsController(ISuggestionService suggestions) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SuggestionDto>>> GetPending(
        [FromQuery] Guid userId, CancellationToken ct) =>
        Ok(await suggestions.GetPendingAsync(userId, ct));

    /// <summary>One-tap apply of a recalc suggestion — shrinks the affected later meals.</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<SuggestionDto>> Accept(Guid id, CancellationToken ct)
    {
        var result = await suggestions.AcceptAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<SuggestionDto>> Dismiss(Guid id, CancellationToken ct)
    {
        var result = await suggestions.DismissAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
