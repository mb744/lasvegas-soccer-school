using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ParentAccount> ParentAccounts => Set<ParentAccount>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<RegistrationPlayer> RegistrationPlayers => Set<RegistrationPlayer>();
    public DbSet<Outreach> Outreaches => Set<Outreach>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.HasOne(u => u.ParentAccount)
                .WithOne(p => p.User!)
                .HasForeignKey<ParentAccount>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParentAccount>(b =>
        {
            b.HasIndex(p => p.UserId).IsUnique();
        });

        modelBuilder.Entity<Player>(b =>
        {
            b.HasOne(p => p.ParentAccount)
                .WithMany(a => a.Players)
                .HasForeignKey(p => p.ParentAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Registration>(b =>
        {
            b.HasOne(r => r.ParentAccount)
                .WithMany(a => a.Registrations)
                .HasForeignKey(r => r.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => new { r.ParentAccountId, r.Season });
        });

        modelBuilder.Entity<RegistrationPlayer>(b =>
        {
            b.HasOne(rp => rp.Registration)
                .WithMany(r => r.Players)
                .HasForeignKey(rp => rp.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(rp => rp.Player)
                .WithMany()
                .HasForeignKey(rp => rp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(rp => new { rp.RegistrationId, rp.PlayerId }).IsUnique();
            // Base64 PNG can be hundreds of KB.
            b.Property(rp => rp.SignatureDataUrl).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<Outreach>(b =>
        {
            b.HasOne(o => o.ParentAccount)
                .WithMany()
                .HasForeignKey(o => o.ParentAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(o => o.Email);
            b.HasIndex(o => o.Phone);
        });
    }
}
