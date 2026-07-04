using MacroSync.Domain;

namespace MacroSync.UnitTests;

public class PortionSolverTests
{
    private static RecipeSnapshot ChickenAndSweetPotato()
    {
        var chicken = Guid.NewGuid();
        var potato = Guid.NewGuid();
        return new RecipeSnapshot(Guid.NewGuid(), "Grilled Chicken & Sweet Potato",
        [
            new SolverIngredient(chicken, "Chicken breast", 400, true, IndivisibleSplitRule.Even, 165, 31, 0, 3.6m, RoundingStepG: 25),
            new SolverIngredient(potato, "Sweet potato", 500, true, IndivisibleSplitRule.Even, 86, 1.6m, 20, 0.1m, RoundingStepG: 10),
        ]);
    }

    [Fact]
    public void BiggerRemainingKcal_GetsBiggerPortion()
    {
        var alin = Guid.NewGuid();
        var maria = Guid.NewGuid();

        var result = PortionSolver.Solve(ChickenAndSweetPotato(),
            [new EaterTarget(alin, 850), new EaterTarget(maria, 578)]);

        var alinPortion = result.Portions.Single(p => p.UserId == alin);
        var mariaPortion = result.Portions.Single(p => p.UserId == maria);
        Assert.True(alinPortion.Kcal > mariaPortion.Kcal);
    }

    [Fact]
    public void Portions_AreRoundedToCookableIncrements()
    {
        var result = PortionSolver.Solve(ChickenAndSweetPotato(),
            [new EaterTarget(Guid.NewGuid(), 850), new EaterTarget(Guid.NewGuid(), 578)]);

        foreach (var portion in result.Portions)
            foreach (var grams in portion.IngredientGrams.Values)
                Assert.Equal(0, grams % 5); // every step used is a multiple of 5
    }

    [Fact]
    public void IndivisibleEven_SplitsEquallyRegardlessOfTargets()
    {
        var egg = Guid.NewGuid();
        var recipe = new RecipeSnapshot(Guid.NewGuid(), "Omelette",
            [new SolverIngredient(egg, "Egg", 240, false, IndivisibleSplitRule.Even, 143, 12.6m, 0.7m, 9.5m, RoundingStepG: 60)]);

        var result = PortionSolver.Solve(recipe,
            [new EaterTarget(Guid.NewGuid(), 900), new EaterTarget(Guid.NewGuid(), 300)]);

        Assert.All(result.Portions, p => Assert.Equal(120, p.IngredientGrams[egg]));
    }
}
