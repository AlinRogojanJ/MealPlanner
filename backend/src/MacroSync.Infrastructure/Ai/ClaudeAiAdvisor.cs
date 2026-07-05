using System.Text.Json;
using Anthropic;
using MacroSync.Application;
using Microsoft.Extensions.Logging;

namespace MacroSync.Infrastructure.Ai;

public class AiOptions
{
    public const string SectionName = "Ai";
    /// <summary>Falls back to the ANTHROPIC_API_KEY environment variable when empty.</summary>
    public string? AnthropicApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-4-8";
}

/// <summary>
/// Claude-backed advisor (Phase 2). Proposes candidate recalc scales and
/// meal recommendations; every output is validated and re-priced by the
/// domain rules before reaching the user. Returns null on any failure so
/// callers fall back to the rules engine — the app never depends on the AI.
/// </summary>
public class ClaudeAiAdvisor : IAiAdvisor
{
    private readonly AnthropicClient? _client;
    private readonly AiOptions _options;
    private readonly ILogger<ClaudeAiAdvisor> _logger;

    public ClaudeAiAdvisor(AiOptions options, ILogger<ClaudeAiAdvisor> logger)
    {
        _options = options;
        _logger = logger;
        var apiKey = string.IsNullOrWhiteSpace(options.AnthropicApiKey)
            ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : options.AnthropicApiKey;
        _client = string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicClient { ApiKey = apiKey };
    }

    public bool IsEnabled => _client is not null;

    public async Task<IReadOnlyList<AiRecalcCandidate>?> ProposeRecalcAsync(AiRecalcRequest request, CancellationToken ct = default)
    {
        if (_client is null || request.RemainingMeals.Count == 0) return null;

        var meals = string.Join("\n", request.RemainingMeals.Select(m =>
            $"- id: {m.PlannedMealId} | {m.SlotType}: {m.RecipeName} ({m.PlannedKcal:0} kcal planned)"));

        var prompt = $$"""
            You are a nutrition assistant inside a meal-planning app. A person on a {{request.DietType}} diet
            just ate off-plan: "{{request.OffPlanDescription}}" (+{{request.OverageKcal:0}} kcal over target).

            Their remaining meals today:
            {{meals}}

            Propose how much to shrink each remaining meal to absorb the overage. Preferences:
            - protein-heavy meals should shrink less (protect protein on a {{request.DietType}} diet)
            - never scale a meal below 0.6 of its planned size
            - snacks and desserts should absorb more than main meals
            - the total absorbed kcal should come as close to the overage as possible without unhealthy cuts

            Respond with ONLY a JSON array, no prose, of {"plannedMealId": "<id>", "scale": <0.6-1.0>} objects,
            one per remaining meal (scale 1.0 = unchanged).
            """;

        var text = await AskAsync(prompt, ct);
        if (text is null) return null;

        try
        {
            var candidates = JsonSerializer.Deserialize<List<AiRecalcCandidate>>(ExtractJson(text),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (candidates is null) return null;

            // Validate: known meal ids only, scales clamped to safe bounds. Rules stay the truth.
            var known = request.RemainingMeals.Select(m => m.PlannedMealId).ToHashSet();
            var valid = candidates
                .Where(c => known.Contains(c.PlannedMealId))
                .Select(c => c with { Scale = Math.Clamp(c.Scale, 0.6m, 1.0m) })
                .ToList();
            return valid.Count > 0 ? valid : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI recalc response was not valid JSON; falling back to rules");
            return null;
        }
    }

    public async Task<IReadOnlyList<AiRecommendation>?> RecommendMealsAsync(AiRecommendationRequest request, CancellationToken ct = default)
    {
        if (_client is null || request.Recipes.Count == 0) return null;

        var eaters = string.Join("\n", request.Eaters.Select(e =>
            $"- {e.DisplayName}: {e.DietType} diet, ~{e.RemainingKcal:0} kcal budget for this {request.SlotType.ToLowerInvariant()}"));
        var recipes = string.Join("\n", request.Recipes.Select(r =>
            $"- id: {r.RecipeId} | {r.Name} (~{r.KcalPerServing:0} kcal/serving; {r.MainIngredients})"));
        var recent = request.RecentRecipeNames.Count == 0
            ? "(nothing recently)"
            : string.Join(", ", request.RecentRecipeNames);

        var prompt = $$"""
            You are a nutrition assistant inside a shared meal-planning app. A household cooks ONE dish
            together and the app splits portions per person automatically.

            Eaters and their remaining budgets:
            {{eaters}}

            Recipe library:
            {{recipes}}

            Recently eaten (prefer variety over repeats): {{recent}}

            Recommend the 3 best dishes for this {{request.SlotType}} that suit BOTH people's goals and budgets.
            Respond with ONLY a JSON array, no prose, of {"recipeId": "<id>", "reason": "<one short sentence>"}
            objects, best first.
            """;

        var text = await AskAsync(prompt, ct);
        if (text is null) return null;

        try
        {
            var recommendations = JsonSerializer.Deserialize<List<AiRecommendation>>(ExtractJson(text),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (recommendations is null) return null;

            var known = request.Recipes.Select(r => r.RecipeId).ToHashSet();
            var valid = recommendations.Where(r => known.Contains(r.RecipeId)).Take(3).ToList();
            return valid.Count > 0 ? valid : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI recommendation response was not valid JSON; falling back to rules");
            return null;
        }
    }

    private async Task<string?> AskAsync(string prompt, CancellationToken ct)
    {
        try
        {
            var response = await _client!.Messages.Create(new()
            {
                Model = _options.Model,
                MaxTokens = 1024,
                Messages = [new() { Role = "user", Content = prompt }],
            }, cancellationToken: ct);

            var text = string.Concat(response.Content
                .Select(b => b.TryPickText(out var textBlock) ? textBlock.Text : ""));
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude request failed; falling back to rules");
            return null;
        }
    }

    /// <summary>Strips markdown fences if the model wrapped the JSON despite instructions.</summary>
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}

/// <summary>Disabled advisor — used when AI is off; callers always fall back to rules.</summary>
public class NullAiAdvisor : IAiAdvisor
{
    public bool IsEnabled => false;
    public Task<IReadOnlyList<AiRecalcCandidate>?> ProposeRecalcAsync(AiRecalcRequest request, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AiRecalcCandidate>?>(null);
    public Task<IReadOnlyList<AiRecommendation>?> RecommendMealsAsync(AiRecommendationRequest request, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AiRecommendation>?>(null);
}
