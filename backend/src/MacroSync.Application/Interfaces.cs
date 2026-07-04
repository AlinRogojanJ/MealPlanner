namespace MacroSync.Application;

// Application service contracts. Infrastructure provides the implementations —
// today backed by the mock data store, later by EF Core against Azure SQL.

public interface IHouseholdService
{
    Task<HouseholdDto?> GetAsync(Guid householdId, CancellationToken ct = default);
}

public interface IMealPlanService
{
    /// <summary>Weekly calendar with all portions and running totals (the main page).</summary>
    Task<WeekPlanDto?> GetWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default);

    Task<GroceryListDto?> GetGroceryListAsync(Guid planId, CancellationToken ct = default);
}

public interface IRecipeService
{
    Task<IReadOnlyList<RecipeDto>> GetAllAsync(CancellationToken ct = default);
}
