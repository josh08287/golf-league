using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public sealed class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonHalf> SeasonHalves => Set<SeasonHalf>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<FlightMembership> FlightMemberships => Set<FlightMembership>();
    public DbSet<Handicap> Handicaps => Set<Handicap>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseHole> CourseHoles => Set<CourseHole>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RoundParticipant> RoundParticipants => Set<RoundParticipant>();
    public DbSet<RoundTeeTime> RoundTeeTimes => Set<RoundTeeTime>();
    public DbSet<HoleScore> HoleScores => Set<HoleScore>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlayerInvite> PlayerInvites => Set<PlayerInvite>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureSeasons(modelBuilder);
        ConfigureSeasonHalves(modelBuilder);
        ConfigureFlights(modelBuilder);
        ConfigurePlayers(modelBuilder);
        ConfigureFlightMemberships(modelBuilder);
        ConfigureHandicaps(modelBuilder);
        ConfigureCourses(modelBuilder);
        ConfigureCourseHoles(modelBuilder);
        ConfigureRounds(modelBuilder);
        ConfigureRoundParticipants(modelBuilder);
        ConfigureRoundTeeTimes(modelBuilder);
        ConfigureHoleScores(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigurePlayerInvites(modelBuilder);
        ConfigureAppUsers(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureUserPasskeys(modelBuilder);
    }

    private static void ConfigureSeasons(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();
        });
    }

    private static void ConfigureFlights(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
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
            // Player has at most one AppUser; AppUser has at most one Player.
            // SetNull on delete so deleting an AppUser doesn't cascade-wipe
            // the player history.
            entity.Property(e => e.PreferredTeeTimeSlots)
                  .HasConversion<int>();
            entity.HasOne(e => e.AppUser)
                  .WithOne(u => u.Player)
                  .HasForeignKey<Player>(e => e.AppUserId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.AppUserId).IsUnique();
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
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Season)
                  .WithMany(s => s.Rounds)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Half)
                  .WithMany(h => h.Rounds)
                  .HasForeignKey(e => e.HalfId)
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
        });
    }

    private static void ConfigurePlayerInvites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
            entity.Property(e => e.InvitedByUserId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<InviteStatus>(v))
                  .HasMaxLength(20);
            entity.Property(e => e.Role)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<Domain.Enums.PlayerRole>(v))
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
            // PlayerId FK is configured on the Player side (1:1 inverse).
            // Roles live in AspNetUserRoles (Identity's built-in join table).
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
}
