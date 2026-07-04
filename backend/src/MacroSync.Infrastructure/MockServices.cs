using System.Text.Json;
using MacroSync.Application;
using MacroSync.Domain;
using MacroSync.Infrastructure.Auth;
using MacroSync.Infrastructure.Mocks;

namespace MacroSync.Infrastructure;

// Mock-backed implementations of the Application interfaces, active when
// DataSource=Mock. Same DTO shapes as the SQL services — the frontend cannot
// tell the difference. Writes mutate the in-memory MockDb (lost on restart).

internal static class Mapping
{
    public static MemberDto ToMemberDto(this MockDb db, HouseholdMember m)
    {
        var user = db.Users.First(u => u.Id == m.UserId);
        var profile = db.Profiles.First(p => p.UserId == m.UserId && p.IsActive);
        return new MemberDto(user.Id, user.DisplayName, m.Role.ToString(), profile.DietType.ToString(),
            profile.CalorieTarget, profile.ProteinG, profile.CarbsG, profile.FatG);
    }

    public static PlannedMealDto ToDto(this MockDb db, PlannedMeal meal) => new(
        meal.Id, meal.Date.ToString("yyyy-MM-dd"), meal.SlotType.ToString(), meal.RecipeId,
        db.Recipes.First(r => r.Id == meal.RecipeId).Name,
        meal.Portions.Select(p => new PortionDto(
            p.UserId, Solving.PortionSummary(p.IngredientGrams, db.IngredientsById),
            p.Kcal, p.ProteinG, p.CarbsG, p.FatG, KcalDelta: 0)).ToList());

    public static SuggestionDto ToDto(RecalcSuggestion s)
    {
        var proposal = JsonSerializer.Deserialize<RecalcProposal>(s.PayloadJson)!;
        return new SuggestionDto(s.Id, s.UserId, s.Date.ToString("yyyy-MM-dd"), s.Status.ToString(),
            proposal.OverageKcal, proposal.AbsorbedKcal, proposal.UnabsorbedKcal,
            proposal.Adjustments.Select(a => new MealAdjustmentDto(
                a.PlannedMealId, a.RecipeName, a.SlotType, a.OldKcal, a.NewKcal, a.Scale)).ToList());
    }

    public static FoodLogDto ToDto(FoodLog log) => new(
        log.Id, log.UserId, log.Date.ToString("yyyy-MM-dd"), log.Source.ToString(),
        log.Description, log.Kcal, log.ProteinG, log.CarbsG, log.FatG);
}

public class MockHouseholdService(MockDb db) : IHouseholdService
{
    public Task<HouseholdDto?> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        if (householdId != db.Household.Id) return Task.FromResult<HouseholdDto?>(null);
        var dto = new HouseholdDto(db.Household.Id, db.Household.Name, db.Household.InviteCode,
            db.Household.Members.Select(db.ToMemberDto).ToList());
        return Task.FromResult<HouseholdDto?>(dto);
    }

    public Task<HouseholdDto?> JoinAsync(Guid householdId, string inviteCode, Guid userId, CancellationToken ct = default)
    {
        if (householdId != db.Household.Id || inviteCode != db.Household.InviteCode)
            return Task.FromResult<HouseholdDto?>(null);
        if (db.Household.Members.All(m => m.UserId != userId))
            db.Household.Members.Add(new HouseholdMember { UserId = userId, HouseholdId = householdId, Role = HouseholdRole.Member });
        return GetAsync(householdId, ct);
    }
}

public class MockMealPlanService(MockDb db) : IMealPlanService
{
    public Task<WeekPlanDto?> GetWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default)
    {
        // v0: one seeded plan regardless of the requested week — the shape is what matters.
        if (householdId != db.Household.Id) return Task.FromResult<WeekPlanDto?>(null);

        var members = db.Household.Members.Select(db.ToMemberDto).ToList();
        var days = new List<DayPlanDto>();

        for (var d = 0; d < 7; d++)
        {
            var date = db.Plan.WeekStartDate.AddDays(d);
            var meals = db.Plan.Meals
                .Where(m => m.Date == date)
                .OrderBy(m => m.SlotType)
                .Select(db.ToDto)
                .ToList();

            var totals = members.Select(member =>
            {
                var portions = db.Plan.Meals.Where(m => m.Date == date)
                    .SelectMany(m => m.Portions).Where(p => p.UserId == member.UserId).ToList();
                var kcal = portions.Sum(p => p.Kcal);
                return new DailyTotalDto(member.UserId, kcal,
                    portions.Sum(p => p.ProteinG), portions.Sum(p => p.CarbsG), portions.Sum(p => p.FatG),
                    member.CalorieTarget, kcal - member.CalorieTarget);
            }).ToList();

            days.Add(new DayPlanDto(date.ToString("yyyy-MM-dd"), meals, totals));
        }

        return Task.FromResult<WeekPlanDto?>(new WeekPlanDto(
            db.Plan.Id, householdId, db.Plan.WeekStartDate.ToString("yyyy-MM-dd"), members, days));
    }

    public Task<PlannedMealDto?> AddMealAsync(Guid planId, AddMealRequest request, CancellationToken ct = default)
    {
        if (planId != db.Plan.Id) return Task.FromResult<PlannedMealDto?>(null);
        var recipe = db.Recipes.FirstOrDefault(r => r.Id == request.RecipeId);
        if (recipe is null) return Task.FromResult<PlannedMealDto?>(null);

        var eaters = db.Profiles.Where(p => p.IsActive).Select(p => (p.UserId, p.CalorieTarget)).ToList();
        var meal = Solving.SolvePlannedMeal(db.Plan.Id, DateOnly.Parse(request.Date),
            Enum.Parse<SlotType>(request.SlotType), recipe, db.IngredientsById, eaters);
        db.Plan.Meals.Add(meal);
        return Task.FromResult<PlannedMealDto?>(db.ToDto(meal));
    }

    public Task<PlannedMealDto?> SolveMealAsync(Guid plannedMealId, SolveMealRequest request, CancellationToken ct = default)
    {
        var meal = db.Plan.Meals.FirstOrDefault(m => m.Id == plannedMealId);
        if (meal is null) return Task.FromResult<PlannedMealDto?>(null);

        var recipe = db.Recipes.First(r => r.Id == meal.RecipeId);
        var skipped = request.SkippedUserIds ?? [];
        var eaters = db.Profiles.Where(p => p.IsActive && !skipped.Contains(p.UserId))
            .Select(p => (p.UserId, p.CalorieTarget)).ToList();
        if (eaters.Count == 0) return Task.FromResult<PlannedMealDto?>(null);

        var solved = Solving.SolvePlannedMeal(meal.MealPlanId, meal.Date, meal.SlotType, recipe, db.IngredientsById, eaters);
        meal.Portions.Clear();
        foreach (var portion in solved.Portions)
        {
            portion.PlannedMealId = meal.Id;
            meal.Portions.Add(portion);
        }
        return Task.FromResult<PlannedMealDto?>(db.ToDto(meal));
    }

    public Task<GroceryListDto?> GetGroceryListAsync(Guid planId, CancellationToken ct = default)
    {
        if (planId != db.Plan.Id) return Task.FromResult<GroceryListDto?>(null);

        var items = db.Plan.Meals
            .SelectMany(m => m.Portions)
            .SelectMany(p => p.IngredientGrams)
            .GroupBy(kv => kv.Key)
            .Select(g =>
            {
                var ing = db.IngredientsById[g.Key];
                return new GroceryItemDto(ing.Name, ing.Aisle, g.Sum(kv => kv.Value));
            })
            .OrderBy(i => i.Aisle).ThenBy(i => i.Name)
            .ToList();

        return Task.FromResult<GroceryListDto?>(
            new GroceryListDto(planId, db.Plan.WeekStartDate.ToString("yyyy-MM-dd"), items));
    }

    public Task<ShareLinkDto?> CreateShareLinkAsync(Guid planId, CancellationToken ct = default)
    {
        if (planId != db.Plan.Id) return Task.FromResult<ShareLinkDto?>(null);
        db.Plan.ShareToken ??= Guid.NewGuid().ToString("N");
        return Task.FromResult<ShareLinkDto?>(
            new ShareLinkDto(db.Plan.ShareToken, $"/api/v1/grocery-lists/{db.Plan.ShareToken}"));
    }

    public Task<GroceryListDto?> GetGroceryListByTokenAsync(string shareToken, CancellationToken ct = default) =>
        db.Plan.ShareToken == shareToken
            ? GetGroceryListAsync(db.Plan.Id, ct)
            : Task.FromResult<GroceryListDto?>(null);
}

public class MockRecipeService(MockDb db) : IRecipeService
{
    public Task<IReadOnlyList<RecipeDto>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RecipeDto> recipes = db.Recipes.Select(r => new RecipeDto(
            r.Id, r.Name, r.Servings, r.Instructions, IsCurated: r.OwnerId is null,
            r.Ingredients.Select(ri =>
            {
                var ing = db.IngredientsById[ri.IngredientId];
                return new RecipeIngredientDto(ing.Id, ing.Name, ri.QuantityG, ri.IsDivisible, ing.KcalPer100G);
            }).ToList())).ToList();
        return Task.FromResult(recipes);
    }
}

public class MockProfileService(MockDb db) : IProfileService
{
    public Task<MemberDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null) return Task.FromResult<MemberDto?>(null);

        foreach (var profile in db.Profiles.Where(p => p.UserId == userId && p.IsActive))
            profile.IsActive = false;

        db.Profiles.Add(new NutritionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CalorieTarget = request.CalorieTarget,
            ProteinG = request.ProteinG,
            CarbsG = request.CarbsG,
            FatG = request.FatG,
            DietType = Enum.Parse<DietType>(request.DietType),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var role = db.Household.Members.FirstOrDefault(m => m.UserId == userId)?.Role.ToString() ?? "Member";
        return Task.FromResult<MemberDto?>(new MemberDto(userId, user.DisplayName, role, request.DietType,
            request.CalorieTarget, request.ProteinG, request.CarbsG, request.FatG));
    }
}

public class MockFoodLogService(MockDb db) : IFoodLogService
{
    public Task<LogFoodResponse> LogAsync(LogFoodRequest request, CancellationToken ct = default)
    {
        var date = DateOnly.Parse(request.Date);
        var log = new FoodLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Date = date,
            Source = FoodLogSource.OffPlan,
            Description = request.Description.Trim(),
            Kcal = request.Kcal,
            ProteinG = request.ProteinG,
            CarbsG = request.CarbsG,
            FatG = request.FatG,
        };
        db.FoodLogs.Add(log);

        // Same v1 heuristic as SQL: today → only later slots can absorb; other days → all slots.
        var minSlot = date != DateOnly.FromDateTime(DateTime.Now) ? SlotType.Breakfast
            : DateTime.Now.Hour < 11 ? SlotType.Lunch
            : DateTime.Now.Hour < 17 ? SlotType.Dinner
            : SlotType.Snack;

        var remainingMeals = db.Plan.Meals
            .Where(m => m.Date == date && m.SlotType >= minSlot)
            .SelectMany(m => m.Portions.Where(p => p.UserId == request.UserId)
                .Select(p => new RemainingMeal(m.Id, db.Recipes.First(r => r.Id == m.RecipeId).Name, m.SlotType.ToString(), p.Kcal)))
            .ToList();

        var proposal = RecalcEngine.Propose(request.Kcal, remainingMeals);

        RecalcSuggestion? suggestion = null;
        if (proposal.Adjustments.Count > 0 || proposal.UnabsorbedKcal > 0)
        {
            suggestion = new RecalcSuggestion
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Date = date,
                PayloadJson = JsonSerializer.Serialize(proposal),
                Status = SuggestionStatus.Pending,
                FoodLogId = log.Id,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Suggestions.Add(suggestion);
        }

        return Task.FromResult(new LogFoodResponse(
            Mapping.ToDto(log), suggestion is null ? null : Mapping.ToDto(suggestion)));
    }

    public Task<IReadOnlyList<FoodLogDto>> GetForDayAsync(Guid userId, DateOnly date, CancellationToken ct = default)
    {
        IReadOnlyList<FoodLogDto> logs = db.FoodLogs
            .Where(l => l.UserId == userId && l.Date == date)
            .Select(Mapping.ToDto)
            .ToList();
        return Task.FromResult(logs);
    }
}

public class MockSuggestionService(MockDb db) : ISuggestionService
{
    public Task<IReadOnlyList<SuggestionDto>> GetPendingAsync(Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<SuggestionDto> pending = db.Suggestions
            .Where(s => s.UserId == userId && s.Status == SuggestionStatus.Pending)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(Mapping.ToDto)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<SuggestionDto?> AcceptAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = db.Suggestions.FirstOrDefault(s => s.Id == suggestionId && s.Status == SuggestionStatus.Pending);
        if (suggestion is null) return Task.FromResult<SuggestionDto?>(null);

        var proposal = JsonSerializer.Deserialize<RecalcProposal>(suggestion.PayloadJson)!;
        foreach (var adjustment in proposal.Adjustments)
        {
            var portion = db.Plan.Meals.FirstOrDefault(m => m.Id == adjustment.PlannedMealId)?
                .Portions.FirstOrDefault(p => p.UserId == suggestion.UserId);
            if (portion is null) continue;

            portion.Kcal = Math.Round(portion.Kcal * adjustment.Scale);
            portion.ProteinG = Math.Round(portion.ProteinG * adjustment.Scale, 1);
            portion.CarbsG = Math.Round(portion.CarbsG * adjustment.Scale, 1);
            portion.FatG = Math.Round(portion.FatG * adjustment.Scale, 1);
            portion.IngredientGrams = portion.IngredientGrams
                .ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value * adjustment.Scale / 5m) * 5m);
        }

        suggestion.Status = SuggestionStatus.Accepted;
        return Task.FromResult<SuggestionDto?>(Mapping.ToDto(suggestion));
    }

    public Task<SuggestionDto?> DismissAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = db.Suggestions.FirstOrDefault(s => s.Id == suggestionId && s.Status == SuggestionStatus.Pending);
        if (suggestion is null) return Task.FromResult<SuggestionDto?>(null);
        suggestion.Status = SuggestionStatus.Dismissed;
        return Task.FromResult<SuggestionDto?>(Mapping.ToDto(suggestion));
    }
}

/// <summary>
/// Dev-only auth for DataSource=Mock: any credentials sign in as the demo
/// user (Alin) with real, signed JWTs — so the frontend auth flow can be built
/// before the database exists. Google sign-in is not available in mock mode.
/// </summary>
public class MockAuthService(MockDb db, JwtTokenService jwt) : IAuthService
{
    private AuthResponse Issue(Guid userId)
    {
        var user = db.Users.First(u => u.Id == userId);
        var (token, expires) = jwt.IssueAccessToken(user.Id, user.Email, user.DisplayName);
        return new AuthResponse(token, JwtTokenService.NewRefreshToken(), expires, user.Id, user.DisplayName, user.Email);
    }

    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        Task.FromResult(Issue(MockDb.AlinId));

    public Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        Task.FromResult<AuthResponse?>(Issue(MockDb.AlinId));

    public Task<AuthResponse?> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken ct = default) =>
        Task.FromResult<AuthResponse?>(null); // requires real Google token validation → Sql mode

    public Task<AuthResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct = default) =>
        Task.FromResult<AuthResponse?>(Issue(MockDb.AlinId));
}
