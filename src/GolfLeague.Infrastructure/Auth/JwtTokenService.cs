using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GolfLeague.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
    public const string Issuer = "golf-league-api";
    public const string Audience = "golf-league-api";
    public const string MfaPendingRole = "mfa-pending";

    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(2);
    private static readonly TimeSpan MfaChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");

        if (secret.Length < 32)
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 characters.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    }

    public AccessTokenResult IssueAccessToken(AppUser user, IEnumerable<string> roles, int? leagueId, int? playerId, bool isSuperAdmin)
        => Issue(user, roles.Select(r => r.ToLowerInvariant()), AccessTokenLifetime, leagueId, playerId, isSuperAdmin);

    public AccessTokenResult IssueMfaChallengeToken(AppUser user)
        => Issue(user, [MfaPendingRole], MfaChallengeLifetime);

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public string HashRefreshToken(string refreshToken)
    {
        // Refresh tokens are 64-byte URL-safe base64 strings with ~384 bits
        // of entropy, so SHA-256 is sufficient — we just need a constant-time
        // lookup key, not a key-stretching function.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    public ClaimsPrincipal? ValidateMfaChallengeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _validationParameters, out _);
            // Only accept tokens that explicitly carry the MFA-pending role.
            if (!principal.IsInRole(MfaPendingRole)) return null;
            return principal;
        }
        catch
        {
            return null;
        }
    }

    private AccessTokenResult Issue(AppUser user, IEnumerable<string> roles, TimeSpan lifetime,
        int? leagueId = null, int? playerId = null, bool isSuperAdmin = false)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim("role", role));

        if (playerId.HasValue)
            claims.Add(new Claim("playerId", playerId.Value.ToString()));

        if (leagueId.HasValue)
            claims.Add(new Claim("leagueId", leagueId.Value.ToString()));

        if (isSuperAdmin)
            claims.Add(new Claim("superAdmin", "true"));

        var jwt = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new AccessTokenResult(encoded, expires);
    }
}
