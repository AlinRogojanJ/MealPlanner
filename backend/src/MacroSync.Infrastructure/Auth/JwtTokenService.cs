using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MacroSync.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "MacroSync";
    public string Audience { get; set; } = "MacroSync";
    /// <summary>Dev-only default — production key lives in Azure Key Vault.</summary>
    public string SigningKey { get; set; } = "dev-only-signing-key-change-me-0123456789abcdef";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class JwtTokenService(JwtOptions options)
{
    public JwtOptions Options => options;

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(Guid userId, string email, string displayName)
    {
        var expires = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, displayName),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    /// <summary>Opaque random refresh token; only its SHA-256 hash is stored (§5.3).</summary>
    public static string NewRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
