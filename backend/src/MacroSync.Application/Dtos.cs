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

public record ShareLinkDto(string ShareToken, string Url);

// ---- Auth ----

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

public record GoogleSignInRequest(string IdToken);

public record RefreshRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    Guid UserId,
    string DisplayName,
    string Email);

// ---- Profile ----

public record UpdateProfileRequest(
    int CalorieTarget,
    int ProteinG,
    int CarbsG,
    int FatG,
    string DietType); // Cut | Maintain | Bulk

// ---- Planning writes ----

public record AddMealRequest(string Date, string SlotType, Guid RecipeId);

public record SolveMealRequest(List<Guid>? SkippedUserIds);

// ---- Off-plan logging & recalc ----

public record LogFoodRequest(
    Guid UserId,          // from JWT once auth is enforced; explicit for now
    string Date,
    string Description,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG);

public record FoodLogDto(
    Guid Id,
    Guid UserId,
    string Date,
    string Source,
    string Description,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG);

public record MealAdjustmentDto(
    Guid PlannedMealId,
    string RecipeName,
    string SlotType,
    decimal OldKcal,
    decimal NewKcal,
    decimal Scale);

public record SuggestionDto(
    Guid Id,
    Guid UserId,
    string Date,
    string Status,
    decimal OverageKcal,
    decimal AbsorbedKcal,
    decimal UnabsorbedKcal,
    List<MealAdjustmentDto> Adjustments);

public record LogFoodResponse(FoodLogDto Log, SuggestionDto? Suggestion);
