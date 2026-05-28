using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Read access to the signed-in family's additional parent/guardian contacts, so the registration
/// form can prefill the ones entered previously. Mutations happen through the registration
/// submit/update flow (replace-all sync), not here.
/// </summary>
[ApiController]
[Route("api/parent-contacts")]
[Authorize]
public class ParentContactsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ParentContactsController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentContactDto>>> Mine(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is null) return Ok(Array.Empty<ParentContactDto>());

        var contacts = await _db.ParentContacts
            .Where(c => c.ParentAccountId == account.Id)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new ParentContactDto(c.Id, c.FirstName, c.LastName, c.Email, c.CellPhone, c.HasWhatsApp, c.Language))
            .ToListAsync(ct);
        return Ok(contacts);
    }
}
