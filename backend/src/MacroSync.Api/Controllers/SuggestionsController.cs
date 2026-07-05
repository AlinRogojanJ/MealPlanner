using MacroSync.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Authorize]
[Route("api/v1/suggestions")]
public class SuggestionsController(ISuggestionService suggestions, IMembershipService membership) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SuggestionDto>>> GetPending(
        [FromQuery] Guid userId, CancellationToken ct)
    {
        if (await DenyUnlessSameHouseholdAsync(membership, userId, ct) is { } denied) return denied;

        return Ok(await suggestions.GetPendingAsync(userId, ct));
    }

    /// <summary>Accept/dismiss change the owner's own portions, so only the owner may act —
    /// 404 for unknown ids, 401/403 for anyone else, null when allowed.</summary>
    private async Task<ActionResult?> DenyUnlessOwnerAsync(Guid suggestionId, CancellationToken ct)
    {
        var owner = await membership.GetSuggestionOwnerAsync(suggestionId, ct);
        if (owner is null) return NotFound();

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return userId == owner ? null : Forbid();
    }

    /// <summary>One-tap apply of a recalc suggestion — shrinks the affected later meals.</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<SuggestionDto>> Accept(Guid id, CancellationToken ct)
    {
        if (await DenyUnlessOwnerAsync(id, ct) is { } denied) return denied;

        var result = await suggestions.AcceptAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<SuggestionDto>> Dismiss(Guid id, CancellationToken ct)
    {
        if (await DenyUnlessOwnerAsync(id, ct) is { } denied) return denied;

        var result = await suggestions.DismissAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
