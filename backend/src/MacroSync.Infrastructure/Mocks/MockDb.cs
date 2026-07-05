using MacroSync.Domain;

namespace MacroSync.Infrastructure.Mocks;

// In-memory mock database standing in for Azure SQL until EF Core lands.
// Everything the API serves in v0 is seeded here: a demo household with two
// eaters on very different targets (the motivating example from the product
// plan), a curated recipe library, and a fully planned week with portions
// computed by the real PortionSolver.

public sealed class MockDb
{
    public static readonly Guid HouseholdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AlinId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid MariaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public List<User> Users { get; } = [];
    public Household Household { get; }
    public List<NutritionProfile> Profiles { get; } = [];
    public List<Ingredient> Ingredients { get; } = [];
    public List<Recipe> Recipes { get; } = [];
    /// <summary>All plans; the seeded demo week is Plans[0] (also exposed as Plan).</summary>
    public List<MealPlan> Plans { get; } = [];
    public MealPlan Plan => Plans[0];
    public List<FoodLog> FoodLogs { get; } = [];
    public List<RecalcSuggestion> Suggestions { get; } = [];

    public IReadOnlyDictionary<Guid, Ingredient> IngredientsById => _ingredientsById ??= Ingredients.ToDictionary(i => i.Id);
    private Dictionary<Guid, Ingredient>? _ingredientsById;

    public Ingredient Ing(string name) => Ingredients.First(i => i.Name == name);
    public Recipe Rec(string name) => Recipes.First(r => r.Name == name);

    public MockDb()
    {
        Users.Add(new User { Id = AlinId, Email = "alin@example.com", DisplayName = "Alin" });
        Users.Add(new User { Id = MariaId, Email = "maria@example.com", DisplayName = "Maria" });

        Household = new Household
        {
            Id = HouseholdId,
            Name = "Alin & Maria",
            InviteCode = "MACRO-2026",
            Members =
            [
                new HouseholdMember { UserId = AlinId, HouseholdId = HouseholdId, Role = HouseholdRole.Owner },
                new HouseholdMember { UserId = MariaId, HouseholdId = HouseholdId, Role = HouseholdRole.Member },
            ],
        };

        // The 1,000-kcal-gap couple from the product plan.
        Profiles.Add(new NutritionProfile { Id = Guid.NewGuid(), UserId = AlinId, CalorieTarget = 2500, ProteinG = 190, CarbsG = 260, FatG = 75, DietType = DietType.Cut, IsActive = true });
        Profiles.Add(new NutritionProfile { Id = Guid.NewGuid(), UserId = MariaId, CalorieTarget = 1700, ProteinG = 130, CarbsG = 160, FatG = 55, DietType = DietType.Cut, IsActive = true });

        SeedIngredients();
        SeedRecipes();
        Plans.Add(BuildWeekPlan(new DateOnly(2026, 6, 29))); // Mon of the current demo week
    }

    private void AddIngredient(string name, decimal kcal, decimal p, decimal c, decimal f, string aisle) =>
        Ingredients.Add(new Ingredient { Id = Guid.NewGuid(), Name = name, KcalPer100G = kcal, ProteinPer100G = p, CarbsPer100G = c, FatPer100G = f, Aisle = aisle });

    private void SeedIngredients()
    {
        AddIngredient("Chicken breast", 165, 31, 0, 3.6m, "Meat & Fish");
        AddIngredient("Sweet potato", 86, 1.6m, 20, 0.1m, "Produce");
        AddIngredient("Olive oil", 884, 0, 0, 100, "Pantry");
        AddIngredient("Rolled oats", 389, 16.9m, 66, 6.9m, "Pantry");
        AddIngredient("Whey protein", 400, 80, 8, 6, "Pantry");
        AddIngredient("Mixed berries", 45, 0.7m, 10, 0.3m, "Produce");
        AddIngredient("Milk 1.5%", 47, 3.4m, 4.9m, 1.5m, "Dairy");
        AddIngredient("Beef sirloin", 201, 29, 0, 9, "Meat & Fish");
        AddIngredient("Basmati rice", 350, 8, 77, 1, "Pantry");
        AddIngredient("Stir-fry vegetables", 35, 2, 6, 0.3m, "Produce");
        AddIngredient("Soy sauce", 53, 8, 5, 0, "Pantry");
        AddIngredient("Greek yogurt 2%", 73, 10, 4, 2, "Dairy");
        AddIngredient("Honey", 304, 0.3m, 82, 0, "Pantry");
        AddIngredient("Granola", 471, 10, 64, 20, "Pantry");
        AddIngredient("Salmon fillet", 208, 20, 0, 13, "Meat & Fish");
        AddIngredient("Quinoa", 368, 14, 64, 6, "Pantry");
        AddIngredient("Broccoli", 34, 2.8m, 7, 0.4m, "Produce");
        AddIngredient("Turkey mince", 148, 20, 0, 7, "Meat & Fish");
        AddIngredient("Whole-wheat pasta", 348, 13, 71, 2.5m, "Pantry");
        AddIngredient("Tomato sauce", 32, 1.4m, 6, 0.3m, "Pantry");
        AddIngredient("Egg", 143, 12.6m, 0.7m, 9.5m, "Dairy");
        AddIngredient("Bell pepper", 26, 1, 6, 0.3m, "Produce");
        AddIngredient("Banana", 89, 1.1m, 23, 0.3m, "Produce");
    }

    private void AddRecipe(string name, int servings, string instructions,
        params (string Name, decimal QtyG, bool Divisible, IndivisibleSplitRule Rule)[] items)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), Name = name, Servings = servings, Instructions = instructions, OwnerId = null, IsPublic = true };
        foreach (var (ingName, qty, divisible, rule) in items)
            recipe.Ingredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = Ing(ingName).Id,
                QuantityG = qty,
                IsDivisible = divisible,
                SplitRule = rule,
            });
        Recipes.Add(recipe);
    }

    private void SeedRecipes()
    {
        const IndivisibleSplitRule even = IndivisibleSplitRule.Even;
        const IndivisibleSplitRule ratio = IndivisibleSplitRule.Ratio;

        AddRecipe("Grilled Chicken & Sweet Potato", 2, "Grill the chicken; roast sweet potato wedges at 200°C for 30 min.",
            ("Chicken breast", 400, true, even), ("Sweet potato", 500, true, even), ("Olive oil", 20, false, ratio));
        AddRecipe("Protein Oats with Berries", 2, "Cook oats in milk, stir in whey off the heat, top with berries.",
            ("Rolled oats", 140, true, even), ("Whey protein", 60, true, even), ("Milk 1.5%", 400, true, even), ("Mixed berries", 200, true, even));
        AddRecipe("Beef Stir-Fry with Rice", 2, "Sear beef strips, add vegetables and soy sauce; serve over rice.",
            ("Beef sirloin", 350, true, even), ("Basmati rice", 180, true, even), ("Stir-fry vegetables", 400, true, even), ("Soy sauce", 30, false, even));
        AddRecipe("Greek Yogurt Parfait", 2, "Layer yogurt, honey and granola in a glass.",
            ("Greek yogurt 2%", 400, true, even), ("Honey", 30, true, even), ("Granola", 80, true, even));
        AddRecipe("Salmon with Quinoa & Broccoli", 2, "Pan-sear salmon; steam broccoli; cook quinoa 1:2 in water.",
            ("Salmon fillet", 350, true, even), ("Quinoa", 150, true, even), ("Broccoli", 400, true, even), ("Olive oil", 15, false, ratio));
        AddRecipe("Turkey Meatballs & Pasta", 2, "Mix turkey with one egg, form meatballs, simmer in tomato sauce; serve on pasta.",
            ("Turkey mince", 400, true, even), ("Whole-wheat pasta", 160, true, even), ("Tomato sauce", 300, true, even), ("Egg", 60, false, even));
        AddRecipe("Veggie Omelette", 2, "Whisk eggs, cook with peppers; whole eggs split evenly.",
            ("Egg", 240, false, even), ("Bell pepper", 150, true, even), ("Olive oil", 10, false, ratio));
        AddRecipe("Banana Protein Pancakes", 2, "Blend banana, egg and oats; fry small pancakes.",
            ("Banana", 200, true, even), ("Egg", 120, false, even), ("Rolled oats", 100, true, even), ("Whey protein", 40, true, even));
    }

    private MealPlan BuildWeekPlan(DateOnly weekStart)
    {
        var plan = new MealPlan { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), HouseholdId = HouseholdId, WeekStartDate = weekStart };

        // (slot, recipe) template per weekday — snack days vary a bit so the UI has texture.
        (SlotType Slot, string Recipe)[][] week =
        [
            [(SlotType.Breakfast, "Protein Oats with Berries"), (SlotType.Lunch, "Beef Stir-Fry with Rice"), (SlotType.Dinner, "Grilled Chicken & Sweet Potato")],
            [(SlotType.Breakfast, "Veggie Omelette"), (SlotType.Lunch, "Grilled Chicken & Sweet Potato"), (SlotType.Dinner, "Salmon with Quinoa & Broccoli"), (SlotType.Snack, "Greek Yogurt Parfait")],
            [(SlotType.Breakfast, "Banana Protein Pancakes"), (SlotType.Lunch, "Turkey Meatballs & Pasta"), (SlotType.Dinner, "Beef Stir-Fry with Rice")],
            [(SlotType.Breakfast, "Protein Oats with Berries"), (SlotType.Lunch, "Salmon with Quinoa & Broccoli"), (SlotType.Dinner, "Turkey Meatballs & Pasta"), (SlotType.Snack, "Greek Yogurt Parfait")],
            [(SlotType.Breakfast, "Veggie Omelette"), (SlotType.Lunch, "Grilled Chicken & Sweet Potato"), (SlotType.Dinner, "Beef Stir-Fry with Rice")],
            [(SlotType.Breakfast, "Banana Protein Pancakes"), (SlotType.Lunch, "Turkey Meatballs & Pasta"), (SlotType.Dinner, "Salmon with Quinoa & Broccoli"), (SlotType.Snack, "Greek Yogurt Parfait")],
            [(SlotType.Breakfast, "Protein Oats with Berries"), (SlotType.Lunch, "Beef Stir-Fry with Rice"), (SlotType.Dinner, "Grilled Chicken & Sweet Potato")],
        ];

        var eaters = Profiles.Where(p => p.IsActive)
            .Select(p => (p.UserId, p.CalorieTarget))
            .ToList();

        for (var d = 0; d < 7; d++)
        {
            var date = weekStart.AddDays(d);
            foreach (var (slot, recipeName) in week[d])
                plan.Meals.Add(Solving.SolvePlannedMeal(plan.Id, date, slot, Rec(recipeName), IngredientsById, eaters));
        }

        return plan;
    }
}
