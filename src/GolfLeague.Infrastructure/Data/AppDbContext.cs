using Azure.Storage.Blobs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public sealed class AppDbContext : BlobSyncedDbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName)
        : base(options, containerClient, localFilePath, blobName)
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
    public DbSet<HoleScore> HoleScores => Set<HoleScore>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlayerInvite> PlayerInvites => Set<PlayerInvite>();

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
        ConfigureHoleScores(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigurePlayerInvites(modelBuilder);
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
                  .WithMany(s => s.Flights)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Half)
                  .WithMany(h => h.Flights)
                  .HasForeignKey(e => e.HalfId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSeasonHalves(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeasonHalf>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Season)
                  .WithMany(s => s.Halves)
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SeasonId, e.StartDate });
        });
    }

    private static void ConfigurePlayers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.EntraObjectId).IsRequired().HasMaxLength(36);
            entity.HasIndex(e => e.EntraObjectId).IsUnique();
        });
    }

    private static void ConfigureFlightMemberships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlightMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.FlightMemberships)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Flight)
                  .WithMany(f => f.Memberships)
                  .HasForeignKey(e => e.FlightId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Season)
                  .WithMany()
                  .HasForeignKey(e => e.SeasonId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Half)
                  .WithMany()
                  .HasForeignKey(e => e.HalfId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.PlayerId, e.SeasonId });
            entity.HasIndex(e => new { e.PlayerId, e.HalfId });
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
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.Handicaps)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.RoundType)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<RoundType>(v))
                  .HasMaxLength(20);
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
            entity.HasOne(e => e.Flight)
                  .WithMany(f => f.Rounds)
                  .HasForeignKey(e => e.FlightId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Course)
                  .WithMany(c => c.Rounds)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(e => new { e.RoundId, e.PlayerId }).IsUnique();
            entity.HasIndex(e => new { e.RoundId, e.FlightId });
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
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(36);
        });
    }

    private static void ConfigurePlayerInvites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
            entity.Property(e => e.InvitedByUserId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.AcceptedByEntraObjectId).HasMaxLength(36);
            entity.Property(e => e.Status)
                  .HasConversion(
                      v => v.ToString(),
                      v => Enum.Parse<InviteStatus>(v))
                  .HasMaxLength(20);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
