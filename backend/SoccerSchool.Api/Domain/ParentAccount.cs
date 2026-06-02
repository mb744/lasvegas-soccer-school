using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class ParentAccount
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(80)]
    public string? City { get; set; }

    [MaxLength(40)]
    public string? State { get; set; } = "NV";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(32)]
    public string? CellPhone { get; set; }

    public Language Language { get; set; } = Language.English;

    /// <summary>Mirrors the latest <see cref="Registration.HasWhatsApp"/> the parent submitted. Lets
    /// the messaging layer route between SMS and WhatsApp without joining through registrations.</summary>
    public bool HasWhatsApp { get; set; }

    /// <summary>Admin-set opt-out for the entire family — primary parent + every additional
    /// guardian (<see cref="ParentContact"/>). When true, <see cref="Services.RecipientResolver"/>
    /// skips this family in every bulk recipient list (templates, broadcasts, monthly-fee runs,
    /// tournament confirmations, event reminders). Inbox-thread replies still go through since
    /// those are explicit one-off sends from an admin in response to the family contacting us.</summary>
    public bool NoCommunications { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Player> Players { get; set; } = new();
    public List<Registration> Registrations { get; set; } = new();

    /// <summary>Additional parent/guardian contacts (no login) reachable for messaging.</summary>
    public List<ParentContact> Contacts { get; set; } = new();

    /// <summary>Additional <see cref="ApplicationUser"/> logins (besides <see cref="UserId"/>'s
    /// owner) that have access to this family. Populated when an admin links a second parent's
    /// login to an existing family.</summary>
    public List<ParentAccountCollaborator> Collaborators { get; set; } = new();
}
