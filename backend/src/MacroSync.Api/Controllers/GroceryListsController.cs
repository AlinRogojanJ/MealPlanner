using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[Route("api/v1/grocery-lists")]
public class GroceryListsController(IMealPlanService plans) : ApiControllerBase
{
    /// <summary>Anonymous read-only share link (v1 phone export) — capability URL, no auth.</summary>
    [HttpGet("{shareToken}")]
    public async Task<ActionResult<GroceryListDto>> GetByToken(string shareToken, CancellationToken ct)
    {
        var list = await plans.GetGroceryListByTokenAsync(shareToken, ct);
        return list is null ? NotFound() : Ok(list);
    }
}
