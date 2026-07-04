using MacroSync.Infrastructure.Mocks;
using Microsoft.EntityFrameworkCore;

namespace MacroSync.Infrastructure.Persistence;

/// <summary>
/// Seeds an empty database with the demo household + curated recipe library
/// (Tech Design §8: local env ships with seeded demo data). Reuses MockDb as
/// the single source of seed truth. Dev convenience only — production data
/// arrives through the API.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(MacroSyncDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var seed = new MockDb();
        db.Users.AddRange(seed.Users);
        db.Households.Add(seed.Household);
        db.NutritionProfiles.AddRange(seed.Profiles);
        db.Ingredients.AddRange(seed.Ingredients);
        db.Recipes.AddRange(seed.Recipes);
        db.MealPlans.Add(seed.Plan);
        await db.SaveChangesAsync(ct);
    }
}
