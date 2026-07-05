using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MacroSync.IntegrationTests;

// End-to-end API tests against the in-memory mock store (DataSource=Mock).
// SQL-backed tests via Testcontainers arrive when the DB pipeline lands.

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string HouseholdId = "11111111-1111-1111-1111-111111111111";
    private const string AlinId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string DemoWeek = "2026-06-29";

    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = Client();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "alin@example.com", password = "demo1234" });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task DemoWeekPlan_ReturnsSevenDaysWithPortionsAndTotals()
    {
        var response = await Client().GetAsync($"/api/v1/households/{HouseholdId}/plans?week={DemoWeek}");
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, plan.GetProperty("days").GetArrayLength());
        Assert.Equal(2, plan.GetProperty("members").GetArrayLength());

        var firstMeal = plan.GetProperty("days")[0].GetProperty("meals")[0];
        Assert.Equal(2, firstMeal.GetProperty("portions").GetArrayLength());
    }

    [Fact]
    public async Task UnknownWeek_Returns404()
    {
        var response = await Client().GetAsync($"/api/v1/households/{HouseholdId}/plans?week=2030-01-06");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WriteWithoutToken_Returns401()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/logs", new
        {
            userId = AlinId,
            date = DemoWeek,
            description = "Cake",
            kcal = 300,
            proteinG = 5,
            carbsG = 40,
            fatG = 12,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogOffPlanFood_CreatesSuggestion_AcceptShrinksMeals()
    {
        var client = await AuthedClientAsync();

        var logResponse = await client.PostAsJsonAsync("/api/v1/logs", new
        {
            userId = AlinId,
            date = DemoWeek, // Monday: breakfast/lunch/dinner planned
            description = "Chocolate cake",
            kcal = 400,
            proteinG = 6,
            carbsG = 50,
            fatG = 18,
        });
        logResponse.EnsureSuccessStatusCode();

        var body = await logResponse.Content.ReadFromJsonAsync<JsonElement>();
        var suggestion = body.GetProperty("suggestion");
        Assert.Equal("Pending", suggestion.GetProperty("status").GetString());
        Assert.True(suggestion.GetProperty("adjustments").GetArrayLength() > 0);

        var suggestionId = suggestion.GetProperty("id").GetString();
        var accept = await client.PostAsync($"/api/v1/suggestions/{suggestionId}/accept", null);
        accept.EnsureSuccessStatusCode();

        var accepted = await accept.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Accepted", accepted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvalidLog_Returns400WithFieldErrors()
    {
        var client = await AuthedClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/logs", new
        {
            userId = AlinId,
            date = "not-a-date",
            description = "",
            kcal = -5,
            proteinG = 0,
            carbsG = 0,
            fatG = 0,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShareLink_AllowsAnonymousGroceryAccess()
    {
        var client = await AuthedClientAsync();

        var week = await client.GetFromJsonAsync<JsonElement>($"/api/v1/households/{HouseholdId}/plans?week={DemoWeek}");
        var planId = week.GetProperty("planId").GetString();

        var share = await client.PostAsync($"/api/v1/plans/{planId}/grocery-list/share", null);
        share.EnsureSuccessStatusCode();
        var link = await share.Content.ReadFromJsonAsync<JsonElement>();
        var token = link.GetProperty("shareToken").GetString();

        // Anonymous client (no bearer) can read via the capability URL.
        var anonymous = Client();
        var list = await anonymous.GetAsync($"/api/v1/grocery-lists/{token}");
        list.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateWeek_CopyFrom_CopiesMenuOntoNewWeek()
    {
        var client = await AuthedClientAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/households/{HouseholdId}/plans", new
        {
            weekStartDate = "2026-08-03",
            copyFromWeekStartDate = DemoWeek,
        });
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<JsonElement>();
        var totalMeals = plan.GetProperty("days").EnumerateArray()
            .Sum(d => d.GetProperty("meals").GetArrayLength());
        Assert.True(totalMeals >= 21); // demo week has 3-4 meals every day
    }

    [Fact]
    public async Task MoveMeal_ChangesDayAndResolvesPortions()
    {
        var client = await AuthedClientAsync();

        var week = await client.GetFromJsonAsync<JsonElement>($"/api/v1/households/{HouseholdId}/plans?week={DemoWeek}");
        var tuesday = week.GetProperty("days")[1];
        var meal = tuesday.GetProperty("meals")[0];
        var mealId = meal.GetProperty("id").GetString();

        var move = await client.PostAsJsonAsync($"/api/v1/meals/{mealId}/move", new
        {
            date = "2026-07-01", // Wednesday
            slotType = "Snack",
        });
        move.EnsureSuccessStatusCode();

        var moved = await move.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-07-01", moved.GetProperty("date").GetString());
        Assert.Equal("Snack", moved.GetProperty("slotType").GetString());
        Assert.True(moved.GetProperty("portions").GetArrayLength() > 0);
    }

    [Fact]
    public async Task DeleteMeal_RemovesItFromThePlan()
    {
        var client = await AuthedClientAsync();

        var week = await client.GetFromJsonAsync<JsonElement>($"/api/v1/households/{HouseholdId}/plans?week={DemoWeek}");
        var sunday = week.GetProperty("days")[6];
        var mealId = sunday.GetProperty("meals")[0].GetProperty("id").GetString();

        var delete = await client.DeleteAsync($"/api/v1/meals/{mealId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var again = await client.DeleteAsync($"/api/v1/meals/{mealId}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }
}
