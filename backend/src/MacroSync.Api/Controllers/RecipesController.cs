using MacroSync.Application;
using Microsoft.AspNetCore.Mvc;

namespace MacroSync.Api.Controllers;

[ApiController]
[Route("api/v1/recipes")]
public class RecipesController(IRecipeService recipes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecipeDto>>> GetAll(CancellationToken ct) =>
        Ok(await recipes.GetAllAsync(ct));
}
