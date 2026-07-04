namespace MacroSync.Application;

// Response DTOs for v1 endpoints. The frontend TS types mirror these 1:1
// (OpenAPI codegen replaces the manual mirror once CI is set up).

public record MemberDto(
    Guid UserId,
    string DisplayName,
    string Role,
    string DietType,
    int CalorieTarget,
    int ProteinG,
    int CarbsG,
    int FatG);

public record HouseholdDto(Guid Id, string Name, string InviteCode, List<MemberDto> Members);

public record PortionDto(
    Guid UserId,
    string Summary,           // "250g chicken + 200g sweet potato"
    decimal Kcal,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal KcalDelta);

public record PlannedMealDto(
    Guid Id,
    string Date,              // yyyy-MM-dd
    string SlotType,
    Guid RecipeId,
    string RecipeName,
    List<PortionDto> Portions);

public record DailyTotalDto(
    Guid UserId,
    decimal ConsumedKcal,
    decimal ConsumedProteinG,
    decimal ConsumedCarbsG,
    decimal ConsumedFatG,
    int TargetKcal,
    decimal DeltaKcal);      // negative = under target

public record DayPlanDto(string Date, List<PlannedMealDto> Meals, List<DailyTotalDto> Totals);

public record WeekPlanDto(
    Guid PlanId,
    Guid HouseholdId,
    string WeekStartDate,
    List<MemberDto> Members,
    List<DayPlanDto> Days);

public record RecipeIngredientDto(
    Guid IngredientId,
    string Name,
    decimal QuantityG,
    bool IsDivisible,
    decimal KcalPer100G);

public record RecipeDto(
    Guid Id,
    string Name,
    int Servings,
    string Instructions,
    bool IsCurated,
    List<RecipeIngredientDto> Ingredients);

public record GroceryItemDto(string Name, string Aisle, decimal TotalQuantityG);

public record GroceryListDto(Guid PlanId, string WeekStartDate, List<GroceryItemDto> Items);
