using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public sealed class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly ILeagueContext? _leagueContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ILeagueContext? leagueContext = null)
        : base(options)
    {
        _leagueContext = leagueContext;
    }

    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMembership> LeagueMemberships => Set<LeagueMembership>();
    public DbSet<LeagueSetting> LeagueSettings => Set<LeagueSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonHalf> SeasonHalves => Set<SeasonHalf>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<FlightMembership> FlightMemberships => Set<FlightMembership>();
    public DbSet<Handicap> Handicaps => Set<Handicap>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseHole> CourseHoles => Set<CourseHole>();
    public DbSet<TeeBox> TeeBoxes => Set<TeeBox>();
    public DbSet<HoleTeeBox> HoleTeeBoxes => Set<HoleTeeBox>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RoundParticipant> RoundParticipants => Set<RoundParticipant>();
    public DbSet<RoundTeeTime> RoundTeeTimes => Set<RoundTeeTime>();
    public DbSet<HoleScore> HoleScores => Set<HoleScore>();
    public DbSet<TournamentMatchup> TournamentMatchups => Set<TournamentMatchup>();
    public DbSet<TournamentHoleExtra> TournamentHoleExtras => Set<TournamentHoleExtra>();
    public DbSet<TournamentLongestDriveWinner> TournamentLongestDriveWinners => Set<TournamentLongestDriveWinner>();
    public DbSet<RoundClosestToPin> RoundClosestToPins => Set<RoundClosestToPin>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlayerInvite> PlayerInvites => Set<PlayerInvite>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public new DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureLeagues(modelBuilder);
        ConfigureLeagueMemberships(modelBuilder);
        ConfigureLeagueSettings(modelBuilder);
        ConfigureFeatureFlags(modelBuilder);
        ConfigureSeasons(modelBuilder);
        ConfigureSeasonHalves(modelBuilder);
        ConfigureFlights(modelBuilder);
        ConfigurePlayers(modelBuilder);
        ConfigureFlightMemberships(modelBuilder);
        ConfigureHandicaps(modelBuilder);
        ConfigureCourses(modelBuilder);
        ConfigureCourseHoles(modelBuilder);
        ConfigureTeeBoxes(modelBuilder);
        ConfigureHoleTeeBoxes(modelBuilder);
        ConfigureRounds(modelBuilder);
        ConfigureRoundParticipants(modelBuilder);
        ConfigureTournamentMatchups(modelBuilder);
        ConfigureTournamentHoleExtras(modelBuilder);
        ConfigureTournamentLongestDriveWinners(modelBuilder);
        ConfigureRoundClosestToPins(modelBuilder);
        ConfigureRoundTeeTimes(modelBuilder);
        ConfigureHoleScores(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigurePlayerInvites(modelBuilder);
        ConfigureAppUsers(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureUserPasskeys(modelBuilder);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    private static void ConfigureLeagues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<League>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();
        });
    }

    private static void ConfigureLeagueSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeagueSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.League)
                  .WithMany()
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.LeagueId, e.Key }).IsUnique();
        });
    }

    private static void ConfigureFeatureFlags(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }

    private static void ConfigureLeagueMemberships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeagueMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<PlayerRole>(v, ignoreCase: true))
                  .HasMaxLength(20);
            entity.HasOne(e => e.League)
                  .WithMany(l => l.Memberships)
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.LeagueId, e.UserId }).IsUnique();
        });
    }

    private static void ConfigureSeasons(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.League)
                  .WithMany(l => l.Seasons)
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFlights(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasOne<League>()
                  .WithMany()
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Half)
                  .WithMany(h => h.Flights)
                  .HasForeignKey(e => e.HalfId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.HalfId, e.DisplayOrder });
        });
    }

    private static void ConfigureSeasonHalves(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeasonHalf>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            // Restrict so deleting a Season doesn't silently wipe a year of
            // history; the SeasonHalf is itself the cascade root for Flights,
            // FlightMemberships and Rounds.
            entity.HasOne(e => e.Season)
                  .WithMany(s => s.Halves)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.SeasonId, e.HalfNumber }).IsUnique();
        });
    }

    private static void ConfigurePlayers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.PreferredTeeTimeSlots)
                  .HasConversion<int>();
            entity.HasOne(e => e.League)
                  .WithMany(l => l.Players)
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AppUser)
                  .WithMany()
                  .HasForeignKey(e => e.AppUserId)
                  .OnDelete(DeleteBehavior.SetNull);
            // A user can have at most one Player profile per league, but may
            // hold separate Player rows across different leagues.
            entity.HasIndex(e => new { e.AppUserId, e.LeagueId }).IsUnique();
        });
    }

    private static void ConfigureFlightMemberships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlightMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Players are soft-deleted (IsActive=false). Hard-deleting a player
            // with active memberships should fail loud rather than silently
            // wipe their season participation.
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.FlightMemberships)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            // The SeasonHalf cascade is the single owning chain. Dropping a
            // Flight independent of its Half is blocked until memberships are
            // cleared explicitly — prevents losing data on accidental delete.
            entity.HasOne(e => e.Flight)
                  .WithMany(f => f.Memberships)
                  .HasForeignKey(e => e.FlightId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Half)
                  .WithMany()
                  .HasForeignKey(e => e.HalfId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.PlayerId, e.HalfId }).IsUnique();
        });
    }

    private static void ConfigureHandicaps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Handicap>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Source)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<HandicapSource>(v))
                  .HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500);
            // NineHoleHandicapIndex is a computed read-only property on the
            // entity; tell EF not to try to map it as a column.
            entity.Ignore(e => e.NineHoleHandicapIndex);
            // Handicaps are an audit trail. Don't wipe them when a player
            // is hard-deleted — the delete should be blocked instead, which
            // matches how the app already soft-deletes players.
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.Handicaps)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.PlayerId, e.EffectiveDate });
        });
    }

    private static void ConfigureCourses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });
    }

    private static void ConfigureCourseHoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseHole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Par)
                  .HasAnnotation("MinValue", 3)
                  .HasAnnotation("MaxValue", 5);
            entity.HasOne(e => e.Course)
                  .WithMany(c => c.Holes)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(tb => tb.HasCheckConstraint("CK_CourseHole_Par", "Par BETWEEN 3 AND 5"));
        });
    }

    private static void ConfigureTeeBoxes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeeBox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CourseRating).IsRequired();
            entity.Property(e => e.SlopeRating).IsRequired();
            entity.Property(e => e.TotalYardage).IsRequired();
            entity.Property(e => e.Par).IsRequired();
            entity.HasOne(e => e.Course)
                  .WithMany(c => c.TeeBoxes)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CourseId, e.Name });
        });
    }

    private static void ConfigureHoleTeeBoxes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HoleTeeBox>(entity =>
        {
            entity.HasKey(e => new { e.TeeBoxId, e.CourseHoleId });
            entity.HasOne(e => e.TeeBox)
                  .WithMany(t => t.HoleTeeBoxes)
                  .HasForeignKey(e => e.TeeBoxId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CourseHole)
                  .WithMany(h => h.HoleTeeBoxes)
                  .HasForeignKey(e => e.CourseHoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRounds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Round>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<RoundStatus>(v))
                  .HasMaxLength(30);
            entity.Property(e => e.NineHoleSide)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<NineHoleSide>(v))
                  .HasMaxLength(20);
            entity.Property(e => e.RoundType)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<RoundType>(v))
                  .HasMaxLength(20)
                  .HasDefaultValue(RoundType.NineHole);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne<League>()
                  .WithMany()
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Season)
                  .WithMany(s => s.Rounds)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Half)
                  .WithMany(h => h.Rounds)
                  .HasForeignKey(e => e.HalfId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course)
                  .WithMany(c => c.Rounds)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.HalfId, e.WeekNumber }).IsUnique();
        });
    }

    private static void ConfigureRoundParticipants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoundParticipant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Round)
                  .WithMany(r => r.Participants)
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.RoundParticipants)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Flight)
                  .WithMany()
                  .HasForeignKey(e => e.FlightId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
            // Tee-time link: nullable, SetNull on delete so deleting a tee
            // time (e.g. admin regenerating the schedule) clears assignments
            // rather than wiping the participant rows.
            entity.HasOne(e => e.TeeTime)
                  .WithMany(t => t.Participants)
                  .HasForeignKey(e => e.TeeTimeId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.RoundId, e.PlayerId }).IsUnique();
            entity.HasIndex(e => new { e.RoundId, e.FlightId });
            entity.HasIndex(e => e.TeeTimeId);
        });
    }

    private static void ConfigureTournamentMatchups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentMatchup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Round)
                  .WithMany(r => r.TournamentMatchups)
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Player1)
                  .WithMany()
                  .HasForeignKey(e => e.Player1Id)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Player2)
                  .WithMany()
                  .HasForeignKey(e => e.Player2Id)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.RoundId, e.MatchupNumber }).IsUnique();
        });
    }

    private static void ConfigureTournamentHoleExtras(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentHoleExtra>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Round)
                  .WithMany(r => r.TournamentHoleExtras)
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ClosestToPinPlayer)
                  .WithMany()
                  .HasForeignKey(e => e.ClosestToPinPlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LongestDrivePlayer)
                  .WithMany()
                  .HasForeignKey(e => e.LongestDrivePlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.RoundId, e.HoleNumber }).IsUnique();
        });
    }

    private static void ConfigureTournamentLongestDriveWinners(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentLongestDriveWinner>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Round)
                  .WithMany(r => r.TournamentLongestDriveWinners)
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.RoundId, e.PlayerId }).IsUnique();
        });
    }

    private static void ConfigureRoundClosestToPins(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoundClosestToPin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Round)
                  .WithMany(r => r.ClosestToPinWinners)
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
            // One winner per hole per round; "None" is simply the absence of a row.
            entity.HasIndex(e => new { e.RoundId, e.HoleNumber }).IsUnique();
        });
    }

    private static void ConfigureRoundTeeTimes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoundTeeTime>(entity =>
        {
            entity.HasKey(e => e.Id);
            // NoAction (rather than Cascade) on Round → RoundTeeTime because
            // RoundParticipants → RoundTeeTimes uses SetNull, and SQL Server
            // refuses multiple cascade paths to the same target table when one
            // is Cascade and another is SetNull (error 1785). If admin deletes
            // a Round, the FK will block until the tee times are cleared
            // manually first — typically not needed since admin uses
            // CancelRoundCommand which leaves the row in place.
            entity.HasOne(e => e.Round)
                  .WithMany()
                  .HasForeignKey(e => e.RoundId)
                  .OnDelete(DeleteBehavior.NoAction);
            // (RoundId, TeeTimeNumber) uniquely identifies a slot — prevents
            // duplicate slot rows from racing inserts.
            entity.HasIndex(e => new { e.RoundId, e.TeeTimeNumber }).IsUnique();
        });
    }

    private static void ConfigureHoleScores(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HoleScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Participant)
                  .WithMany(rp => rp.HoleScores)
                  .HasForeignKey(e => e.ParticipantId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ParticipantId);
            entity.Property(e => e.Putts);
            entity.Property(e => e.FirstPuttDistanceFeet);
            entity.Property(e => e.FairwayHit);
            entity.Property(e => e.Gir);
            entity.Property(e => e.LastModifiedByPlayerId);
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.LeagueId);
            entity.HasIndex(e => e.LeagueId);
        });
    }

    private static void ConfigurePlayerInvites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasOne(e => e.League)
                  .WithMany()
                  .HasForeignKey(e => e.LeagueId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
            entity.Property(e => e.InvitedByUserId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<InviteStatus>(v, ignoreCase: true))
                  .HasMaxLength(20);
            entity.Property(e => e.Role)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<Domain.Enums.PlayerRole>(v, ignoreCase: true))
                  .HasMaxLength(20)
                  .IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.SetNull);
            // NoAction (instead of SetNull) because PlayerInvites already has
            // another nullable FK to Players (PlayerId) configured as SetNull.
            // SQL Server refuses multiple cascade / set-null paths to the
            // same target (error 1785), so this second FK blocks Player
            // deletes when an invite still references the row — admin must
            // revoke or delete the invite first. That's the safer behavior
            // anyway.
            entity.HasOne(e => e.PreLinkedPlayer)
                  .WithMany()
                  .HasForeignKey(e => e.PreLinkedPlayerId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureAppUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.TotpSecret).HasMaxLength(64);
            entity.Property(e => e.IsSuperAdmin).HasDefaultValue(false);
        });
    }

    private static void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Ignore(e => e.IsActive);
        });
    }

    private static void ConfigureUserPasskeys(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPasskey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CredentialId).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Passkeys)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CredentialId).IsUnique();
            entity.HasIndex(e => e.UserId);
        });
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Bypass filter entirely when running outside an HTTP request (migrations,
        // startup jobs) — _leagueContext is null or IsSet is false in those cases.
        // When a league slug was resolved (LeagueId != null), scope ALL users
        // including SuperAdmins to that league — the slug is the URL the user is
        // viewing and must be the authoritative scope.
        // SuperAdmin bypasses only when no league slug was provided (LeagueId is
        // null), allowing true cross-league admin calls.
        modelBuilder.Entity<Season>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == _leagueContext.LeagueId);

        modelBuilder.Entity<Player>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == _leagueContext.LeagueId);

        modelBuilder.Entity<PlayerInvite>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == _leagueContext.LeagueId);

        modelBuilder.Entity<Round>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == _leagueContext.LeagueId);

        modelBuilder.Entity<Flight>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == _leagueContext.LeagueId);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e => _leagueContext == null
                || !_leagueContext.IsSet
                || (_leagueContext.IsSuperAdmin && _leagueContext.LeagueId == null)
                || e.LeagueId == null
                || e.LeagueId == _leagueContext.LeagueId);
    }
}
