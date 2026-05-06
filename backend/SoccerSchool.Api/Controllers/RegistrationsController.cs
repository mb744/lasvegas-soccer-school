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

[ApiController]
[Route("api/registrations")]
[Authorize]
public class RegistrationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWaiverPdfGenerator _pdf;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppOptions _app;

    public RegistrationsController(
        AppDbContext db,
        IWaiverPdfGenerator pdf,
        UserManager<ApplicationUser> users,
        IOptions<AppOptions> app)
    {
        _db = db;
        _pdf = pdf;
        _users = users;
        _app = app.Value;
    }

    [HttpPost]
    public async Task<ActionResult<RegistrationDetail>> Submit(
        [FromBody] SubmitRegistrationRequest request,
        CancellationToken ct)
    {
        if (!request.WaiverConsent)
            return BadRequest("Waiver consent is required.");
        if (request.Players is null || request.Players.Count == 0)
            return BadRequest("At least one player is required.");
        for (var i = 0; i < request.Players.Count; i++)
        {
            var p = request.Players[i];
            if (string.IsNullOrWhiteSpace(p.SignatureDataUrl) || !p.SignatureDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Player {i + 1}: signature is required.");
        }

        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var now = DateTime.UtcNow;

        var registration = new Registration
        {
            ParentAccountId = account.Id,
            Season = _app.ActiveSeason,
            ParentFirstName = request.ParentFirstName.Trim(),
            ParentLastName = request.ParentLastName.Trim(),
            AddressLine1 = request.AddressLine1?.Trim() ?? "",
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City?.Trim() ?? "",
            State = request.State?.Trim() ?? "",
            PostalCode = request.PostalCode?.Trim() ?? "",
            CellPhone = request.CellPhone.Trim(),
            Email = request.Email.Trim(),
            Language = request.Language,
            WaiverConsent = request.WaiverConsent,
            WaiverSignedAt = now,
            CreatedAt = now,
        };

        // Sync parent profile with the values they just submitted (live profile, used for prefill next time).
        account.FirstName = registration.ParentFirstName;
        account.LastName = registration.ParentLastName;
        account.AddressLine1 = registration.AddressLine1;
        account.AddressLine2 = registration.AddressLine2;
        account.City = registration.City;
        account.State = registration.State;
        account.PostalCode = registration.PostalCode;
        account.CellPhone = registration.CellPhone;
        account.Language = registration.Language;

        foreach (var input in request.Players)
        {
            Player player;
            if (input.PlayerId is int existingId)
            {
                var existing = await _db.Players.FirstOrDefaultAsync(p => p.Id == existingId && p.ParentAccountId == account.Id, ct);
                if (existing is null) return BadRequest($"Player {existingId} not found in your roster.");
                player = existing;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName) || input.DateOfBirth is null)
                    return BadRequest("New players require FirstName, LastName, and DateOfBirth.");
                player = new Player
                {
                    ParentAccountId = account.Id,
                    FirstName = input.FirstName!.Trim(),
                    LastName = input.LastName!.Trim(),
                    DateOfBirth = input.DateOfBirth.Value
                };
                _db.Players.Add(player);
            }

            registration.Players.Add(new RegistrationPlayer
            {
                Player = player,
                SchoolGrade = input.SchoolGrade.Trim(),
                UniformSize = input.UniformSize.Trim(),
                ShoeSize = input.ShoeSize.Trim(),
                HeardFrom = input.HeardFrom?.Trim(),
                WaiverParticipantName = string.IsNullOrWhiteSpace(input.WaiverParticipantName)
                    ? $"{player.FirstName} {player.LastName}".Trim()
                    : input.WaiverParticipantName!.Trim(),
                WaiverTeamName = input.WaiverTeamName?.Trim(),
                WaiverParentGuardianName = string.IsNullOrWhiteSpace(input.WaiverParentGuardianName)
                    ? $"{registration.ParentFirstName} {registration.ParentLastName}".Trim()
                    : input.WaiverParentGuardianName!.Trim(),
                WaiverPhone = string.IsNullOrWhiteSpace(input.WaiverPhone) ? registration.CellPhone : input.WaiverPhone!.Trim(),
                WaiverEmail = string.IsNullOrWhiteSpace(input.WaiverEmail) ? registration.Email : input.WaiverEmail!.Trim(),
                SignatureDataUrl = input.SignatureDataUrl,
                SignedAt = now,
            });
        }

        _db.Registrations.Add(registration);
        await _db.SaveChangesAsync(ct);

        await AttributeOutreachRegisteredAsync(account.Id, ct);

        return Ok(ToDetail(registration));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<RegistrationSummary>>> Mine(CancellationToken ct)
    {
        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var items = await _db.Registrations
            .Where(r => r.ParentAccountId == account.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RegistrationSummary(
                r.Id, r.Season, r.ParentFirstName, r.ParentLastName, r.Email, r.CellPhone,
                r.Language, r.Players.Count, r.CreatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IEnumerable<RegistrationSummary>>> List([FromQuery] string? season, CancellationToken ct)
    {
        var query = _db.Registrations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(season))
            query = query.Where(r => r.Season == season);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RegistrationSummary(
                r.Id, r.Season, r.ParentFirstName, r.ParentLastName, r.Email, r.CellPhone,
                r.Language, r.Players.Count, r.CreatedAt))
            .Take(500)
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RegistrationDetail>> Get(int id, CancellationToken ct)
    {
        var (registration, denied) = await LoadAuthorizedAsync(id, ct);
        if (denied is not null) return denied;
        return Ok(ToDetail(registration!));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var registration = await _db.Registrations.FindAsync(new object?[] { id }, ct);
        if (registration is null) return NotFound();
        _db.Registrations.Remove(registration);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/waivers.pdf")]
    public async Task<IActionResult> CombinedWaivers(int id, CancellationToken ct)
    {
        var (registration, denied) = await LoadAuthorizedAsync(id, ct);
        if (denied is not null) return denied;
        var bytes = _pdf.GenerateForRegistration(registration!);
        var filename = $"waivers-{registration!.Id}-{registration.ParentLastName}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpGet("{id:int}/players/{rpId:int}/waiver.pdf")]
    public async Task<IActionResult> PlayerWaiver(int id, int rpId, CancellationToken ct)
    {
        var (registration, denied) = await LoadAuthorizedAsync(id, ct);
        if (denied is not null) return denied;
        var rp = registration!.Players.FirstOrDefault(p => p.Id == rpId);
        if (rp is null) return NotFound();

        var bytes = _pdf.GenerateForPlayer(registration, rp);
        var filename = $"waiver-{registration.Id}-{rp.Player!.LastName}-{rp.Player.FirstName}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    private async Task<(Registration? registration, ActionResult? denied)> LoadAuthorizedAsync(int id, CancellationToken ct)
    {
        var registration = await _db.Registrations
            .Include(x => x.Players)
                .ThenInclude(rp => rp.Player)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (registration is null) return (null, NotFound());

        if (User.IsInRole(Roles.Admin)) return (registration, null);

        var account = await GetAccountAsync(ct);
        if (account is null || registration.ParentAccountId != account.Id)
            return (null, Forbid());

        return (registration, null);
    }

    private async Task<ParentAccount?> GetAccountAsync(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return null;
        return await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
    }

    private async Task AttributeOutreachRegisteredAsync(int parentAccountId, CancellationToken ct)
    {
        var match = await _db.Outreaches
            .Where(o => o.ParentAccountId == parentAccountId && o.Status < OutreachStatus.Registered)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (match is null) return;
        match.Status = OutreachStatus.Registered;
        match.RegisteredAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static RegistrationDetail ToDetail(Registration r) => new(
        r.Id, r.Season,
        r.ParentFirstName, r.ParentLastName, r.AddressLine1, r.AddressLine2,
        r.City, r.State, r.PostalCode, r.CellPhone, r.Email, r.Language,
        r.WaiverConsent, r.WaiverSignedAt, r.CreatedAt,
        r.Players.Select(rp => new RegistrationPlayerDetail(
            rp.Id,
            rp.PlayerId,
            rp.Player!.FirstName, rp.Player.LastName, rp.Player.DateOfBirth,
            rp.SchoolGrade, rp.UniformSize, rp.ShoeSize, rp.HeardFrom,
            rp.WaiverParticipantName, rp.WaiverTeamName, rp.WaiverParentGuardianName,
            rp.WaiverPhone, rp.WaiverEmail,
            !string.IsNullOrEmpty(rp.SignatureDataUrl), rp.SignedAt
        )).ToList()
    );
}
