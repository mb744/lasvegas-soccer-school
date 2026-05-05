using Microsoft.AspNetCore.Identity;

namespace SoccerSchool.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public ParentAccount? ParentAccount { get; set; }
}
