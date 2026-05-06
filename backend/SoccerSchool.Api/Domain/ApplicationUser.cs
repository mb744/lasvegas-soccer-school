using Microsoft.AspNetCore.Identity;

namespace SoccerSchool.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public ParentAccount? ParentAccount { get; set; }

    /// <summary>UTC of the most recent successful sign-in (any path).</summary>
    public DateTime? LastLoginAt { get; set; }
}
