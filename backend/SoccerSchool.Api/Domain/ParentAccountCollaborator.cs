using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Links an additional <see cref="ApplicationUser"/> (a login) to a <see cref="ParentAccount"/>
/// they don't own. Lets two parents (mom + dad) sign up independently and then have both their
/// logins see the same kids, registrations, and waivers. The owner of the family is still
/// <see cref="ParentAccount.UserId"/>; collaborators are everyone else with access.
/// </summary>
public class ParentAccountCollaborator
{
    public int Id { get; set; }

    public int ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
