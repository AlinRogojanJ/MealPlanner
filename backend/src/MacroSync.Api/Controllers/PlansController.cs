using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[ApiController]
[Route("api/v1/plans")]
public class PlansController(IMealPlanService plans) : ControllerBase
{
    [HttpGet("{planId:guid}/grocery-list")]
    public async Task<ActionResult<GroceryListDto>> GetGroceryList(Guid planId, CancellationToken ct)
    {
        var list = await plans.GetGroceryListAsync(planId, ct);
        return list is null ? NotFound() : Ok(list);
    }
}
