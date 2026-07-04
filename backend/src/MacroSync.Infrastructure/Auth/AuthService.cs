using Google.Apis.Auth;
using MacroSync.Application;
using MacroSync.Domain;
using MacroSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MacroSync.Infrastructure.Auth;

// Google Sign-In primary + email/password fallback (Tech Design §5).
// Google authenticates identity; authorization downstream always runs on our
// own JWT — one token pipeline regardless of sign-in method.

public class AuthService(MacroSyncDbContext db, JwtTokenService jwt, string? googleClientId) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = request.DisplayName.Trim() };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user?.PasswordHash is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return null;

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse?> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken ct = default)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Validates signature (JWKS, cached), issuer, audience, expiry.
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = googleClientId is null ? null : [googleClientId],
                });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        var email = payload.Email.ToLowerInvariant();

        // Find by external login first, then link by verified email (§5.1 account linking).
        var external = await db.ExternalLogins
            .FirstOrDefaultAsync(x => x.Provider == "Google" && x.ProviderKey == payload.Subject, ct);

        User? user;
        if (external is not null)
        {
            user = await db.Users.FirstAsync(u => u.Id == external.UserId, ct);
        }
        else
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = payload.Name ?? email };
                db.Users.Add(user);
            }
            db.ExternalLogins.Add(new ExternalLogin { UserId = user.Id, Provider = "Google", ProviderKey = payload.Subject });
            await db.SaveChangesAsync(ct);
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return null;

        if (!stored.IsActive)
        {
            // Reuse detection: a revoked token was replayed — kill the whole family (§5.3).
            await db.RefreshTokens
                .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), ct);
            return null;
        }

        stored.RevokedAtUtc = DateTime.UtcNow;
        var user = await db.Users.FirstAsync(u => u.Id == stored.UserId, ct);
        return await IssueTokensAsync(user, ct, familyId: stored.FamilyId);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct, Guid? familyId = null)
    {
        var (accessToken, expiresAt) = jwt.IssueAccessToken(user.Id, user.Email, user.DisplayName);
        var refreshToken = JwtTokenService.NewRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(jwt.Options.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.Id, user.DisplayName, user.Email);
    }
}
