using System.Text.Json;
using MacroSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MacroSync.Infrastructure.Persistence;

// Code-first schema per Tech Design §3.1. English naming, plural table names.
// Migrations live next to this context and ship through the pipeline.

public class MacroSyncDbContext(DbContextOptions<MacroSyncDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<NutritionProfile> NutritionProfiles => Set<NutritionProfile>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
    public DbSet<MealPortion> MealPortions => Set<MealPortion>();
    public DbSet<FoodLog> FoodLogs => Set<FoodLog>();
    public DbSet<RecalcSuggestion> RecalcSuggestions => Set<RecalcSuggestion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(60);
        });

        b.Entity<Household>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.InviteCode).HasMaxLength(20);
            e.HasIndex(x => x.InviteCode).IsUnique();
            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.HouseholdId);
        });

        b.Entity<HouseholdMember>(e =>
        {
            e.HasKey(x => new { x.UserId, x.HouseholdId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<NutritionProfile>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            // One active profile per user; history kept (§3.2).
            e.HasIndex(x => new { x.UserId, x.IsActive }).HasFilter("[IsActive] = 1").IsUnique();
        });

        b.Entity<Recipe>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasMany(x => x.Ingredients).WithOne().HasForeignKey(ri => ri.RecipeId);
        });

        b.Entity<Ingredient>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Aisle).HasMaxLength(60);
            e.Property(x => x.ExternalFoodDbId).HasMaxLength(100);
            e.Property(x => x.KcalPer100G).HasPrecision(9, 2);
            e.Property(x => x.ProteinPer100G).HasPrecision(9, 2);
            e.Property(x => x.CarbsPer100G).HasPrecision(9, 2);
            e.Property(x => x.FatPer100G).HasPrecision(9, 2);
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.HasKey(x => new { x.RecipeId, x.IngredientId });
            e.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId);
            e.Property(x => x.QuantityG).HasPrecision(9, 2);
        });

        b.Entity<MealPlan>(e =>
        {
            e.HasIndex(x => new { x.HouseholdId, x.WeekStartDate }).IsUnique();
            e.HasIndex(x => x.ShareToken).IsUnique().HasFilter("[ShareToken] IS NOT NULL");
            e.Property(x => x.ShareToken).HasMaxLength(64);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId);
            e.HasMany(x => x.Meals).WithOne().HasForeignKey(m => m.MealPlanId);
        });

        b.Entity<PlannedMeal>(e =>
        {
            e.HasIndex(x => new { x.MealPlanId, x.Date, x.SlotType });
            e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId);
            e.HasMany(x => x.Portions).WithOne().HasForeignKey(p => p.PlannedMealId);
        });

        b.Entity<MealPortion>(e =>
        {
            e.HasKey(x => new { x.PlannedMealId, x.UserId });
            e.Property(x => x.Kcal).HasPrecision(9, 2);
            e.Property(x => x.ProteinG).HasPrecision(9, 2);
            e.Property(x => x.CarbsG).HasPrecision(9, 2);
            e.Property(x => x.FatG).HasPrecision(9, 2);

            // Solver output stored as a JSON column (§3.2) — read as a unit, never queried per-ingredient.
            e.Property(x => x.IngredientGrams)
                .HasColumnType("nvarchar(max)")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(v, (JsonSerializerOptions?)null) ?? new(),
                    new ValueComparer<Dictionary<Guid, decimal>>(
                        (a, z) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(z, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                        v => JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        });

        b.Entity<FoodLog>(e =>
        {
            // Append-only (§3.2); edits create a superseding row.
            e.HasIndex(x => new { x.UserId, x.Date });
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.Kcal).HasPrecision(9, 2);
            e.Property(x => x.ProteinG).HasPrecision(9, 2);
            e.Property(x => x.CarbsG).HasPrecision(9, 2);
            e.Property(x => x.FatG).HasPrecision(9, 2);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<RecalcSuggestion>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Date, x.Status });
            e.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.HasIndex(x => x.FamilyId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.Ignore(x => x.IsActive);
        });

        b.Entity<ExternalLogin>(e =>
        {
            e.HasKey(x => new { x.Provider, x.ProviderKey });
            e.Property(x => x.Provider).HasMaxLength(30);
            e.Property(x => x.ProviderKey).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });
    }
}
