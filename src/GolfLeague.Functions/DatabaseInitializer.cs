using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Infrastructure.Auth;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions;

public static class DatabaseInitializer
{
    public static async Task EnsureDatabaseInitializedAsync(IServiceProvider rootServices, ILogger logger)
    {
        using var scope = rootServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("DbInit: applying EF Core migrations.");

        // EnableRetryOnFailure handles transient faults per-command, but MigrateAsync
        // opens its own connection before the strategy fires. Wrap the whole call so
        // a 42119 (server busy / serverless resume) at connection-open time is retried.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync();
        });

        logger.LogInformation("DbInit: migrations applied. Seeding roles + default league + active season if missing.");
        await SeedRolesAsync(scope.ServiceProvider, logger);
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var defaultLeague = await SeedDefaultLeagueAsync(dbContext, config);
        await SeedActiveSeasonAsync(dbContext, defaultLeague.Id);
        await BootstrapAdminAsync(scope.ServiceProvider, logger);
        await BootstrapSuperAdminAsync(scope.ServiceProvider, logger);

        if (config.GetValue<bool>("SEED_LOCAL_DEV_DATA"))
        {
            var connectionString = config["SQL_CONNECTION_STRING"] ?? string.Empty;
            var isLocalDatabase = connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

            if (isLocalDatabase)
            {
                await SeedLocalDevelopmentDataAsync(scope.ServiceProvider, dbContext, defaultLeague.Id, logger);
            }
            else
            {
                logger.LogWarning("DbInit: SEED_LOCAL_DEV_DATA was configured, but connection string does not target a local database. Skipping local data seed for safety.");
            }
        }

        logger.LogInformation("DbInit: seed complete.");
    }

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var name in new[] { "admin", "scorer", "player" })
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid> { Name = name });
                if (!result.Succeeded)
                {
                    logger.LogError(
                        "DbInit: failed to seed role {Role}: {Errors}",
                        name, string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private static async Task<League> SeedDefaultLeagueAsync(AppDbContext dbContext, IConfiguration config)
    {
        var defaultSlug = config["DEFAULT_LEAGUE_SLUG"] ?? "capital";
        var existing = await dbContext.Leagues.FirstOrDefaultAsync(l => l.Slug == defaultSlug);
        if (existing is not null) return existing;

        var league = new League
        {
            Name = config["DEFAULT_LEAGUE_NAME"] ?? "Capital Golf League",
            Slug = defaultSlug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.Leagues.Add(league);
        await dbContext.SaveChangesAsync();
        return league;
    }

    private static async Task SeedActiveSeasonAsync(AppDbContext dbContext, int leagueId)
    {
        var hasActiveSeason = await dbContext.Seasons.AnyAsync(s => s.IsActive && s.LeagueId == leagueId);
        if (hasActiveSeason) return;

        var year = DateTime.UtcNow.Year;
        var start = new DateOnly(year, 5, 1);
        var end = new DateOnly(year, 9, 30);
        var midpoint = start.AddDays((end.DayNumber - start.DayNumber) / 2);

        var season = new Season
        {
            LeagueId = leagueId,
            Name = $"{year} Season",
            Year = year,
            StartDate = start,
            EndDate = end,
            IsActive = true,
        };
        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync();

        dbContext.SeasonHalves.AddRange(
            new SeasonHalf
            {
                SeasonId = season.Id,
                HalfNumber = 1,
                Name = $"{season.Name} - First Half",
                StartDate = start,
                EndDate = midpoint,
                CreatedAt = DateTime.UtcNow,
            },
            new SeasonHalf
            {
                SeasonId = season.Id,
                HalfNumber = 2,
                Name = $"{season.Name} - Second Half",
                StartDate = midpoint.AddDays(1),
                EndDate = end,
                CreatedAt = DateTime.UtcNow,
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task BootstrapAdminAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();

        var bootstrapEmail = config["ADMIN_BOOTSTRAP_EMAIL"];
        if (string.IsNullOrWhiteSpace(bootstrapEmail))
        {
            logger.LogInformation("DbInit: ADMIN_BOOTSTRAP_EMAIL not set; skipping admin bootstrap.");
            return;
        }

        if (bootstrapEmail.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                "DbInit: ADMIN_BOOTSTRAP_EMAIL resolved to a raw Key Vault reference string — " +
                "the secret is missing or the managed identity lacks access. " +
                "Create the 'AdminBootstrapEmail' secret in Key Vault and restart the Function App.");
            return;
        }

        // Check whether anyone already holds the admin role.
        var anyAdmin = await dbContext.UserRoles
            .AnyAsync(ur => dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == "admin"));
        if (anyAdmin)
        {
            logger.LogInformation("DbInit: admin user already exists; skipping bootstrap.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(bootstrapEmail);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, "admin"))
            {
                var addResult = await userManager.AddToRoleAsync(existing, "admin");
                if (!addResult.Succeeded)
                {
                    logger.LogError(
                        "DbInit: failed to grant admin role to existing user {Email}: {Errors}",
                        bootstrapEmail, string.Join("; ", addResult.Errors.Select(e => e.Description)));
                    return;
                }
            }
            logger.LogWarning(
                "DbInit: granted admin role to existing user {Email}. They must enroll MFA on next sign-in.",
                bootstrapEmail);
            return;
        }

        var user = new AppUser
        {
            UserName = bootstrapEmail,
            Email = bootstrapEmail,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
        };

        // Created with no password — admin must use the "forgot password"
        // flow on first login to set one. This avoids ever holding a
        // bootstrap secret in plaintext config.
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("DbInit: failed to create bootstrap admin: {Errors}", errors);
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, "admin");
        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "DbInit: created bootstrap admin {Email} but failed to assign admin role: {Errors}",
                bootstrapEmail, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogWarning(
            "DbInit: created bootstrap admin {Email} (no password set). " +
            "Use the password-reset flow to complete account setup.",
            bootstrapEmail);
    }

    private static async Task BootstrapSuperAdminAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        const string superAdminEmail = "josh.b.blaine@gmail.com";

        var user = await userManager.FindByEmailAsync(superAdminEmail);
        if (user is null)
        {
            logger.LogInformation("DbInit: super-admin account {Email} not found; skipping.", superAdminEmail);
            return;
        }

        if (user.IsSuperAdmin)
        {
            logger.LogInformation("DbInit: {Email} is already super-admin.", superAdminEmail);
            return;
        }

        user.IsSuperAdmin = true;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
            logger.LogWarning("DbInit: granted IsSuperAdmin to {Email}.", superAdminEmail);
        else
            logger.LogError(
                "DbInit: failed to set IsSuperAdmin on {Email}: {Errors}",
                superAdminEmail, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task SeedLocalDevelopmentDataAsync(
        IServiceProvider services,
        AppDbContext dbContext,
        int defaultLeagueId,
        ILogger logger)
    {
        logger.LogInformation("DbInit: seeding local development test data.");
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        var testAccounts = new[]
        {
            (Email: "admin@golfleague.local", RoleName: "admin", LeagueRole: PlayerRole.Admin, First: "Alex", Last: "Admin"),
            (Email: "scorer@golfleague.local", RoleName: "scorer", LeagueRole: PlayerRole.Scorer, First: "Sam", Last: "Scorer"),
            (Email: "player@golfleague.local", RoleName: "player", LeagueRole: PlayerRole.Player, First: "Pat", Last: "Player"),
        };

        const string defaultPassword = "Password123!";
        var userMap = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in testAccounts)
        {
            var user = await userManager.FindByEmailAsync(account.Email);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = account.Email,
                    Email = account.Email,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                };
                var createResult = await userManager.CreateAsync(user, defaultPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("DbInit: failed creating test user {Email}: {Errors}",
                        account.Email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    continue;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    await userManager.ResetPasswordAsync(user, token, defaultPassword);
                }
            }

            if (!await userManager.IsInRoleAsync(user, account.RoleName))
            {
                await userManager.AddToRoleAsync(user, account.RoleName);
            }

            var hasMembership = await dbContext.LeagueMemberships
                .AnyAsync(m => m.LeagueId == defaultLeagueId && m.UserId == user.Id);
            if (!hasMembership)
            {
                dbContext.LeagueMemberships.Add(new LeagueMembership
                {
                    LeagueId = defaultLeagueId,
                    UserId = user.Id,
                    Role = account.LeagueRole,
                    JoinedAt = DateTime.UtcNow,
                });
                await dbContext.SaveChangesAsync();
            }

            userMap[account.Email] = user;
        }

        // Check if roster players are already seeded
        var alreadySeeded = await dbContext.Players.AnyAsync(p => p.LeagueId == defaultLeagueId && p.Email == "player@golfleague.local");
        if (alreadySeeded)
        {
            logger.LogInformation("DbInit: test players and rounds already seeded.");
            return;
        }

        // Seed Players
        var playersToCreate = new List<(string First, string Last, string? Email, double Handicap, Guid? UserId)>
        {
            ("Alex", "Admin", "admin@golfleague.local", 6.4, userMap.GetValueOrDefault("admin@golfleague.local")?.Id),
            ("Sam", "Scorer", "scorer@golfleague.local", 11.2, userMap.GetValueOrDefault("scorer@golfleague.local")?.Id),
            ("Pat", "Player", "player@golfleague.local", 14.8, userMap.GetValueOrDefault("player@golfleague.local")?.Id),
            ("Jordan", "Spieth", null, 2.1, null),
            ("Rory", "McIlroy", null, 1.5, null),
            ("Scottie", "Scheffler", null, 0.4, null),
            ("Tiger", "Woods", null, 4.0, null),
            ("Viktor", "Hovland", null, 3.2, null),
        };

        var playerEntities = new List<Player>();
        foreach (var p in playersToCreate)
        {
            var player = new Player
            {
                LeagueId = defaultLeagueId,
                FirstName = p.First,
                LastName = p.Last,
                Email = p.Email,
                AppUserId = p.UserId,
                IsActive = true,
            };
            dbContext.Players.Add(player);
            playerEntities.Add(player);
        }
        await dbContext.SaveChangesAsync();

        // Seed Handicaps
        for (int i = 0; i < playerEntities.Count; i++)
        {
            dbContext.Handicaps.Add(new Handicap
            {
                PlayerId = playerEntities[i].Id,
                HandicapIndex = playersToCreate[i].Handicap,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                Source = HandicapSource.Manual,
                Notes = "Initial local dev seed handicap",
            });
        }
        await dbContext.SaveChangesAsync();

        // Seed Flights & Memberships under Active Season / Half 1
        var season = await dbContext.Seasons
            .Include(s => s.Halves)
            .FirstOrDefaultAsync(s => s.LeagueId == defaultLeagueId && s.IsActive);

        if (season is not null && season.Halves.Count > 0)
        {
            var half1 = season.Halves.OrderBy(h => h.HalfNumber).First();
            var flightA = new Flight
            {
                LeagueId = defaultLeagueId,
                SeasonId = season.Id,
                HalfId = half1.Id,
                Name = "Championship Flight",
                DisplayOrder = 1,
            };
            var flightB = new Flight
            {
                LeagueId = defaultLeagueId,
                SeasonId = season.Id,
                HalfId = half1.Id,
                Name = "First Flight",
                DisplayOrder = 2,
            };
            dbContext.Flights.AddRange(flightA, flightB);
            await dbContext.SaveChangesAsync();

            // Assign first 4 players to Flight A, rest to Flight B
            for (int i = 0; i < playerEntities.Count; i++)
            {
                var targetFlight = i < 4 ? flightA : flightB;
                dbContext.FlightMemberships.Add(new FlightMembership
                {
                    PlayerId = playerEntities[i].Id,
                    FlightId = targetFlight.Id,
                    SeasonId = season.Id,
                    HalfId = half1.Id,
                    JoinedAt = DateTime.UtcNow,
                });
            }
            await dbContext.SaveChangesAsync();

            // Seed Course Rounds
            var course = await dbContext.Courses.FirstOrDefaultAsync();
            if (course is not null)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // Past Round (Week 1) - Finalized
                var pastRound = new Round
                {
                    LeagueId = defaultLeagueId,
                    SeasonId = season.Id,
                    HalfId = half1.Id,
                    CourseId = course.Id,
                    WeekNumber = 1,
                    RoundDate = today.AddDays(-7),
                    Status = RoundStatus.Finalized,
                    NineHoleSide = NineHoleSide.Front,
                    RoundType = RoundType.NineHole,
                    Notes = "Week 1 - Season Opener (Local Dev Seed)",
                };
                dbContext.Rounds.Add(pastRound);
                await dbContext.SaveChangesAsync();

                foreach (var player in playerEntities.Take(6))
                {
                    dbContext.RoundParticipants.Add(new RoundParticipant
                    {
                        RoundId = pastRound.Id,
                        PlayerId = player.Id,
                        HandicapIndex = 10.0,
                        CourseHandicap = 5,
                        TotalGrossStrokes = 42,
                        TotalNetStrokes = 37,
                        TotalGrossStablefordPoints = 14,
                        TotalNetStablefordPoints = 19,
                    });
                }

                // Upcoming Round (Week 2) - Scheduled with Tee Times
                var upcomingRound = new Round
                {
                    LeagueId = defaultLeagueId,
                    SeasonId = season.Id,
                    HalfId = half1.Id,
                    CourseId = course.Id,
                    WeekNumber = 2,
                    RoundDate = today.AddDays(2),
                    Status = RoundStatus.Scheduled,
                    NineHoleSide = NineHoleSide.Back,
                    RoundType = RoundType.NineHole,
                    Notes = "Week 2 - Upcoming Match (Local Dev Seed)",
                };
                dbContext.Rounds.Add(upcomingRound);
                await dbContext.SaveChangesAsync();

                var teeTime1 = new RoundTeeTime
                {
                    RoundId = upcomingRound.Id,
                    TeeTimeNumber = 1,
                    ScheduledTime = new TimeOnly(15, 28),
                };
                var teeTime2 = new RoundTeeTime
                {
                    RoundId = upcomingRound.Id,
                    TeeTimeNumber = 2,
                    ScheduledTime = new TimeOnly(15, 36),
                };
                dbContext.RoundTeeTimes.AddRange(teeTime1, teeTime2);
                await dbContext.SaveChangesAsync();

                // Put first 2 players in tee time 1, leave others unassigned
                for (int i = 0; i < playerEntities.Count; i++)
                {
                    dbContext.RoundParticipants.Add(new RoundParticipant
                    {
                        RoundId = upcomingRound.Id,
                        PlayerId = playerEntities[i].Id,
                        HandicapIndex = 10.0,
                        CourseHandicap = 5,
                        TeeTimeId = i < 2 ? teeTime1.Id : null,
                    });
                }
                await dbContext.SaveChangesAsync();
            }
        }

        logger.LogInformation(
            "DbInit: Local development test data seeded successfully. " +
            "Test credentials: admin@golfleague.local / {Password}, scorer@golfleague.local / {Password}, player@golfleague.local / {Password}",
            defaultPassword, defaultPassword, defaultPassword);
    }
}
