using MacroSync.Application;
using MacroSync.Domain;
using MacroSync.Infrastructure.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSync.Infrastructure;

// Mock-backed implementations of the Application interfaces. When EF Core
// arrives these are replaced by SQL-backed services; the contracts don't move.

public static class InfrastructureModule
{
    public static IServiceCollection AddMacroSyncInfrastructure(this IServiceCollection services) =>
        services
            .AddSingleton<MockDb>()
            .AddSingleton<IHouseholdService, MockHouseholdService>()
            .AddSingleton<IMealPlanService, MockMealPlanService>()
            .AddSingleton<IRecipeService, MockRecipeService>();
}

internal static class Mapping
{
    public static MemberDto ToMemberDto(this MockDb db, HouseholdMember m)
    {
        var user = db.Users.First(u => u.Id == m.UserId);
        var profile = db.Profiles.First(p => p.UserId == m.UserId && p.IsActive);
        return new MemberDto(user.Id, user.DisplayName, m.Role.ToString(), profile.DietType.ToString(),
            profile.CalorieTarget, profile.ProteinG, profile.CarbsG, profile.FatG);
    }

    /// <summary>"250g Chicken breast + 200g Sweet potato" — top ingredients by weight.</summary>
    public static string PortionSummary(this MockDb db, MealPortion portion)
    {
        var parts = portion.IngredientGrams
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv =>
            {
                var ing = db.Ingredients.First(i => i.Id == kv.Key);
                return ing.Name == "Egg"
                    ? $"{(int)(kv.Value / 60)} egg{(kv.Value > 60 ? "s" : "")}"
                    : $"{kv.Value:0}g {ing.Name.ToLowerInvariant()}";
            });
        return string.Join(" + ", parts);
    }
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
                .Select(m => new PlannedMealDto(
                    m.Id, date.ToString("yyyy-MM-dd"), m.SlotType.ToString(), m.RecipeId,
                    db.Recipes.First(r => r.Id == m.RecipeId).Name,
                    m.Portions.Select(p => new PortionDto(
                        p.UserId, db.PortionSummary(p), p.Kcal, p.ProteinG, p.CarbsG, p.FatG,
                        KcalDelta: 0)).ToList()))
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

    public Task<GroceryListDto?> GetGroceryListAsync(Guid planId, CancellationToken ct = default)
    {
        if (planId != db.Plan.Id) return Task.FromResult<GroceryListDto?>(null);

        var items = db.Plan.Meals
            .SelectMany(m => m.Portions)
            .SelectMany(p => p.IngredientGrams)
            .GroupBy(kv => kv.Key)
            .Select(g =>
            {
                var ing = db.Ingredients.First(i => i.Id == g.Key);
                return new GroceryItemDto(ing.Name, ing.Aisle, g.Sum(kv => kv.Value));
            })
            .OrderBy(i => i.Aisle).ThenBy(i => i.Name)
            .ToList();

        return Task.FromResult<GroceryListDto?>(
            new GroceryListDto(planId, db.Plan.WeekStartDate.ToString("yyyy-MM-dd"), items));
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
                var ing = db.Ingredients.First(i => i.Id == ri.IngredientId);
                return new RecipeIngredientDto(ing.Id, ing.Name, ri.QuantityG, ri.IsDivisible, ing.KcalPer100G);
            }).ToList())).ToList();
        return Task.FromResult(recipes);
    }
}
