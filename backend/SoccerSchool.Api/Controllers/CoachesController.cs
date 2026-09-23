using Microsoft.AspNetCore.Authorization;
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
/// Admin-only Coaches roster. Distinct from <see cref="TeamCoach"/> (per-team contact card used
/// for messaging fan-out): this is the HR-style profile that holds the coach's mailing address,
/// monthly stipend, and the list of coaching certifications they've earned.
/// </summary>
[ApiController]
[Route("api/coaches")]
[Authorize(Roles = Roles.Admin, AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class CoachesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly AppOptions _app;

    public CoachesController(AppDbContext db, IEmailSender email, IOptions<AppOptions> app)
    {
        _db = db;
        _email = email;
        _app = app.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CoachSummary>>> List(CancellationToken ct)
    {
        var items = await _db.Coaches
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new CoachSummary(
                c.Id, c.FirstName, c.LastName, c.CellPhone, c.Email,
                c.MonthlyPayment, c.Certifications.Count, c.UpdatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CoachDto>> Get(int id, CancellationToken ct)
    {
        var c = await _db.Coaches
            .Include(x => x.Certifications)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        return Ok(ToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<CoachDto>> Create([FromBody] SaveCoachRecordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest("First name and last name are required.");
        var c = new Coach();
        ApplyRequest(c, request);
        c.CreatedAt = c.UpdatedAt = DateTime.UtcNow;
        _db.Coaches.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(await ReloadAsync(c.Id, ct)));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CoachDto>> Update(int id, [FromBody] SaveCoachRecordRequest request, CancellationToken ct)
    {
        var c = await _db.Coaches.Include(x => x.Certifications).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest("First name and last name are required.");
        ApplyRequest(c, request);
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(c));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var c = await _db.Coaches.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound();
        _db.Coaches.Remove(c);  // Certifications cascade-delete via the FK config.
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Emails the coach a link to sign up for a login. Their signup uses the standard
    /// /signup form; matching them back to this Coach row happens by email after the fact.</summary>
    [HttpPost("{id:int}/send-invite")]
    public async Task<ActionResult<SendCoachInviteResult>> SendInvite(int id, CancellationToken ct)
    {
        var c = await _db.Coaches.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound(new SendCoachInviteResult(false, "Coach not found."));
        if (string.IsNullOrWhiteSpace(c.Email))
            return BadRequest(new SendCoachInviteResult(false, "Coach has no email on file. Add one before sending an invite."));

        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var link = string.IsNullOrEmpty(baseUrl) ? "/signup" : $"{baseUrl}/signup";

        var isSpanish = c.Language == Language.Spanish;
        var firstName = string.IsNullOrWhiteSpace(c.FirstName) ? (isSpanish ? "Entrenador" : "Coach") : c.FirstName.Trim();

        string subject, body;
        if (isSpanish)
        {
            subject = "Su cuenta de entrenador — Las Vegas Soccer School";
            body =
                $"Hola {firstName},\n\n" +
                "Las Vegas Soccer School le invita a crear una cuenta para acceder a la aplicación móvil y a los chats de equipo.\n\n" +
                $"Regístrese aquí con este correo electrónico:\n{link}\n\n" +
                "Una vez creada su cuenta, un administrador le agregará a los chats de sus equipos.\n\n" +
                "Gracias,\nLas Vegas Soccer School";
        }
        else
        {
            subject = "Your coach account — Las Vegas Soccer School";
            body =
                $"Hi {firstName},\n\n" +
                "Las Vegas Soccer School is inviting you to create an account so you can access the mobile app and team chats.\n\n" +
                $"Sign up here using this email address:\n{link}\n\n" +
                "Once your account is created, an admin will add you to your teams' chats.\n\n" +
                "Thanks,\nLas Vegas Soccer School";
        }

        var send = await _email.SendAsync(c.Email!, subject, body, ct);
        return send.Success
            ? Ok(new SendCoachInviteResult(true, $"Invite sent to {c.Email}."))
            : StatusCode(502, new SendCoachInviteResult(false, $"Email send failed: {send.Message}"));
    }

    // --- Certifications (nested) ---

    [HttpPost("{id:int}/certifications")]
    public async Task<ActionResult<CoachDto>> AddCertification(
        int id, [FromBody] SaveCoachCertificationRequest request, CancellationToken ct)
    {
        var c = await _db.Coaches.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Certification name is required.");
        _db.CoachCertifications.Add(new CoachCertification
        {
            CoachId = id,
            Name = request.Name.Trim(),
            IssuingBody = string.IsNullOrWhiteSpace(request.IssuingBody) ? null : request.IssuingBody.Trim(),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        });
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(await ReloadAsync(id, ct)));
    }

    [HttpPut("{id:int}/certifications/{certId:int}")]
    public async Task<ActionResult<CoachDto>> UpdateCertification(
        int id, int certId, [FromBody] SaveCoachCertificationRequest request, CancellationToken ct)
    {
        var cert = await _db.CoachCertifications.FirstOrDefaultAsync(x => x.Id == certId && x.CoachId == id, ct);
        if (cert is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Certification name is required.");
        cert.Name = request.Name.Trim();
        cert.IssuingBody = string.IsNullOrWhiteSpace(request.IssuingBody) ? null : request.IssuingBody.Trim();
        cert.IssuedOn = request.IssuedOn;
        cert.ExpiresOn = request.ExpiresOn;
        cert.CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim();
        cert.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var c = await _db.Coaches.FindAsync(new object?[] { id }, ct);
        if (c is not null) c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(await ReloadAsync(id, ct)));
    }

    [HttpDelete("{id:int}/certifications/{certId:int}")]
    public async Task<ActionResult<CoachDto>> RemoveCertification(int id, int certId, CancellationToken ct)
    {
        var cert = await _db.CoachCertifications.FirstOrDefaultAsync(x => x.Id == certId && x.CoachId == id, ct);
        if (cert is null) return NotFound();
        _db.CoachCertifications.Remove(cert);
        var c = await _db.Coaches.FindAsync(new object?[] { id }, ct);
        if (c is not null) c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(await ReloadAsync(id, ct)));
    }

    // --- Helpers ---

    private static void ApplyRequest(Coach c, SaveCoachRecordRequest r)
    {
        c.FirstName = r.FirstName.Trim();
        c.LastName = r.LastName.Trim();
        c.CellPhone = string.IsNullOrWhiteSpace(r.CellPhone) ? null : PhoneNormalizer.Normalize(r.CellPhone);
        c.HasWhatsApp = r.HasWhatsApp;
        c.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
        c.AddressLine1 = string.IsNullOrWhiteSpace(r.AddressLine1) ? null : r.AddressLine1.Trim();
        c.AddressLine2 = string.IsNullOrWhiteSpace(r.AddressLine2) ? null : r.AddressLine2.Trim();
        c.City = string.IsNullOrWhiteSpace(r.City) ? null : r.City.Trim();
        c.State = string.IsNullOrWhiteSpace(r.State) ? null : r.State.Trim();
        c.PostalCode = string.IsNullOrWhiteSpace(r.PostalCode) ? null : r.PostalCode.Trim();
        c.MonthlyPayment = r.MonthlyPayment;
        c.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        c.Language = r.Language;
    }

    private async Task<Coach> ReloadAsync(int id, CancellationToken ct) =>
        (await _db.Coaches.Include(x => x.Certifications).FirstAsync(x => x.Id == id, ct));

    private static CoachDto ToDto(Coach c) => new(
        c.Id, c.FirstName, c.LastName, c.CellPhone, c.HasWhatsApp, c.Email,
        c.AddressLine1, c.AddressLine2, c.City, c.State, c.PostalCode,
        c.MonthlyPayment, c.Notes, c.Language, c.CreatedAt, c.UpdatedAt,
        c.Certifications
            .OrderByDescending(x => x.IssuedOn ?? DateOnly.MinValue)
            .ThenBy(x => x.Name)
            .Select(x => new CoachCertificationDto(
                x.Id, x.CoachId, x.Name, x.IssuingBody, x.IssuedOn, x.ExpiresOn,
                x.CertificateNumber, x.Notes, x.CreatedAt))
            .ToList());
}
