using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invitation>(b =>
        {
            b.HasIndex(x => x.Token).IsUnique();
            b.HasOne(x => x.Registration)
                .WithMany()
                .HasForeignKey(x => x.RegistrationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Registration>(b =>
        {
            b.HasMany(x => x.Players)
                .WithOne(p => p.Registration!)
                .HasForeignKey(p => p.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(b =>
        {
            // Base64 PNG can be hundreds of KB.
            b.Property(p => p.SignatureDataUrl).HasColumnType("nvarchar(max)");
        });
    }
}
