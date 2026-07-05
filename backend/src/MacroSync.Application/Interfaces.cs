namespace MacroSync.Application;

// Application service contracts. Infrastructure provides the implementations —
// EF Core against SQL when DataSource=Sql, the in-memory mock store when Mock.

public interface IHouseholdService
{
    Task<HouseholdDto?> GetAsync(Guid householdId, CancellationToken ct = default);
    Task<HouseholdDto?> JoinAsync(Guid householdId, string inviteCode, Guid userId, CancellationToken ct = default);
}

public interface IMealPlanService
{
    /// <summary>Weekly calendar with all portions and running totals (the main page).</summary>
    Task<WeekPlanDto?> GetWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default);

    /// <summary>Create an empty plan for a week; returns the existing one if already there.</summary>
    Task<WeekPlanDto?> CreateWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default);

    /// <summary>Add a dish to a slot → triggers portion solve for all members.</summary>
    Task<PlannedMealDto?> AddMealAsync(Guid planId, AddMealRequest request, CancellationToken ct = default);

    /// <summary>Re-run the portion split (targets changed, eater skipped).</summary>
    Task<PlannedMealDto?> SolveMealAsync(Guid plannedMealId, SolveMealRequest request, CancellationToken ct = default);

    Task<GroceryListDto?> GetGroceryListAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Create (or return existing) anonymous share token for the plan's grocery list.</summary>
    Task<ShareLinkDto?> CreateShareLinkAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Anonymous read-only grocery list via capability URL (v1 phone export).</summary>
    Task<GroceryListDto?> GetGroceryListByTokenAsync(string shareToken, CancellationToken ct = default);
}

public interface IRecipeService
{
    Task<IReadOnlyList<RecipeDto>> GetAllAsync(CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse?> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken ct = default);
    Task<AuthResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
}

public interface IProfileService
{
    /// <summary>Creates a new active profile row (history kept, per schema decision §3.2).</summary>
    Task<MemberDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
}

public interface IFoodLogService
{
    /// <summary>Log off-plan food → flags overage → creates a pending RecalcSuggestion.</summary>
    Task<LogFoodResponse> LogAsync(LogFoodRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<FoodLogDto>> GetForDayAsync(Guid userId, DateOnly date, CancellationToken ct = default);
}

public interface ISuggestionService
{
    Task<IReadOnlyList<SuggestionDto>> GetPendingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One-tap apply: scales the affected meal portions and marks the suggestion accepted.</summary>
    Task<SuggestionDto?> AcceptAsync(Guid suggestionId, CancellationToken ct = default);

    Task<SuggestionDto?> DismissAsync(Guid suggestionId, CancellationToken ct = default);
}
