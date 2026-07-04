namespace MacroSync.Domain;

// Core entities per Technical Design §3.1. Plain POCOs — EF Core configuration
// lives in Infrastructure once the database arrives.

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = "";
}

public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public List<HouseholdMember> Members { get; set; } = [];
}

public class HouseholdMember
{
    public Guid UserId { get; set; }
    public Guid HouseholdId { get; set; }
    public HouseholdRole Role { get; set; }
}

public class NutritionProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int CalorieTarget { get; set; }
    public int ProteinG { get; set; }
    public int CarbsG { get; set; }
    public int FatG { get; set; }
    public DietType DietType { get; set; }
    public bool IsComputed { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class Recipe
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Servings { get; set; }
    public string Instructions { get; set; } = "";
    public Guid? OwnerId { get; set; } // null = curated library
    public bool IsPublic { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public class Ingredient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal KcalPer100G { get; set; }
    public decimal ProteinPer100G { get; set; }
    public decimal CarbsPer100G { get; set; }
    public decimal FatPer100G { get; set; }
    public string? ExternalFoodDbId { get; set; }
    public string Aisle { get; set; } = "Other";
}

public class RecipeIngredient
{
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal QuantityG { get; set; }
    public bool IsDivisible { get; set; }
    public IndivisibleSplitRule SplitRule { get; set; } = IndivisibleSplitRule.Even;
}

public class MealPlan
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    /// <summary>Capability URL token for the anonymous read-only grocery share link.</summary>
    public string? ShareToken { get; set; }
    public List<PlannedMeal> Meals { get; set; } = [];
}

public class PlannedMeal
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public DateOnly Date { get; set; }
    public SlotType SlotType { get; set; }
    public Guid RecipeId { get; set; }
    public List<MealPortion> Portions { get; set; } = [];
}

public class MealPortion
{
    public Guid PlannedMealId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Per-ingredient computed grams — stored as a JSON column in SQL.</summary>
    public Dictionary<Guid, decimal> IngredientGrams { get; set; } = [];
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal FatG { get; set; }
}

public class FoodLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public FoodLogSource Source { get; set; }
    public string Description { get; set; } = "";
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal FatG { get; set; }
    public Guid? PlannedMealId { get; set; }
}

public class RecalcSuggestion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public SuggestionStatus Status { get; set; }
    public Guid? FoodLogId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    /// <summary>Rotation family — reuse of a revoked member invalidates the whole family.</summary>
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}

public class ExternalLogin
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = ""; // "Google"
    public string ProviderKey { get; set; } = ""; // Google `sub`
}
