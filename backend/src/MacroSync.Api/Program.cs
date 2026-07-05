using System.Text;
using FluentValidation;
using MacroSync.Application;
using MacroSync.Infrastructure;
using MacroSync.Infrastructure.Auth;
using MacroSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMacroSyncInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// App-issued JWT — one token pipeline regardless of sign-in method (§5.2).
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    });
builder.Services.AddAuthorization();

// Vite dev server origin — tightened per environment once deployed.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json — feeds frontend type codegen later

    // Local dev only: apply migrations + seed demo data when running against SQL.
    // Production applies migrations as a deploy-time step, never on startup (§3.3).
    if (string.Equals(app.Configuration["DataSource"], "Sql", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MacroSyncDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
