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
            CellPhone = PhoneNormalizer.Normalize(request.CellPhone) ?? string.Empty,
            Email = request.Email.Trim(),
            Language = request.Language,
            HasWhatsApp = request.HasWhatsApp,
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
        account.HasWhatsApp = registration.HasWhatsApp;

        // Cache the classification list once — we'll look up each player's DOB against it below.
        var classifications = await _db.AgeClassifications.ToListAsync(ct);

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

            // Auto-assign the age bracket the player's DOB falls into. Null when no defined bracket
            // covers the DOB; admin can add or widen one and re-save the registration to fix.
            var classification = classifications
                .FirstOrDefault(c => c.DobStart <= player.DateOfBirth && player.DateOfBirth <= c.DobEnd);

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
                AgeClassificationId = classification?.Id,
            });
        }

        await SyncAdditionalParentsAsync(account, request.AdditionalParents, registration.Language, ct);
        registration.ParentAccount = account;

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
                r.Language, r.HasWhatsApp, r.Players.Count, r.CreatedAt))
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
                r.Language, r.HasWhatsApp, r.Players.Count, r.CreatedAt))
            .Take(500)
            .ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>Admin-only patch of registration contact info + flags. Waiver consent, signatures,
    /// and the player roster are intentionally immutable here — those are the legally-binding
    /// submission artifacts.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RegistrationDetail>> Update(
        int id, [FromBody] UpdateRegistrationRequest request, CancellationToken ct)
    {
        var registration = await _db.Registrations
            .Include(r => r.ParentAccount).ThenInclude(pa => pa!.Contacts)
            .Include(r => r.Players).ThenInclude(rp => rp.Player)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (registration is null) return NotFound();

        registration.ParentFirstName = request.ParentFirstName.Trim();
        registration.ParentLastName = request.ParentLastName.Trim();
        registration.AddressLine1 = request.AddressLine1?.Trim() ?? "";
        registration.AddressLine2 = request.AddressLine2?.Trim();
        registration.City = request.City?.Trim() ?? "";
        registration.State = request.State?.Trim() ?? "";
        registration.PostalCode = request.PostalCode?.Trim() ?? "";
        registration.CellPhone = PhoneNormalizer.Normalize(request.CellPhone) ?? string.Empty;
        registration.Email = request.Email.Trim();
        registration.Language = request.Language;
        registration.HasWhatsApp = request.HasWhatsApp;

        // Mirror the same fields onto the live ParentAccount profile so prefill stays in sync.
        if (registration.ParentAccount is not null)
        {
            registration.ParentAccount.FirstName = registration.ParentFirstName;
            registration.ParentAccount.LastName = registration.ParentLastName;
            registration.ParentAccount.AddressLine1 = registration.AddressLine1;
            registration.ParentAccount.AddressLine2 = registration.AddressLine2;
            registration.ParentAccount.City = registration.City;
            registration.ParentAccount.State = registration.State;
            registration.ParentAccount.PostalCode = registration.PostalCode;
            registration.ParentAccount.CellPhone = registration.CellPhone;
            registration.ParentAccount.Language = registration.Language;
            registration.ParentAccount.HasWhatsApp = registration.HasWhatsApp;

            await SyncAdditionalParentsAsync(registration.ParentAccount, request.AdditionalParents, registration.Language, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToDetail(registration));
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
            .Include(x => x.ParentAccount).ThenInclude(pa => pa!.Contacts)
            .Include(x => x.Players)
                .ThenInclude(rp => rp.Player)
            .Include(x => x.Players)
                .ThenInclude(rp => rp.AgeClassification)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (registration is null) return (null, NotFound());

        if (User.IsInRole(Roles.Admin)) return (registration, null);

        var account = await GetAccountAsync(ct);
        if (account is null || registration.ParentAccountId != account.Id)
            return (null, Forbid());

        return (registration, null);
    }

    /// <summary>Admin-only toggle of a single player's free-trial-over flag. Per-player rather than
    /// per-registration so families with mixed trial status are handled correctly.</summary>
    [HttpPatch("{id:int}/players/{rpId:int}/trial")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RegistrationPlayerDetail>> UpdatePlayerTrial(
        int id, int rpId, [FromBody] UpdatePlayerTrialRequest request, CancellationToken ct)
    {
        var rp = await _db.RegistrationPlayers
            .Include(p => p.Player)
            .Include(p => p.AgeClassification)
            .FirstOrDefaultAsync(p => p.Id == rpId && p.RegistrationId == id, ct);
        if (rp is null) return NotFound();

        rp.FreeTrialOver = request.FreeTrialOver;
        await _db.SaveChangesAsync(ct);

        return Ok(new RegistrationPlayerDetail(
            rp.Id, rp.PlayerId,
            rp.Player!.FirstName, rp.Player.LastName, rp.Player.DateOfBirth,
            rp.SchoolGrade, rp.UniformSize, rp.ShoeSize, rp.HeardFrom,
            rp.WaiverParticipantName, rp.WaiverTeamName, rp.WaiverParentGuardianName,
            rp.WaiverPhone, rp.WaiverEmail,
            !string.IsNullOrEmpty(rp.SignatureDataUrl), rp.SignedAt,
            rp.FreeTrialOver, rp.AgeClassificationId, rp.AgeClassification?.Name));
    }

    /// <summary>Admin adds a player to a registration (creates the durable Player under the
    /// registration's account too). Enrolled unsigned — no signature is captured here.</summary>
    [HttpPost("{id:int}/players")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RegistrationDetail>> AddPlayer(
        int id, [FromBody] AddRegistrationPlayerRequest request, CancellationToken ct)
    {
        var registration = await _db.Registrations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (registration is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || request.DateOfBirth == default)
            return BadRequest("First name, last name, and date of birth are required.");

        var player = new Player
        {
            ParentAccountId = registration.ParentAccountId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
        };
        _db.Players.Add(player);

        _db.RegistrationPlayers.Add(new RegistrationPlayer
        {
            RegistrationId = registration.Id,
            Player = player,
            SchoolGrade = request.SchoolGrade.Trim(),
            UniformSize = request.UniformSize.Trim(),
            ShoeSize = request.ShoeSize.Trim(),
            HeardFrom = request.HeardFrom?.Trim(),
            WaiverParticipantName = $"{player.FirstName} {player.LastName}".Trim(),
            WaiverParentGuardianName = $"{registration.ParentFirstName} {registration.ParentLastName}".Trim(),
            WaiverPhone = registration.CellPhone,
            WaiverEmail = registration.Email,
            SignatureDataUrl = null,
            SignedAt = null,
            AgeClassificationId = await AssignAgeClassificationAsync(request.DateOfBirth, ct),
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    /// <summary>Admin edit of an enrolled player: per-season fields + the durable name/DOB.</summary>
    [HttpPut("{id:int}/players/{rpId:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RegistrationDetail>> UpdatePlayer(
        int id, int rpId, [FromBody] UpdateRegistrationPlayerRequest request, CancellationToken ct)
    {
        var rp = await _db.RegistrationPlayers
            .Include(p => p.Player)
            .FirstOrDefaultAsync(p => p.Id == rpId && p.RegistrationId == id, ct);
        if (rp is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || request.DateOfBirth == default)
            return BadRequest("First name, last name, and date of birth are required.");

        rp.Player!.FirstName = request.FirstName.Trim();
        rp.Player.LastName = request.LastName.Trim();
        rp.Player.DateOfBirth = request.DateOfBirth;
        rp.SchoolGrade = request.SchoolGrade.Trim();
        rp.UniformSize = request.UniformSize.Trim();
        rp.ShoeSize = request.ShoeSize.Trim();
        rp.AgeClassificationId = await AssignAgeClassificationAsync(request.DateOfBirth, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    /// <summary>Admin un-enrolls a player from this registration. The durable Player (and any team
    /// roster membership) is kept.</summary>
    [HttpDelete("{id:int}/players/{rpId:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RemovePlayer(int id, int rpId, CancellationToken ct)
    {
        var rp = await _db.RegistrationPlayers.FirstOrDefaultAsync(p => p.Id == rpId && p.RegistrationId == id, ct);
        if (rp is null) return NotFound();
        _db.RegistrationPlayers.Remove(rp);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Most-specific age bracket whose inclusive DOB range covers the date, or null.</summary>
    private async Task<int?> AssignAgeClassificationAsync(DateOnly dob, CancellationToken ct)
    {
        var classifications = await _db.AgeClassifications.ToListAsync(ct);
        return classifications.FirstOrDefault(c => c.DobStart <= dob && dob <= c.DobEnd)?.Id;
    }

    private async Task<RegistrationDetail> LoadDetailAsync(int id, CancellationToken ct)
    {
        var registration = await _db.Registrations
            .Include(x => x.ParentAccount).ThenInclude(pa => pa!.Contacts)
            .Include(x => x.Players).ThenInclude(rp => rp.Player)
            .Include(x => x.Players).ThenInclude(rp => rp.AgeClassification)
            .FirstAsync(x => x.Id == id, ct);
        return ToDetail(registration);
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

    /// <summary>Replace-all sync of a family account's additional parent/guardian contacts. Blank
    /// rows and rows missing both a name and a contact method are skipped; phones are E.164-normalized
    /// like the primary parent's cell; per-contact language defaults to the registration's language.</summary>
    private async Task SyncAdditionalParentsAsync(
        ParentAccount account, List<ParentContactInput> input, Language defaultLanguage, CancellationToken ct)
    {
        await _db.Entry(account).Collection(a => a.Contacts).LoadAsync(ct);
        if (account.Contacts.Count > 0)
        {
            _db.ParentContacts.RemoveRange(account.Contacts);
            account.Contacts.Clear();
        }

        foreach (var c in input ?? new List<ParentContactInput>())
        {
            var first = c.FirstName?.Trim() ?? string.Empty;
            var last = c.LastName?.Trim() ?? string.Empty;
            var email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(c.CellPhone) ? null : PhoneNormalizer.Normalize(c.CellPhone);

            // Need a name and at least one way to reach them.
            if (string.IsNullOrWhiteSpace(first)) continue;
            if (email is null && string.IsNullOrWhiteSpace(phone)) continue;

            account.Contacts.Add(new ParentContact
            {
                ParentAccountId = account.Id,
                FirstName = first,
                LastName = last,
                Email = email,
                CellPhone = phone,
                HasWhatsApp = c.HasWhatsApp,
                Language = c.Language ?? defaultLanguage,
            });
        }
    }

    private static ParentContactDto ToContactDto(ParentContact c) =>
        new(c.Id, c.FirstName, c.LastName, c.Email, c.CellPhone, c.HasWhatsApp, c.Language);

    private static RegistrationDetail ToDetail(Registration r) => new(
        r.Id, r.Season,
        r.ParentFirstName, r.ParentLastName, r.AddressLine1, r.AddressLine2,
        r.City, r.State, r.PostalCode, r.CellPhone, r.Email, r.Language,
        r.HasWhatsApp, r.WaiverConsent, r.WaiverSignedAt, r.CreatedAt,
        r.Players.Select(rp => new RegistrationPlayerDetail(
            rp.Id,
            rp.PlayerId,
            rp.Player!.FirstName, rp.Player.LastName, rp.Player.DateOfBirth,
            rp.SchoolGrade, rp.UniformSize, rp.ShoeSize, rp.HeardFrom,
            rp.WaiverParticipantName, rp.WaiverTeamName, rp.WaiverParentGuardianName,
            rp.WaiverPhone, rp.WaiverEmail,
            !string.IsNullOrEmpty(rp.SignatureDataUrl), rp.SignedAt,
            rp.FreeTrialOver, rp.AgeClassificationId, rp.AgeClassification?.Name
        )).ToList(),
        (r.ParentAccount?.Contacts ?? new List<ParentContact>())
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(ToContactDto).ToList()
    );
}
