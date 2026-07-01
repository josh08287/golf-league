using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using GolfLeague.Domain.Entities;
using GolfLeague.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

public class JwtTokenServiceTests
{
    private const string ValidSigningKey = "this-is-a-32-character-or-longer-signing-key-for-tests";

    private static JwtTokenService MakeService(string? signingKey = ValidSigningKey)
    {
        var configValues = new Dictionary<string, string?> { ["JWT_SIGNING_KEY"] = signingKey };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        return new JwtTokenService(config);
    }

    private static AppUser MakeUser(Guid? id = null, string email = "player@example.com") => new()
    {
        Id = id ?? Guid.NewGuid(),
        Email = email,
        UserName = email,
    };

    [Fact]
    public void Constructor_MissingSigningKey_Throws()
    {
        var config = new ConfigurationBuilder().Build();
        var act = () => new JwtTokenService(config);
        act.Should().Throw<InvalidOperationException>().WithMessage("*JWT_SIGNING_KEY*not configured*");
    }

    [Fact]
    public void Constructor_SigningKeyTooShort_Throws()
    {
        var act = () => MakeService(signingKey: "too-short");
        act.Should().Throw<InvalidOperationException>().WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void IssueAccessToken_IncludesSubjectEmailAndRoleClaims()
    {
        var service = MakeService();
        var user = MakeUser(email: "alice@example.com");

        var result = service.IssueAccessToken(user, new[] { "Player", "Scorer" }, leagueId: null, playerId: null, isSuperAdmin: false);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "alice@example.com");
        // Roles are lower-cased regardless of input casing.
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().BeEquivalentTo("player", "scorer");
    }

    [Fact]
    public void IssueAccessToken_WithLeagueAndPlayerId_IncludesBothClaims()
    {
        var service = MakeService();
        var user = MakeUser();

        var result = service.IssueAccessToken(user, Array.Empty<string>(), leagueId: 7, playerId: 42, isSuperAdmin: false);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Should().Contain(c => c.Type == "leagueId" && c.Value == "7");
        jwt.Claims.Should().Contain(c => c.Type == "playerId" && c.Value == "42");
    }

    [Fact]
    public void IssueAccessToken_WithoutLeagueOrPlayerId_OmitsBothClaims()
    {
        var service = MakeService();
        var user = MakeUser();

        var result = service.IssueAccessToken(user, Array.Empty<string>(), leagueId: null, playerId: null, isSuperAdmin: false);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Should().NotContain(c => c.Type == "leagueId");
        jwt.Claims.Should().NotContain(c => c.Type == "playerId");
    }

    [Fact]
    public void IssueAccessToken_SuperAdmin_IncludesSuperAdminClaim()
    {
        var service = MakeService();
        var user = MakeUser();

        var result = service.IssueAccessToken(user, Array.Empty<string>(), leagueId: null, playerId: null, isSuperAdmin: true);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Should().Contain(c => c.Type == "superAdmin" && c.Value == "true");
    }

    [Fact]
    public void IssueAccessToken_NotSuperAdmin_OmitsSuperAdminClaim()
    {
        var service = MakeService();
        var user = MakeUser();

        var result = service.IssueAccessToken(user, Array.Empty<string>(), leagueId: null, playerId: null, isSuperAdmin: false);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Should().NotContain(c => c.Type == "superAdmin");
    }

    [Fact]
    public void IssueMfaChallengeToken_HasMfaPendingRoleAndShortLifetime()
    {
        var service = MakeService();
        var user = MakeUser();
        var before = DateTime.UtcNow;

        var result = service.IssueMfaChallengeToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == JwtTokenService.MfaPendingRole);
        // 5-minute challenge lifetime — generous tolerance for test execution time.
        result.ExpiresAt.Should().BeCloseTo(before.AddMinutes(5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ValidateMfaChallengeToken_ValidChallengeToken_ReturnsPrincipal()
    {
        var service = MakeService();
        var user = MakeUser();
        var challenge = service.IssueMfaChallengeToken(user);

        var principal = service.ValidateMfaChallengeToken(challenge.Token);

        principal.Should().NotBeNull();
        principal!.IsInRole(JwtTokenService.MfaPendingRole).Should().BeTrue();
    }

    [Fact]
    public void ValidateMfaChallengeToken_RegularAccessToken_ReturnsNull()
    {
        // A normal access token (no mfa-pending role) must not be accepted
        // as an MFA challenge — that would let a fully-authenticated session
        // replay as a pending-MFA challenge.
        var service = MakeService();
        var user = MakeUser();
        var access = service.IssueAccessToken(user, new[] { "player" }, leagueId: null, playerId: null, isSuperAdmin: false);

        var principal = service.ValidateMfaChallengeToken(access.Token);

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateMfaChallengeToken_GarbageToken_ReturnsNull()
    {
        var service = MakeService();

        var principal = service.ValidateMfaChallengeToken("not-a-real-jwt");

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateMfaChallengeToken_TokenSignedWithDifferentKey_ReturnsNull()
    {
        var serviceA = MakeService(signingKey: "key-A-this-is-a-32-character-key-aaaa");
        var serviceB = MakeService(signingKey: "key-B-this-is-a-different-32char-key");
        var user = MakeUser();
        var challenge = serviceA.IssueMfaChallengeToken(user);

        var principal = serviceB.ValidateMfaChallengeToken(challenge.Token);

        principal.Should().BeNull();
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUrlSafeUniqueTokens()
    {
        var service = MakeService();

        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();

        token1.Should().NotBe(token2);
        token1.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    public void HashRefreshToken_IsDeterministicAndDistinguishesDifferentInputs()
    {
        var service = MakeService();

        var hash1a = service.HashRefreshToken("token-one");
        var hash1b = service.HashRefreshToken("token-one");
        var hash2 = service.HashRefreshToken("token-two");

        hash1a.Should().Be(hash1b);
        hash1a.Should().NotBe(hash2);
    }
}
