using System.Text.Json;
using MacroSync.Application;
using MacroSync.Domain;
using MacroSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MacroSync.Infrastructure.Sql;

// EF Core implementations of the Application contracts — active when
// DataSource=Sql. Same DTO shapes as the mock services, so the frontend
// cannot tell the difference.

internal static class SqlMapping
{
    public static async Task<List<MemberDto>> LoadMembersAsync(MacroSyncDbContext db, Guid householdId, CancellationToken ct)
    {
        return await db.HouseholdMembers
            .Where(m => m.HouseholdId == householdId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .Join(db.NutritionProfiles.Where(p => p.IsActive), x => x.u.Id, p => p.UserId,
                (x, p) => new MemberDto(x.u.Id, x.u.DisplayName, x.m.Role.ToString(), p.DietType.ToString(),
                    p.CalorieTarget, p.ProteinG, p.CarbsG, p.FatG))
            .ToListAsync(ct);
    }

    public static PlannedMealDto ToDto(PlannedMeal meal, string recipeName, IReadOnlyDictionary<Guid, Ingredient> ingredientsById) =>
        new(meal.Id, meal.Date.ToString("yyyy-MM-dd"), meal.SlotType.ToString(), meal.RecipeId, recipeName,
            meal.Portions.Select(p => new PortionDto(
                p.UserId, Solving.PortionSummary(p.IngredientGrams, ingredientsById),
                p.Kcal, p.ProteinG, p.CarbsG, p.FatG, KcalDelta: 0)).ToList());

    public static SuggestionDto ToDto(RecalcSuggestion s)
    {
        var proposal = JsonSerializer.Deserialize<RecalcProposal>(s.PayloadJson)!;
        return new SuggestionDto(s.Id, s.UserId, s.Date.ToString("yyyy-MM-dd"), s.Status.ToString(),
            proposal.OverageKcal, proposal.AbsorbedKcal, proposal.UnabsorbedKcal,
            proposal.Adjustments.Select(a => new MealAdjustmentDto(
                a.PlannedMealId, a.RecipeName, a.SlotType, a.OldKcal, a.NewKcal, a.Scale)).ToList());
    }
}

public class SqlHouseholdService(MacroSyncDbContext db) : IHouseholdService
{
    public async Task<HouseholdDto?> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        var household = await db.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == householdId, ct);
        if (household is null) return null;
        var members = await SqlMapping.LoadMembersAsync(db, householdId, ct);
        return new HouseholdDto(household.Id, household.Name, household.InviteCode, members);
    }

    public async Task<HouseholdDto?> JoinAsync(Guid householdId, string inviteCode, Guid userId, CancellationToken ct = default)
    {
        var household = await db.Households
            .FirstOrDefaultAsync(h => h.Id == householdId && h.InviteCode == inviteCode, ct);
        if (household is null) return null;

        var alreadyMember = await db.HouseholdMembers
            .AnyAsync(m => m.HouseholdId == householdId && m.UserId == userId, ct);
        if (!alreadyMember)
        {
            db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = householdId, UserId = userId, Role = HouseholdRole.Member });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(householdId, ct);
    }
}

public class SqlRecipeService(MacroSyncDbContext db) : IRecipeService
{
    public async Task<IReadOnlyList<RecipeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var recipes = await db.Recipes.AsNoTracking().Include(r => r.Ingredients).ToListAsync(ct);
        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);

        return recipes.Select(r => new RecipeDto(
            r.Id, r.Name, r.Servings, r.Instructions, IsCurated: r.OwnerId is null,
            r.Ingredients.Select(ri => new RecipeIngredientDto(
                ri.IngredientId, ingredients[ri.IngredientId].Name, ri.QuantityG,
                ri.IsDivisible, ingredients[ri.IngredientId].KcalPer100G)).ToList())).ToList();
    }
}

public class SqlProfileService(MacroSyncDbContext db) : IProfileService
{
    public async Task<MemberDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        // New active row instead of update-in-place — past days keep their target (§3.2).
        await db.NutritionProfiles
            .Where(p => p.UserId == userId && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);

        db.NutritionProfiles.Add(new NutritionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CalorieTarget = request.CalorieTarget,
            ProteinG = request.ProteinG,
            CarbsG = request.CarbsG,
            FatG = request.FatG,
            DietType = Enum.Parse<DietType>(request.DietType),
            IsComputed = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var role = await db.HouseholdMembers.Where(m => m.UserId == userId)
            .Select(m => m.Role.ToString()).FirstOrDefaultAsync(ct) ?? "Member";
        return new MemberDto(userId, user.DisplayName, role, request.DietType,
            request.CalorieTarget, request.ProteinG, request.CarbsG, request.FatG);
    }
}

public class SqlMealPlanService(MacroSyncDbContext db) : IMealPlanService
{
    public async Task<WeekPlanDto?> GetWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.AsNoTracking()
            .Include(p => p.Meals).ThenInclude(m => m.Portions)
            .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.WeekStartDate == weekStart, ct);
        if (plan is null) return null;

        var members = await SqlMapping.LoadMembersAsync(db, householdId, ct);
        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var recipeNames = await db.Recipes.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var days = new List<DayPlanDto>();
        for (var d = 0; d < 7; d++)
        {
            var date = plan.WeekStartDate.AddDays(d);
            var dayMeals = plan.Meals.Where(m => m.Date == date).OrderBy(m => m.SlotType)
                .Select(m => SqlMapping.ToDto(m, recipeNames[m.RecipeId], ingredients))
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

            days.Add(new DayPlanDto(date.ToString("yyyy-MM-dd"), dayMeals, totals));
        }

        return new WeekPlanDto(plan.Id, householdId, plan.WeekStartDate.ToString("yyyy-MM-dd"), members, days);
    }

    public async Task<WeekPlanDto?> CreateWeekPlanAsync(Guid householdId, DateOnly weekStart, CancellationToken ct = default)
    {
        var exists = await db.Households.AnyAsync(h => h.Id == householdId, ct);
        if (!exists) return null;

        var plan = await db.MealPlans
            .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.WeekStartDate == weekStart, ct);
        if (plan is null)
        {
            plan = new MealPlan { Id = Guid.NewGuid(), HouseholdId = householdId, WeekStartDate = weekStart };
            db.MealPlans.Add(plan);
            await db.SaveChangesAsync(ct);
        }
        return await GetWeekPlanAsync(householdId, weekStart, ct);
    }

    public async Task<PlannedMealDto?> AddMealAsync(Guid planId, AddMealRequest request, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        var recipe = await db.Recipes.Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);
        if (plan is null || recipe is null) return null;

        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var eaters = await db.HouseholdMembers.Where(m => m.HouseholdId == plan.HouseholdId)
            .Join(db.NutritionProfiles.Where(p => p.IsActive), m => m.UserId, p => p.UserId,
                (m, p) => new { m.UserId, p.CalorieTarget })
            .ToListAsync(ct);

        var meal = Solving.SolvePlannedMeal(
            plan.Id, DateOnly.Parse(request.Date), Enum.Parse<SlotType>(request.SlotType),
            recipe, ingredients, eaters.Select(e => (e.UserId, e.CalorieTarget)).ToList());

        db.PlannedMeals.Add(meal);
        await db.SaveChangesAsync(ct);
        return SqlMapping.ToDto(meal, recipe.Name, ingredients);
    }

    public async Task<PlannedMealDto?> SolveMealAsync(Guid plannedMealId, SolveMealRequest request, CancellationToken ct = default)
    {
        var meal = await db.PlannedMeals.Include(m => m.Portions)
            .FirstOrDefaultAsync(m => m.Id == plannedMealId, ct);
        if (meal is null) return null;

        var plan = await db.MealPlans.AsNoTracking().FirstAsync(p => p.Id == meal.MealPlanId, ct);
        var recipe = await db.Recipes.AsNoTracking().Include(r => r.Ingredients)
            .FirstAsync(r => r.Id == meal.RecipeId, ct);
        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);

        var skipped = request.SkippedUserIds ?? [];
        var eaters = await db.HouseholdMembers.Where(m => m.HouseholdId == plan.HouseholdId)
            .Join(db.NutritionProfiles.Where(p => p.IsActive), m => m.UserId, p => p.UserId,
                (m, p) => new { m.UserId, p.CalorieTarget })
            .ToListAsync(ct);
        var remaining = eaters.Where(e => !skipped.Contains(e.UserId))
            .Select(e => (e.UserId, e.CalorieTarget)).ToList();
        if (remaining.Count == 0) return null;

        var solved = Solving.SolvePlannedMeal(meal.MealPlanId, meal.Date, meal.SlotType, recipe, ingredients, remaining);

        db.MealPortions.RemoveRange(meal.Portions);
        meal.Portions.Clear();
        foreach (var portion in solved.Portions)
        {
            portion.PlannedMealId = meal.Id;
            meal.Portions.Add(portion);
        }
        await db.SaveChangesAsync(ct);
        return SqlMapping.ToDto(meal, recipe.Name, ingredients);
    }

    public async Task<GroceryListDto?> GetGroceryListAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.AsNoTracking()
            .Include(p => p.Meals).ThenInclude(m => m.Portions)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return null;

        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var items = plan.Meals.SelectMany(m => m.Portions)
            .SelectMany(p => p.IngredientGrams)
            .GroupBy(kv => kv.Key)
            .Select(g => new GroceryItemDto(ingredients[g.Key].Name, ingredients[g.Key].Aisle, g.Sum(kv => kv.Value)))
            .OrderBy(i => i.Aisle).ThenBy(i => i.Name)
            .ToList();

        return new GroceryListDto(planId, plan.WeekStartDate.ToString("yyyy-MM-dd"), items);
    }

    public async Task<ShareLinkDto?> CreateShareLinkAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return null;

        if (plan.ShareToken is null)
        {
            // Capability URL: random 128-bit token, read-only, revocable (§5.3).
            plan.ShareToken = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
        }
        return new ShareLinkDto(plan.ShareToken, $"/api/v1/grocery-lists/{plan.ShareToken}");
    }

    public async Task<GroceryListDto?> GetGroceryListByTokenAsync(string shareToken, CancellationToken ct = default)
    {
        var planId = await db.MealPlans.AsNoTracking()
            .Where(p => p.ShareToken == shareToken)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        return planId is null ? null : await GetGroceryListAsync(planId.Value, ct);
    }
}

public class SqlFoodLogService(MacroSyncDbContext db) : IFoodLogService
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

        // Remaining meals today for this user → rules-based proposal (§5.4).
        var remainingMeals = await RemainingMealsAsync(request.UserId, date, ct);
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
            db.RecalcSuggestions.Add(suggestion);
        }

        await db.SaveChangesAsync(ct);
        return new LogFoodResponse(ToDto(log), suggestion is null ? null : SqlMapping.ToDto(suggestion));
    }

    public async Task<IReadOnlyList<FoodLogDto>> GetForDayAsync(Guid userId, DateOnly date, CancellationToken ct = default)
    {
        var logs = await db.FoodLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.Date == date)
            .OrderBy(l => l.Id)
            .ToListAsync(ct);
        return logs.Select(ToDto).ToList();
    }

    private async Task<List<RemainingMeal>> RemainingMealsAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        // v1 heuristic: when logging for today, only meals in later slots than the
        // current time can still absorb the overage; other days count all slots.
        var minSlot = date != DateOnly.FromDateTime(DateTime.Now) ? SlotType.Breakfast
            : DateTime.Now.Hour < 11 ? SlotType.Lunch
            : DateTime.Now.Hour < 17 ? SlotType.Dinner
            : SlotType.Snack;

        return await db.PlannedMeals.AsNoTracking()
            .Where(m => m.Date == date && m.SlotType >= minSlot)
            .Join(db.Recipes, m => m.RecipeId, r => r.Id, (m, r) => new { m, r.Name })
            .SelectMany(x => x.m.Portions.Where(p => p.UserId == userId),
                (x, p) => new RemainingMeal(x.m.Id, x.Name, x.m.SlotType.ToString(), p.Kcal))
            .ToListAsync(ct);
    }

    private static FoodLogDto ToDto(FoodLog log) => new(
        log.Id, log.UserId, log.Date.ToString("yyyy-MM-dd"), log.Source.ToString(),
        log.Description, log.Kcal, log.ProteinG, log.CarbsG, log.FatG);
}

public class SqlSuggestionService(MacroSyncDbContext db) : ISuggestionService
{
    public async Task<IReadOnlyList<SuggestionDto>> GetPendingAsync(Guid userId, CancellationToken ct = default)
    {
        var suggestions = await db.RecalcSuggestions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == SuggestionStatus.Pending)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        return suggestions.Select(SqlMapping.ToDto).ToList();
    }

    public async Task<SuggestionDto?> AcceptAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = await db.RecalcSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.Status == SuggestionStatus.Pending, ct);
        if (suggestion is null) return null;

        var proposal = JsonSerializer.Deserialize<RecalcProposal>(suggestion.PayloadJson)!;
        foreach (var adjustment in proposal.Adjustments)
        {
            var portion = await db.MealPortions.FirstOrDefaultAsync(
                p => p.PlannedMealId == adjustment.PlannedMealId && p.UserId == suggestion.UserId, ct);
            if (portion is null) continue;

            portion.Kcal = Math.Round(portion.Kcal * adjustment.Scale);
            portion.ProteinG = Math.Round(portion.ProteinG * adjustment.Scale, 1);
            portion.CarbsG = Math.Round(portion.CarbsG * adjustment.Scale, 1);
            portion.FatG = Math.Round(portion.FatG * adjustment.Scale, 1);
            portion.IngredientGrams = portion.IngredientGrams
                .ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value * adjustment.Scale / 5m) * 5m);
        }

        suggestion.Status = SuggestionStatus.Accepted;
        await db.SaveChangesAsync(ct);
        return SqlMapping.ToDto(suggestion);
    }

    public async Task<SuggestionDto?> DismissAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = await db.RecalcSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.Status == SuggestionStatus.Pending, ct);
        if (suggestion is null) return null;

        suggestion.Status = SuggestionStatus.Dismissed;
        await db.SaveChangesAsync(ct);
        return SqlMapping.ToDto(suggestion);
    }
}
