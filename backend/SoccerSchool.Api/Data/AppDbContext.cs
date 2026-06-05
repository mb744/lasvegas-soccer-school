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
    public DbSet<ParentAccountCollaborator> ParentAccountCollaborators => Set<ParentAccountCollaborator>();
    public DbSet<ParentContact> ParentContacts => Set<ParentContact>();
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
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailTemplateVariable> EmailTemplateVariables => Set<EmailTemplateVariable>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamPlayer> TeamPlayers => Set<TeamPlayer>();
    public DbSet<TeamCoach> TeamCoaches => Set<TeamCoach>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<CoachCertification> CoachCertifications => Set<CoachCertification>();
    public DbSet<ScheduledGame> ScheduledGames => Set<ScheduledGame>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();
    public DbSet<TournamentAttendance> TournamentAttendances => Set<TournamentAttendance>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<PhraseTranslation> PhraseTranslations => Set<PhraseTranslation>();
    public DbSet<InboundMessage> InboundMessages => Set<InboundMessage>();
    public DbSet<MessagingSettings> MessagingSettings => Set<MessagingSettings>();
    public DbSet<AgeClassification> AgeClassifications => Set<AgeClassification>();

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

        modelBuilder.Entity<ParentAccountCollaborator>(b =>
        {
            b.HasOne(c => c.ParentAccount)
                .WithMany(p => p.Collaborators)
                .HasForeignKey(c => c.ParentAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            // SQL Server forbids multiple cascade paths to the same table, and AspNetUsers
            // already cascades through ParentAccount → ParentAccountCollaborators. Keep this
            // edge as NoAction; if a user is deleted we'll clean up collaborator rows in app
            // code (Identity user deletes are rare anyway).
            b.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            // A user can only be a collaborator once per family, and we look up rows by
            // (account, user) when toggling links.
            b.HasIndex(c => new { c.ParentAccountId, c.UserId }).IsUnique();
            b.HasIndex(c => c.UserId);
        });

        modelBuilder.Entity<ParentContact>(b =>
        {
            b.HasOne(c => c.ParentAccount)
                .WithMany(a => a.Contacts)
                .HasForeignKey(c => c.ParentAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(c => c.ParentAccountId);
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
            b.HasOne(rp => rp.AgeClassification)
                .WithMany()
                .HasForeignKey(rp => rp.AgeClassificationId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(rp => new { rp.RegistrationId, rp.PlayerId }).IsUnique();
            // Base64 PNG can be hundreds of KB.
            b.Property(rp => rp.SignatureDataUrl).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<AgeClassification>(b =>
        {
            b.HasIndex(c => c.Name).IsUnique();
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

        modelBuilder.Entity<EmailTemplate>(b =>
        {
            b.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<EmailTemplateVariable>(b =>
        {
            b.HasOne(v => v.Template)
                .WithMany(t => t.Variables)
                .HasForeignKey(v => v.EmailTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(v => new { v.EmailTemplateId, v.Position }).IsUnique();
        });

        modelBuilder.Entity<Broadcast>().HasOne(b => b.WhatsAppTemplate)
            .WithMany()
            .HasForeignKey(b => b.WhatsAppTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Broadcast>().HasOne(b => b.ScheduledGame)
            .WithMany()
            .HasForeignKey(b => b.ScheduledGameId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Broadcast>().HasOne(b => b.Tournament)
            .WithMany()
            .HasForeignKey(b => b.TournamentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Broadcast>().HasOne(b => b.Player)
            .WithMany()
            .HasForeignKey(b => b.PlayerId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Broadcast>().HasIndex(b => new { b.TournamentId, b.PlayerId, b.CreatedAt });
        modelBuilder.Entity<Broadcast>().HasIndex(b => new { b.BatchId, b.CreatedAt });

        modelBuilder.Entity<Team>(b =>
        {
            b.HasIndex(t => t.Name).IsUnique();
            b.HasOne(t => t.MessageGroup)
                .WithMany()
                .HasForeignKey(t => t.MessageGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TeamPlayer>(b =>
        {
            b.HasOne(tp => tp.Team)
                .WithMany(t => t.Roster)
                .HasForeignKey(tp => tp.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(tp => tp.Player)
                .WithMany()
                .HasForeignKey(tp => tp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(tp => new { tp.TeamId, tp.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<TeamCoach>(b =>
        {
            b.HasOne(c => c.Team)
                .WithMany(t => t.Coaches)
                .HasForeignKey(c => c.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(c => c.TeamId);
        });

        modelBuilder.Entity<Coach>(b =>
        {
            // MonthlyPayment is a money amount — fix precision so EF doesn't infer a default
            // truncating type. 10,2 covers anything realistic for a youth-league stipend.
            b.Property(c => c.MonthlyPayment).HasPrecision(10, 2);
            b.HasIndex(c => new { c.LastName, c.FirstName });
        });

        modelBuilder.Entity<CoachCertification>(b =>
        {
            b.HasOne(cc => cc.Coach)
                .WithMany(c => c.Certifications)
                .HasForeignKey(cc => cc.CoachId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(cc => cc.CoachId);
        });

        modelBuilder.Entity<Tournament>(b =>
        {
            // Tournament.TeamId is nullable now — admins create the tournament first, then build
            // its team. SetNull on team-delete so an accidental team delete doesn't take the
            // tournament's records with it.
            b.HasOne(t => t.Team)
                .WithMany()
                .HasForeignKey(t => t.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasMany(t => t.Games)
                .WithOne(g => g.Tournament!)
                .HasForeignKey(g => g.TournamentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EventAttendance>(b =>
        {
            b.HasOne(a => a.ScheduledGame)
                .WithMany()
                .HasForeignKey(a => a.ScheduledGameId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.Player)
                .WithMany()
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.ScheduledGameId, a.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<TournamentAttendance>(b =>
        {
            b.HasOne(a => a.Tournament)
                .WithMany(t => t.Attendances)
                .HasForeignKey(a => a.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.Player)
                .WithMany()
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.TournamentId, a.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<TournamentTeam>(b =>
        {
            b.HasOne(tt => tt.Tournament)
                .WithMany(t => t.Teams)
                .HasForeignKey(tt => tt.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict on Team-delete so a team that's referenced by a tournament can't be
            // silently removed — admin has to detach it from the tournament first.
            b.HasOne(tt => tt.Team)
                .WithMany()
                .HasForeignKey(tt => tt.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(tt => new { tt.TournamentId, tt.TeamId }).IsUnique();
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
            b.HasIndex(m => m.BroadcastId);
            b.HasOne(m => m.Broadcast)
                .WithMany()
                .HasForeignKey(m => m.BroadcastId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
