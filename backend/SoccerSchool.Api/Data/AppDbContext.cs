using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// SQL Server <c>datetime2</c> has no time-zone info, so EF Core materializes columns as
    /// <see cref="DateTimeKind.Unspecified"/>. The default JSON serializer then writes them
    /// without a "Z" suffix, and browsers treat the strings as local time even though we
    /// wrote UTC. Stamping <c>Kind = Utc</c> on read makes the API surface round-trip-safe
    /// without changing storage. The "to-DB" leg defends against accidental Local kinds.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        { }
    }

    private sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter() : base(
            v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        { }
    }

    public DbSet<ParentAccount> ParentAccounts => Set<ParentAccount>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<RegistrationPlayer> RegistrationPlayers => Set<RegistrationPlayer>();
    public DbSet<Outreach> Outreaches => Set<Outreach>();
    public DbSet<MessageGroup> MessageGroups => Set<MessageGroup>();
    public DbSet<MessageGroupMember> MessageGroupMembers => Set<MessageGroupMember>();
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<BroadcastRecipient> BroadcastRecipients => Set<BroadcastRecipient>();
    public DbSet<GroupConversation> GroupConversations => Set<GroupConversation>();
    public DbSet<GroupConversationParticipant> GroupConversationParticipants => Set<GroupConversationParticipant>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<WhatsAppTemplateVariable> WhatsAppTemplateVariables => Set<WhatsAppTemplateVariable>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<ScheduledGame> ScheduledGames => Set<ScheduledGame>();
    public DbSet<PhraseTranslation> PhraseTranslations => Set<PhraseTranslation>();
    public DbSet<InboundMessage> InboundMessages => Set<InboundMessage>();

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

        modelBuilder.Entity<MessageGroup>(b =>
        {
            b.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<MessageGroupMember>(b =>
        {
            b.HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.MessageGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(m => m.ParentAccount)
                .WithMany()
                .HasForeignKey(m => m.ParentAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(m => new { m.MessageGroupId, m.Phone }).IsUnique();
        });

        modelBuilder.Entity<Broadcast>(b =>
        {
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<BroadcastRecipient>(b =>
        {
            b.HasOne(r => r.Broadcast)
                .WithMany(x => x.Recipients)
                .HasForeignKey(r => r.BroadcastId)
                .OnDelete(DeleteBehavior.Cascade);
            // Twilio status callback uses MessageSid to find the row to update.
            b.HasIndex(r => r.TwilioSid);
        });

        modelBuilder.Entity<GroupConversation>(b =>
        {
            b.HasIndex(c => c.TwilioConversationSid).IsUnique();
        });

        modelBuilder.Entity<GroupConversationParticipant>(b =>
        {
            b.HasOne(p => p.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.GroupConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => p.TwilioParticipantSid);
        });

        modelBuilder.Entity<WhatsAppTemplate>(b =>
        {
            b.HasIndex(t => t.Name).IsUnique();
            b.HasIndex(t => t.ContentSid);
        });

        modelBuilder.Entity<WhatsAppTemplateVariable>(b =>
        {
            b.HasOne(v => v.Template)
                .WithMany(t => t.Variables)
                .HasForeignKey(v => v.WhatsAppTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(v => new { v.WhatsAppTemplateId, v.Position }).IsUnique();
        });

        modelBuilder.Entity<Broadcast>().HasOne(b => b.WhatsAppTemplate)
            .WithMany()
            .HasForeignKey(b => b.WhatsAppTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Broadcast>().HasOne(b => b.ScheduledGame)
            .WithMany()
            .HasForeignKey(b => b.ScheduledGameId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Team>(b =>
        {
            b.HasIndex(t => t.Name).IsUnique();
            b.HasOne(t => t.MessageGroup)
                .WithMany()
                .HasForeignKey(t => t.MessageGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ScheduledGame>(b =>
        {
            b.HasOne(g => g.Team)
                .WithMany(t => t.Games)
                .HasForeignKey(g => g.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            // Upsert key on re-sync: (team, ICS UID) must be unique.
            b.HasIndex(g => new { g.TeamId, g.ExternalUid }).IsUnique();
            b.HasIndex(g => g.StartsAt);
        });

        modelBuilder.Entity<PhraseTranslation>(b =>
        {
            // Indexed for the longest-match-first lookup the translator does. Not unique on the
            // SQL side because admins might enter case-variant duplicates we'll dedupe in app code.
            b.HasIndex(p => p.English);
            b.HasIndex(p => p.Spanish);
        });

        modelBuilder.Entity<InboundMessage>(b =>
        {
            b.HasIndex(m => m.ReceivedAt);
            b.HasIndex(m => m.FromPhone);
        });
    }
}
