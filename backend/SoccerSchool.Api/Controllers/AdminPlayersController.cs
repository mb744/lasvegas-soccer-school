using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin-only player management — list-all-players view backing the <c>/admin/players</c> hub
/// card, uniform-assignment CRUD (Player ↔ Uniform N:M with jersey number + given-on date),
/// admin-create-player + parent-account spin-up, and the parent-facing "registration invite"
/// email that links them to the signup/registration flow to add player info + sign the waiver.
/// Scoped separately from <see cref="PlayersController"/> (which is parent-side and limits each
/// caller to their own kids).
/// </summary>
[ApiController]
[Route("api/admin/players")]
[Authorize(Roles = Roles.Admin)]
public class AdminPlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly AppOptions _app;

    public AdminPlayersController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IEmailSender email,
        IOptions<AppOptions> app)
    {
        _db = db;
        _users = users;
        _email = email;
        _app = app.Value;
    }

    /// <summary>Every player in the system with the joined context the admin table needs:
    /// parent contact, current team, current-season registration status, and a one-glance summary
    /// of active uniform assignments. Filter via the <paramref name="q"/> query string — name,
    /// parent name, parent phone (digit-suffix), or team name; case-insensitive.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminPlayerSummaryDto>>> List(
        [FromQuery] string? q, CancellationToken ct)
    {
        var season = _app.ActiveSeason;
        var qq = q?.Trim();

        // Project everything we need in one shot so the admin list is a single round trip even
        // for hundreds of players. Per-row joins:
        //   - ParentAccount + User (for parent name + email + phone)
        //   - Latest RegistrationPlayer in the active season (for waiver + bracket)
        //   - Latest team (whatever team the player's currently on, if any)
        //   - PlayerUniformAssignments (for count + active jersey list)
        var rows = await _db.Players
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.DateOfBirth,
                ParentAccountId = (int?)p.ParentAccountId,
                ParentFirstName = p.ParentAccount != null ? p.ParentAccount.FirstName : null,
                ParentLastName = p.ParentAccount != null ? p.ParentAccount.LastName : null,
                ParentCellPhone = p.ParentAccount != null ? p.ParentAccount.CellPhone : null,
                ParentEmail = p.ParentAccount != null && p.ParentAccount.User != null ? p.ParentAccount.User.Email : null,
                CurrentTeamId = _db.TeamPlayers
                    .Where(tp => tp.PlayerId == p.Id)
                    .OrderByDescending(tp => tp.AddedAt)
                    .Select(tp => (int?)tp.TeamId)
                    .FirstOrDefault(),
                CurrentTeamName = _db.TeamPlayers
                    .Where(tp => tp.PlayerId == p.Id)
                    .OrderByDescending(tp => tp.AddedAt)
                    .Select(tp => tp.Team!.Name)
                    .FirstOrDefault(),
                ActiveReg = _db.RegistrationPlayers
                    .Where(rp => rp.PlayerId == p.Id && rp.Registration!.Season == season)
                    .Select(rp => new { rp.SignedAt, BracketName = rp.AgeClassification != null ? rp.AgeClassification.Name : null })
                    .FirstOrDefault(),
                UniformCount = _db.PlayerUniformAssignments.Count(a => a.PlayerId == p.Id),
                ActiveJerseyNumbers = _db.PlayerUniformAssignments
                    .Where(a => a.PlayerId == p.Id && a.ReturnedAt == null)
                    .OrderBy(a => a.AssignedAt)
                    .Select(a => a.JerseyNumber)
                    .ToList(),
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new AdminPlayerSummaryDto(
                r.Id,
                r.FirstName,
                r.LastName,
                r.DateOfBirth,
                r.ActiveReg?.BracketName,
                r.ParentAccountId,
                ComposeName(r.ParentFirstName, r.ParentLastName),
                r.ParentCellPhone,
                r.ParentEmail,
                r.CurrentTeamId,
                r.CurrentTeamName,
                r.ActiveReg?.SignedAt != null,
                r.ActiveReg != null,
                r.UniformCount,
                string.Join(", ", r.ActiveJerseyNumbers)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(qq))
        {
            // Plain substring filter on the projected fields. Digit-suffix match for phone so
            // "8859" hits "+18317568859".
            var lower = qq.ToLowerInvariant();
            var digits = new string(qq.Where(char.IsDigit).ToArray());
            items = items
                .Where(it =>
                    ($"{it.FirstName} {it.LastName}").ToLowerInvariant().Contains(lower)
                    || (it.ParentName ?? string.Empty).ToLowerInvariant().Contains(lower)
                    || (it.CurrentTeamName ?? string.Empty).ToLowerInvariant().Contains(lower)
                    || (digits.Length > 0 && (it.ParentCellPhone ?? string.Empty).Contains(digits)))
                .ToList();
        }

        return Ok(items
            .OrderBy(i => i.LastName).ThenBy(i => i.FirstName)
            .ToList());
    }

    /// <summary>All uniform assignments for one player — drawer/detail panel on the admin row.</summary>
    [HttpGet("{playerId:int}/uniforms")]
    public async Task<ActionResult<IEnumerable<PlayerUniformAssignmentDto>>> ListUniforms(
        int playerId, CancellationToken ct)
    {
        if (!await _db.Players.AnyAsync(p => p.Id == playerId, ct)) return NotFound();
        var rows = await _db.PlayerUniformAssignments
            .Where(a => a.PlayerId == playerId)
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new PlayerUniformAssignmentDto(
                a.Id,
                a.UniformId,
                a.Uniform!.Name,
                a.Uniform.Designation == UniformDesignation.None ? null : a.Uniform.Designation.ToString(),
                a.JerseyNumber,
                a.AssignedAt,
                a.ReturnedAt,
                a.Notes,
                a.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Records a new uniform assignment — admin hands a kit to a player with a jersey
    /// number on a date. Players can have several active assignments (Home kit + Away kit +
    /// Practice kit, or a replacement after a lost jersey).</summary>
    [HttpPost("{playerId:int}/uniforms")]
    public async Task<ActionResult<PlayerUniformAssignmentDto>> CreateUniform(
        int playerId, [FromBody] CreatePlayerUniformAssignmentRequest req, CancellationToken ct)
    {
        if (!await _db.Players.AnyAsync(p => p.Id == playerId, ct)) return NotFound();
        if (!await _db.Uniforms.AnyAsync(u => u.Id == req.UniformId, ct))
            return BadRequest("Pick an existing uniform from the catalog.");
        if (string.IsNullOrWhiteSpace(req.JerseyNumber))
            return BadRequest("Jersey number is required.");

        var assignment = new PlayerUniformAssignment
        {
            PlayerId = playerId,
            UniformId = req.UniformId,
            JerseyNumber = req.JerseyNumber.Trim(),
            AssignedAt = req.AssignedAt,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            AssignedByUserId = _users.GetUserId(User),
        };
        _db.PlayerUniformAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        // Re-load with the Uniform join so the response has the display name.
        var uniform = await _db.Uniforms.AsNoTracking().FirstAsync(u => u.Id == req.UniformId, ct);
        return Ok(new PlayerUniformAssignmentDto(
            assignment.Id,
            uniform.Id,
            uniform.Name,
            uniform.Designation == UniformDesignation.None ? null : uniform.Designation.ToString(),
            assignment.JerseyNumber,
            assignment.AssignedAt,
            assignment.ReturnedAt,
            assignment.Notes,
            assignment.CreatedAt));
    }

    [HttpPut("{playerId:int}/uniforms/{assignmentId:int}")]
    public async Task<ActionResult<PlayerUniformAssignmentDto>> UpdateUniform(
        int playerId, int assignmentId,
        [FromBody] UpdatePlayerUniformAssignmentRequest req, CancellationToken ct)
    {
        var row = await _db.PlayerUniformAssignments
            .Include(a => a.Uniform)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.PlayerId == playerId, ct);
        if (row is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.JerseyNumber))
            return BadRequest("Jersey number is required.");

        row.JerseyNumber = req.JerseyNumber.Trim();
        row.AssignedAt = req.AssignedAt;
        row.ReturnedAt = req.ReturnedAt;
        row.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        await _db.SaveChangesAsync(ct);

        return Ok(new PlayerUniformAssignmentDto(
            row.Id,
            row.UniformId,
            row.Uniform!.Name,
            row.Uniform.Designation == UniformDesignation.None ? null : row.Uniform.Designation.ToString(),
            row.JerseyNumber,
            row.AssignedAt,
            row.ReturnedAt,
            row.Notes,
            row.CreatedAt));
    }

    [HttpDelete("{playerId:int}/uniforms/{assignmentId:int}")]
    public async Task<IActionResult> DeleteUniform(int playerId, int assignmentId, CancellationToken ct)
    {
        var row = await _db.PlayerUniformAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.PlayerId == playerId, ct);
        if (row is null) return NotFound();
        _db.PlayerUniformAssignments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Admin creates a player. Either binds to an existing ParentAccount (picked by id
    /// from the parents picker) or spins up a fresh ParentAccount + ApplicationUser stub from
    /// the New* fields; the parent redeems the account via the registration invite email.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminPlayerSummaryDto>> CreatePlayer(
        [FromBody] AdminCreatePlayerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest("First and last name are required.");

        int parentAccountId;
        if (req.ParentAccountId is int existingPid)
        {
            if (!await _db.ParentAccounts.AnyAsync(a => a.Id == existingPid, ct))
                return BadRequest("Parent account not found.");
            parentAccountId = existingPid;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.NewParentEmail))
                return BadRequest("Pick an existing parent OR provide NewParentEmail to create one.");
            var email = req.NewParentEmail!.Trim();
            // Reuse an existing ApplicationUser if one already has this email — avoids a duplicate
            // when the parent signed up but never created a ParentAccount.
            var user = await _users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = false,
                };
                var createResult = await _users.CreateAsync(user);
                if (!createResult.Succeeded)
                    return BadRequest("Could not create user: " + string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
            var existing = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id, ct);
            if (existing is null)
            {
                var account = new ParentAccount
                {
                    UserId = user.Id,
                    FirstName = (req.NewParentFirstName ?? string.Empty).Trim(),
                    LastName = (req.NewParentLastName ?? string.Empty).Trim(),
                    CellPhone = string.IsNullOrWhiteSpace(req.NewParentCellPhone)
                        ? null
                        : PhoneNormalizer.Normalize(req.NewParentCellPhone!),
                };
                _db.ParentAccounts.Add(account);
                await _db.SaveChangesAsync(ct);
                parentAccountId = account.Id;
            }
            else
            {
                parentAccountId = existing.Id;
            }
        }

        var player = new Player
        {
            ParentAccountId = parentAccountId,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            DateOfBirth = req.DateOfBirth,
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);

        return Ok(new AdminPlayerSummaryDto(
            player.Id, player.FirstName, player.LastName, player.DateOfBirth,
            AgeBracket: null,
            ParentAccountId: parentAccountId,
            ParentName: null, ParentCellPhone: null, ParentEmail: null,
            CurrentTeamId: null, CurrentTeamName: null,
            WaiverSigned: false, RegisteredThisSeason: false,
            UniformCount: 0, ActiveJerseyNumbers: string.Empty));
    }

    /// <summary>Emails the parent a link to /register so they can sign the waiver + complete
    /// player info for the active season. The link points at the public registration page; the
    /// parent signs in (or resets their password) and the existing registration flow takes over
    /// from there.</summary>
    [HttpPost("send-registration-invite")]
    public async Task<ActionResult<SendRegistrationInviteResult>> SendRegistrationInvite(
        [FromBody] SendRegistrationInviteRequest req, CancellationToken ct)
    {
        var account = await _db.ParentAccounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == req.ParentAccountId, ct);
        if (account is null) return NotFound(new SendRegistrationInviteResult(false, "Parent account not found."));
        var email = account.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new SendRegistrationInviteResult(false, "Parent has no email on file. Add one before sending an invite."));

        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var link = string.IsNullOrEmpty(baseUrl) ? "/register" : $"{baseUrl}/register";

        var subject = $"Complete your {_app.ActiveSeason} registration — Las Vegas Soccer School";
        var parentName = string.IsNullOrWhiteSpace($"{account.FirstName} {account.LastName}".Trim())
            ? "there"
            : account.FirstName.Trim();
        var note = string.IsNullOrWhiteSpace(req.AdditionalNote) ? string.Empty : $"\n\nFrom your admin:\n{req.AdditionalNote!.Trim()}\n";
        var body =
            $"Hi {parentName},\n\n" +
            $"Las Vegas Soccer School needs you to finish your child's {_app.ActiveSeason} registration — please add player info (name, date of birth, grade, uniform/shoe size) and sign the parent/guardian waiver online.\n\n" +
            $"Open this link to continue:\n{link}\n\n" +
            $"If you don't have a password yet, click \"Sign up\" with this email address — you'll be able to set one and finish the form in a single sitting.{note}\n" +
            $"Thanks,\nLas Vegas Soccer School";

        var send = await _email.SendAsync(email!, subject, body, ct);
        return send.Success
            ? Ok(new SendRegistrationInviteResult(true, $"Invite sent to {email}."))
            : StatusCode(502, new SendRegistrationInviteResult(false, $"Email send failed: {send.Message}"));
    }

    private static string? ComposeName(string? first, string? last)
    {
        var name = $"{first ?? string.Empty} {last ?? string.Empty}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
