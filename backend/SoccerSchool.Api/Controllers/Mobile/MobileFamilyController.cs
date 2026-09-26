using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// The Profile tab's "Family" section: who's on the family, and inviting a grandparent, friend or
/// co-parent by email. The family owner and parents/guardians can invite, change access and remove;
/// view-only family members can see the list and remove themselves.
/// </summary>
[ApiController]
[Route("api/mobile/family")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileFamilyController : ControllerBase
{
    /// <summary>Minimum gap between invite emails to the same person.</summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IParentAccountResolver _accounts;
    private readonly IFamilyService _family;

    public MobileFamilyController(
        AppDbContext db, UserManager<ApplicationUser> users, IParentAccountResolver accounts, IFamilyService family)
    {
        _db = db;
        _users = users;
        _accounts = accounts;
        _family = family;
    }

    [HttpGet]
    public async Task<ActionResult<FamilyDto>> Get(CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        var members = await _family.ListMembersAsync(ctx.Family!, ctx.User!.Id, ct);
        return Ok(new FamilyDto(ctx.Family!.Id, ctx.Role.ToString().ToLowerInvariant(), ctx.CanManage, members));
    }

    [HttpPost("invites")]
    public async Task<ActionResult<FamilyDto>> Invite([FromBody] FamilyInviteRequest req, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        if (!ctx.CanManage) return StatusCode(StatusCodes.Status403Forbidden, "Only parents/guardians can invite people.");
        if (string.IsNullOrWhiteSpace(req.FirstName)) return BadRequest("First name is required.");

        var (outcome, _) = await _family.InviteAsync(
            ctx.Family!, ctx.User!, req.FirstName, req.LastName ?? "", req.Email ?? "",
            req.AccessLevel, req.Language ?? ctx.Family!.Language, ct);
        return outcome switch
        {
            InviteOutcome.InvalidEmail => BadRequest("Enter a valid email address."),
            InviteOutcome.IsOwner => BadRequest("That's the family's own account."),
            InviteOutcome.AlreadyMember => Conflict("That person is already part of this family."),
            InviteOutcome.EmailFailed => StatusCode(StatusCodes.Status502BadGateway,
                "The invite was saved but the email couldn't be sent. Try Resend in a few minutes."),
            _ => await Get(ct),
        };
    }

    [HttpPost("members/{contactId:int}/resend")]
    public async Task<IActionResult> Resend(int contactId, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        if (!ctx.CanManage) return StatusCode(StatusCodes.Status403Forbidden, "Only parents/guardians can send invites.");
        var contact = await ContactAsync(ctx.Family!, contactId, ct);
        if (contact is null) return NotFound();
        if (contact.UserId is not null) return Conflict("They've already joined.");
        if (contact.InviteSentAt is DateTime sent && DateTime.UtcNow - sent < ResendCooldown)
            return StatusCode(StatusCodes.Status429TooManyRequests, "An invite was just sent. Try again in a few minutes.");

        var outcome = await _family.ResendAsync(ctx.Family!, contact, ctx.User!, ct);
        return outcome switch
        {
            InviteOutcome.InvalidEmail => BadRequest("This person has no email address."),
            InviteOutcome.EmailFailed => StatusCode(StatusCodes.Status502BadGateway, "The email couldn't be sent. Try again in a few minutes."),
            _ => NoContent(),
        };
    }

    [HttpPut("members/{contactId:int}/access")]
    public async Task<IActionResult> SetAccess(int contactId, [FromBody] FamilyAccessRequest req, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        if (!ctx.CanManage) return StatusCode(StatusCodes.Status403Forbidden, "Only parents/guardians can change access.");
        var contact = await ContactAsync(ctx.Family!, contactId, ct);
        if (contact is null) return NotFound();
        if (contact.UserId == ctx.User!.Id && req.AccessLevel == FamilyAccessLevel.Viewer)
            return BadRequest("You can't make yourself view-only. Ask another parent to do it.");
        await _family.SetAccessAsync(ctx.Family!, contact, req.AccessLevel, ct);
        return NoContent();
    }

    /// <summary>Removes an invited or listed person. Anyone can remove themselves (leave the family).</summary>
    [HttpDelete("members/{contactId:int}")]
    public async Task<IActionResult> RemoveContact(int contactId, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        var contact = await ContactAsync(ctx.Family!, contactId, ct);
        if (contact is null) return NotFound();
        if (!ctx.CanManage && contact.UserId != ctx.User!.Id)
            return StatusCode(StatusCodes.Status403Forbidden, "Only parents/guardians can remove people.");
        await _family.RemoveContactAsync(ctx.Family!, contact, ct);
        return NoContent();
    }

    /// <summary>Removes a login linked without a contact entry (an admin linked them).</summary>
    [HttpDelete("links/{collaboratorId:int}")]
    public async Task<IActionResult> RemoveLink(int collaboratorId, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (ctx.Error is not null) return ctx.Error;
        var link = await _db.ParentAccountCollaborators.FirstOrDefaultAsync(
            c => c.Id == collaboratorId && c.ParentAccountId == ctx.Family!.Id, ct);
        if (link is null) return NotFound();
        if (!ctx.CanManage && link.UserId != ctx.User!.Id)
            return StatusCode(StatusCodes.Status403Forbidden, "Only parents/guardians can remove people.");
        await _family.RemoveCollaboratorAsync(link, ct);
        return NoContent();
    }

    private record Ctx(ApplicationUser? User, ParentAccount? Family, FamilyRole Role, bool CanManage, ActionResult? Error);

    private async Task<Ctx> ContextAsync(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return new Ctx(null, null, default, false, Unauthorized());
        var family = await _accounts.ResolveByUserIdAsync(user.Id, ct);
        if (family is null) return new Ctx(user, null, default, false, NotFound("You're not part of a family yet."));
        var role = await _accounts.RoleInAsync(user.Id, family.Id, ct) ?? FamilyRole.Viewer;
        return new Ctx(user, family, role, role is FamilyRole.Owner or FamilyRole.Guardian, null);
    }

    private Task<ParentContact?> ContactAsync(ParentAccount family, int contactId, CancellationToken ct) =>
        _db.ParentContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.ParentAccountId == family.Id, ct);
}

public record FamilyDto(int FamilyId, string YourRole, bool CanManage, IReadOnlyList<FamilyMember> Members);

public record FamilyInviteRequest
{
    [Required, MaxLength(80)]
    public string FirstName { get; init; } = string.Empty;

    [MaxLength(80)]
    public string? LastName { get; init; }

    [Required, MaxLength(256)]
    public string? Email { get; init; }

    public FamilyAccessLevel AccessLevel { get; init; } = FamilyAccessLevel.Viewer;

    /// <summary>Language of the invite email; defaults to the family's language.</summary>
    public Language? Language { get; init; }
}

public record FamilyAccessRequest
{
    public FamilyAccessLevel AccessLevel { get; init; }
}
