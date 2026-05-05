using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Auth;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/registrations")]
public class RegistrationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWaiverPdfGenerator _pdf;

    public RegistrationsController(AppDbContext db, IWaiverPdfGenerator pdf)
    {
        _db = db;
        _pdf = pdf;
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

        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Token == request.Token, ct);
        if (invite is null) return NotFound("Invitation not found.");
        if (invite.Status == InvitationStatus.Registered)
            return Conflict("This invitation has already been used.");

        var now = DateTime.UtcNow;

        var registration = new Registration
        {
            ParentFirstName = request.ParentFirstName.Trim(),
            ParentLastName = request.ParentLastName.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            PostalCode = request.PostalCode.Trim(),
            CellPhone = request.CellPhone.Trim(),
            Email = request.Email.Trim(),
            Language = request.Language,
            WaiverConsent = request.WaiverConsent,
            WaiverSignedAt = now,
            Players = request.Players.Select(p => new Player
            {
                FirstName = p.FirstName.Trim(),
                LastName = p.LastName.Trim(),
                DateOfBirth = p.DateOfBirth,
                SchoolGrade = p.SchoolGrade.Trim(),
                ShirtSize = p.ShirtSize.Trim(),
                ShortSize = p.ShortSize.Trim(),
                ShoeSize = p.ShoeSize.Trim(),
                HeardFrom = p.HeardFrom?.Trim(),
                WaiverParticipantName = string.IsNullOrWhiteSpace(p.WaiverParticipantName)
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : p.WaiverParticipantName!.Trim(),
                WaiverTeamName = p.WaiverTeamName?.Trim(),
                WaiverParentGuardianName = string.IsNullOrWhiteSpace(p.WaiverParentGuardianName)
                    ? $"{request.ParentFirstName} {request.ParentLastName}".Trim()
                    : p.WaiverParentGuardianName!.Trim(),
                WaiverPhone = string.IsNullOrWhiteSpace(p.WaiverPhone) ? request.CellPhone.Trim() : p.WaiverPhone!.Trim(),
                WaiverEmail = string.IsNullOrWhiteSpace(p.WaiverEmail) ? request.Email.Trim() : p.WaiverEmail!.Trim(),
                SignatureDataUrl = p.SignatureDataUrl,
                SignedAt = now,
            }).ToList()
        };

        _db.Registrations.Add(registration);
        await _db.SaveChangesAsync(ct);

        invite.Status = InvitationStatus.Registered;
        invite.RegisteredAt = now;
        invite.RegistrationId = registration.Id;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(registration));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<ActionResult<IEnumerable<RegistrationSummary>>> List(CancellationToken ct)
    {
        var items = await _db.Registrations
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RegistrationSummary(
                r.Id, r.ParentFirstName, r.ParentLastName, r.Email, r.CellPhone,
                r.Language, r.Players.Count, r.CreatedAt))
            .Take(500)
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequireAdmin]
    public async Task<ActionResult<RegistrationDetail>> Get(int id, CancellationToken ct)
    {
        var r = await _db.Registrations
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? NotFound() : Ok(ToDetail(r));
    }

    /// <summary>Combined PDF — one waiver per player, all in one document.</summary>
    [HttpGet("{id:int}/waivers.pdf")]
    [RequireAdmin]
    public async Task<IActionResult> CombinedWaivers(int id, CancellationToken ct)
    {
        var r = await _db.Registrations
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();

        var bytes = _pdf.GenerateForRegistration(r);
        var filename = $"waivers-{r.Id}-{r.ParentLastName}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpGet("{id:int}/players/{playerId:int}/waiver.pdf")]
    [RequireAdmin]
    public async Task<IActionResult> PlayerWaiver(int id, int playerId, CancellationToken ct)
    {
        var r = await _db.Registrations
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        var player = r.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return NotFound();

        var bytes = _pdf.GenerateForPlayer(r, player);
        var filename = $"waiver-{r.Id}-{player.LastName}-{player.FirstName}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    private static RegistrationDetail ToDetail(Registration r) => new(
        r.Id, r.ParentFirstName, r.ParentLastName, r.AddressLine1, r.AddressLine2,
        r.City, r.State, r.PostalCode, r.CellPhone, r.Email, r.Language,
        r.WaiverConsent, r.WaiverSignedAt, r.CreatedAt,
        r.Players.Select(p => new PlayerDetail(
            p.Id, p.FirstName, p.LastName, p.DateOfBirth, p.SchoolGrade,
            p.ShirtSize, p.ShortSize, p.ShoeSize, p.HeardFrom,
            p.WaiverParticipantName, p.WaiverTeamName, p.WaiverParentGuardianName,
            p.WaiverPhone, p.WaiverEmail,
            !string.IsNullOrEmpty(p.SignatureDataUrl), p.SignedAt
        )).ToList()
    );
}
