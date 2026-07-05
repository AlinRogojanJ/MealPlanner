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
                a.PlannedMealId, a.RecipeName, a.SlotType, a.OldKcal, a.NewKcal, a.Scale)).ToList(),
            proposal.Source);
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

public class MockMembershipService(MockDb db) : IMembershipService
{
    public Task<bool> IsMemberAsync(Guid userId, Guid householdId, CancellationToken ct = default) =>
        Task.FromResult(householdId == db.Household.Id && db.Household.Members.Any(m => m.UserId == userId));

    public Task<bool> ShareHouseholdAsync(Guid userId, Guid otherUserId, CancellationToken ct = default) =>
        Task.FromResult(db.Household.Members.Any(m => m.UserId == userId)
            && db.Household.Members.Any(m => m.UserId == otherUserId));

    public Task<Guid?> GetHouseholdIdForPlanAsync(Guid planId, CancellationToken ct = default) =>
        Task.FromResult(db.Plans.FirstOrDefault(p => p.Id == planId) is { } plan ? (Guid?)plan.HouseholdId : null);

    public Task<Guid?> GetHouseholdIdForMealAsync(Guid plannedMealId, CancellationToken ct = default) =>
        Task.FromResult(db.Plans.FirstOrDefault(p => p.Meals.Any(m => m.Id == plannedMealId)) is { } plan
            ? (Guid?)plan.HouseholdId : null);

    public Task<Guid?> GetSuggestionOwnerAsync(Guid suggestionId, CancellationToken ct = default) =>
        Task.FromResult(db.Suggestions.FirstOrDefault(s => s.Id == suggestionId) is { } s ? (Guid?)s.UserId : null);
}

public class MockMealPlanService(MockDb db) : IMealPlanService
{
    public Task<WeekPlanDto?> GetWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default)
    {
        // Weeks that don't exist yet 404 so the frontend shows its honest empty state.
        var plan = db.Plans.FirstOrDefault(p => p.WeekStartDate == weekStart);
        if (householdId != db.Household.Id || plan is null)
            return Task.FromResult<WeekPlanDto?>(null);

        return Task.FromResult<WeekPlanDto?>(BuildWeekDto(plan, householdId));
    }

    public Task<WeekPlanDto?> CreateWeekPlanAsync(Guid householdId, DateOnly weekStart, DateOnly? copyFrom = null, CancellationToken ct = default)
    {
        if (householdId != db.Household.Id) return Task.FromResult<WeekPlanDto?>(null);

        var plan = db.Plans.FirstOrDefault(p => p.WeekStartDate == weekStart);
        if (plan is null)
        {
            plan = new MealPlan { Id = Guid.NewGuid(), HouseholdId = householdId, WeekStartDate = weekStart };
            db.Plans.Add(plan);

            var source = copyFrom is null ? null : db.Plans.FirstOrDefault(p => p.WeekStartDate == copyFrom);
            if (source is not null)
            {
                // Copy the source week's menu, re-solved against current targets.
                var eaters = db.Profiles.Where(p => p.IsActive).Select(p => (p.UserId, p.CalorieTarget)).ToList();
                foreach (var sourceMeal in source.Meals)
                {
                    var date = weekStart.AddDays(sourceMeal.Date.DayNumber - source.WeekStartDate.DayNumber);
                    var recipe = db.Recipes.First(r => r.Id == sourceMeal.RecipeId);
                    plan.Meals.Add(Solving.SolvePlannedMeal(plan.Id, date, sourceMeal.SlotType, recipe, db.IngredientsById, eaters));
                }
            }
        }
        return Task.FromResult<WeekPlanDto?>(BuildWeekDto(plan, householdId));
    }

    public Task<bool> DeleteMealAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        foreach (var plan in db.Plans)
        {
            var meal = plan.Meals.FirstOrDefault(m => m.Id == plannedMealId);
            if (meal is not null) return Task.FromResult(plan.Meals.Remove(meal));
        }
        return Task.FromResult(false);
    }

    public Task<PlannedMealDto?> MoveMealAsync(Guid plannedMealId, DateOnly date, string slotType, CancellationToken ct = default)
    {
        var meal = db.Plans.SelectMany(p => p.Meals).FirstOrDefault(m => m.Id == plannedMealId);
        if (meal is null) return Task.FromResult<PlannedMealDto?>(null);

        meal.Date = date;
        meal.SlotType = Enum.Parse<SlotType>(slotType);

        // Slot budget changed → keep the same eaters, re-solve portions.
        var currentEaters = meal.Portions.Select(p => p.UserId).ToHashSet();
        var eaters = db.Profiles.Where(p => p.IsActive && currentEaters.Contains(p.UserId))
            .Select(p => (p.UserId, p.CalorieTarget)).ToList();
        var recipe = db.Recipes.First(r => r.Id == meal.RecipeId);
        var solved = Solving.SolvePlannedMeal(meal.MealPlanId, date, meal.SlotType, recipe, db.IngredientsById, eaters);

        meal.Portions.Clear();
        foreach (var portion in solved.Portions)
        {
            portion.PlannedMealId = meal.Id;
            meal.Portions.Add(portion);
        }
        return Task.FromResult<PlannedMealDto?>(db.ToDto(meal));
    }

    private WeekPlanDto BuildWeekDto(MealPlan plan, Guid householdId)
    {
        var members = db.Household.Members.Select(db.ToMemberDto).ToList();
        var days = new List<DayPlanDto>();

        for (var d = 0; d < 7; d++)
        {
            var date = plan.WeekStartDate.AddDays(d);
            var meals = plan.Meals
                .Where(m => m.Date == date)
                .OrderBy(m => m.SlotType)
                .Select(db.ToDto)
                .ToList();

            var totals = members.Select(member =>
            {
                var portions = plan.Meals.Where(m => m.Date == date)
                    .SelectMany(m => m.Portions).Where(p => p.UserId == member.UserId).ToList();
                var kcal = portions.Sum(p => p.Kcal);
                return new DailyTotalDto(member.UserId, kcal,
                    portions.Sum(p => p.ProteinG), portions.Sum(p => p.CarbsG), portions.Sum(p => p.FatG),
                    member.CalorieTarget, kcal - member.CalorieTarget);
            }).ToList();

            days.Add(new DayPlanDto(date.ToString("yyyy-MM-dd"), meals, totals));
        }

        return new WeekPlanDto(plan.Id, householdId, plan.WeekStartDate.ToString("yyyy-MM-dd"), members, days);
    }

    public Task<WeekPlanDto?> CopyDayAsync(Guid planId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var plan = db.Plans.FirstOrDefault(p => p.Id == planId);
        if (plan is null || !WithinWeek(plan, fromDate) || !WithinWeek(plan, toDate))
            return Task.FromResult<WeekPlanDto?>(null);

        if (fromDate != toDate)
        {
            plan.Meals.RemoveAll(m => m.Date == toDate);
            var eaters = db.Profiles.Where(p => p.IsActive).Select(p => (p.UserId, p.CalorieTarget)).ToList();
            foreach (var sourceMeal in plan.Meals.Where(m => m.Date == fromDate).ToList())
            {
                var recipe = db.Recipes.First(r => r.Id == sourceMeal.RecipeId);
                plan.Meals.Add(Solving.SolvePlannedMeal(plan.Id, toDate, sourceMeal.SlotType, recipe, db.IngredientsById, eaters));
            }
        }
        return Task.FromResult<WeekPlanDto?>(BuildWeekDto(plan, plan.HouseholdId));
    }

    private static bool WithinWeek(MealPlan plan, DateOnly date) =>
        date >= plan.WeekStartDate && date < plan.WeekStartDate.AddDays(7);

    public Task<PlannedMealDto?> AddMealAsync(Guid planId, AddMealRequest request, CancellationToken ct = default)
    {
        var plan = db.Plans.FirstOrDefault(p => p.Id == planId);
        var recipe = db.Recipes.FirstOrDefault(r => r.Id == request.RecipeId);
        if (plan is null || recipe is null) return Task.FromResult<PlannedMealDto?>(null);

        var eaters = db.Profiles.Where(p => p.IsActive).Select(p => (p.UserId, p.CalorieTarget)).ToList();
        var meal = Solving.SolvePlannedMeal(plan.Id, DateOnly.Parse(request.Date),
            Enum.Parse<SlotType>(request.SlotType), recipe, db.IngredientsById, eaters);
        plan.Meals.Add(meal);
        return Task.FromResult<PlannedMealDto?>(db.ToDto(meal));
    }

    public Task<PlannedMealDto?> SolveMealAsync(Guid plannedMealId, SolveMealRequest request, CancellationToken ct = default)
    {
        var meal = db.Plans.SelectMany(p => p.Meals).FirstOrDefault(m => m.Id == plannedMealId);
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
        var plan = db.Plans.FirstOrDefault(p => p.Id == planId);
        if (plan is null) return Task.FromResult<GroceryListDto?>(null);

        var items = plan.Meals
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
            new GroceryListDto(planId, plan.WeekStartDate.ToString("yyyy-MM-dd"), items));
    }

    public Task<ShareLinkDto?> CreateShareLinkAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = db.Plans.FirstOrDefault(p => p.Id == planId);
        if (plan is null) return Task.FromResult<ShareLinkDto?>(null);
        plan.ShareToken ??= Guid.NewGuid().ToString("N");
        return Task.FromResult<ShareLinkDto?>(
            new ShareLinkDto(plan.ShareToken, $"/api/v1/grocery-lists/{plan.ShareToken}"));
    }

    public Task<GroceryListDto?> GetGroceryListByTokenAsync(string shareToken, CancellationToken ct = default)
    {
        var plan = db.Plans.FirstOrDefault(p => p.ShareToken == shareToken);
        return plan is null
            ? Task.FromResult<GroceryListDto?>(null)
            : GetGroceryListAsync(plan.Id, ct);
    }
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

public class MockFoodLogService(MockDb db, Ai.RecalcPlanner planner) : IFoodLogService
{
    public async Task<LogFoodResponse> LogAsync(LogFoodRequest request, CancellationToken ct = default)
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

        var remainingMeals = db.Plans.SelectMany(p => p.Meals)
            .Where(m => m.Date == date && m.SlotType >= minSlot)
            .SelectMany(m => m.Portions.Where(p => p.UserId == request.UserId)
                .Select(p => new RemainingMeal(m.Id, db.Recipes.First(r => r.Id == m.RecipeId).Name, m.SlotType.ToString(), p.Kcal)))
            .ToList();

        var dietType = db.Profiles.FirstOrDefault(p => p.UserId == request.UserId && p.IsActive)
            ?.DietType.ToString() ?? "Maintain";
        var proposal = await planner.ProposeAsync(request.Description, request.Kcal, dietType, remainingMeals, ct);

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

        return new LogFoodResponse(
            Mapping.ToDto(log), suggestion is null ? null : Mapping.ToDto(suggestion));
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
            var portion = db.Plans.SelectMany(p => p.Meals)
                .FirstOrDefault(m => m.Id == adjustment.PlannedMealId)?
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

public class MockRecommendationService(MockDb db, IAiAdvisor advisor) : IRecommendationService
{
    public async Task<IReadOnlyList<MealRecommendationDto>> RecommendAsync(
        Guid planId, DateOnly date, string slotType, CancellationToken ct = default)
    {
        var plan = db.Plans.FirstOrDefault(p => p.Id == planId);
        if (plan is null) return [];

        var eaters = db.Household.Members
            .Select(m =>
            {
                var user = db.Users.First(u => u.Id == m.UserId);
                var profile = db.Profiles.First(p => p.UserId == m.UserId && p.IsActive);
                return new Ai.RecEater(user.Id, user.DisplayName, profile.DietType.ToString(), profile.CalorieTarget);
            })
            .ToList();

        // Variety signal: dishes already planned in the two days before this slot.
        var recent = plan.Meals
            .Where(m => m.Date <= date && m.Date >= date.AddDays(-2))
            .Select(m => db.Recipes.First(r => r.Id == m.RecipeId).Name)
            .Distinct()
            .ToList();

        return await Ai.RecommendationEngine.RecommendAsync(
            advisor, slotType, db.Recipes, db.IngredientsById, eaters, recent, ct);
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
