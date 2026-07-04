using MacroSync.Application;
using MacroSync.Infrastructure.Auth;
using MacroSync.Infrastructure.Mocks;
using MacroSync.Infrastructure.Persistence;
using MacroSync.Infrastructure.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSync.Infrastructure;

public static class InfrastructureModule
{
    /// <summary>
    /// DataSource=Mock (default) → in-memory demo data, no database needed.
    /// DataSource=Sql → EF Core against ConnectionStrings:MacroSync.
    /// </summary>
    public static IServiceCollection AddMacroSyncInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var jwtOptions = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton(jwtOptions);
        services.AddSingleton<JwtTokenService>();

        var useSql = string.Equals(config["DataSource"], "Sql", StringComparison.OrdinalIgnoreCase);
        if (useSql)
        {
            services.AddDbContext<MacroSyncDbContext>(o =>
                o.UseSqlServer(config.GetConnectionString("MacroSync")
                    ?? throw new InvalidOperationException("ConnectionStrings:MacroSync is not configured.")));

            services.AddScoped<IHouseholdService, SqlHouseholdService>();
            services.AddScoped<IMealPlanService, SqlMealPlanService>();
            services.AddScoped<IRecipeService, SqlRecipeService>();
            services.AddScoped<IProfileService, SqlProfileService>();
            services.AddScoped<IFoodLogService, SqlFoodLogService>();
            services.AddScoped<ISuggestionService, SqlSuggestionService>();
            services.AddScoped<IAuthService>(sp => new AuthService(
                sp.GetRequiredService<MacroSyncDbContext>(),
                sp.GetRequiredService<JwtTokenService>(),
                config["Google:ClientId"]));
        }
        else
        {
            services.AddSingleton<MockDb>();
            services.AddSingleton<IHouseholdService, MockHouseholdService>();
            services.AddSingleton<IMealPlanService, MockMealPlanService>();
            services.AddSingleton<IRecipeService, MockRecipeService>();
            services.AddSingleton<IProfileService, MockProfileService>();
            services.AddSingleton<IFoodLogService, MockFoodLogService>();
            services.AddSingleton<ISuggestionService, MockSuggestionService>();
            services.AddSingleton<IAuthService, MockAuthService>();
        }

        return services;
    }
}
