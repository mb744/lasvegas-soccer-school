using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin → Settings → Event emails: the wording of the automatic "new event" / "event updated"
/// emails, per language. The details, change list and attendance buttons stay generated.
/// </summary>
[ApiController]
[Route("api/admin/event-email-templates")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
[RequirePermission(Permissions.SettingsManage)]
public class EventEmailTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventEmailTemplatesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<EventEmailTemplatesResponse>> Get(CancellationToken ct)
    {
        var saved = await _db.EventEmailTemplates.AsNoTracking().ToListAsync(ct);
        var items = new List<EventEmailTemplateDto>();
        foreach (var kind in new[] { EventEmailKind.Created, EventEmailKind.Updated, EventEmailKind.Reminder })
            foreach (var lang in new[] { Language.English, Language.Spanish })
            {
                var def = EventEmailWording.Default(kind, lang);
                var row = saved.FirstOrDefault(t => t.Kind == kind && t.Language == lang);
                items.Add(new EventEmailTemplateDto(kind, lang,
                    row?.Subject ?? def.Subject, row?.Message ?? def.Message, row?.Footer ?? def.Footer,
                    row is not null, row?.UpdatedAt, row?.UpdatedBy, def.Subject, def.Message, def.Footer));
            }
        return Ok(new EventEmailTemplatesResponse(items, EventEmailPlaceholders.All));
    }

    [HttpPut("{kind}/{language}")]
    public async Task<ActionResult<EventEmailTemplateDto>> Save(
        EventEmailKind kind, Language language, [FromBody] SaveEventEmailTemplateRequest req, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(language)) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Subject)) return BadRequest("The subject can't be empty.");
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest("The message can't be empty.");

        var row = await _db.EventEmailTemplates.FirstOrDefaultAsync(t => t.Kind == kind && t.Language == language, ct);
        if (row is null)
        {
            row = new EventEmailTemplate { Kind = kind, Language = language };
            _db.EventEmailTemplates.Add(row);
        }
        row.Subject = req.Subject.Trim();
        row.Message = req.Message.Trim();
        row.Footer = (req.Footer ?? string.Empty).Trim();
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        var def = EventEmailWording.Default(kind, language);
        return Ok(new EventEmailTemplateDto(kind, language, row.Subject, row.Message, row.Footer, true,
            row.UpdatedAt, row.UpdatedBy, def.Subject, def.Message, def.Footer));
    }

    /// <summary>Back to the built-in wording.</summary>
    [HttpDelete("{kind}/{language}")]
    public async Task<IActionResult> Reset(EventEmailKind kind, Language language, CancellationToken ct)
    {
        var row = await _db.EventEmailTemplates.FirstOrDefaultAsync(t => t.Kind == kind && t.Language == language, ct);
        if (row is not null)
        {
            _db.EventEmailTemplates.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Renders unsaved wording against a sample game, exactly as parents would get it.</summary>
    [HttpPost("preview")]
    public ActionResult<EventEmailPreviewDto> Preview([FromBody] EventEmailPreviewRequest req)
    {
        if (!Enum.IsDefined(req.Kind) || !Enum.IsDefined(req.Language)) return BadRequest("Unknown email.");
        var text = EventMessageText.For(req.Language);
        var start = DateTime.UtcNow.Date.AddDays(4).AddHours(1); // ~6 PM Pacific
        var sample = new EventSnapshot(ScheduledEventKind.Game, "U11 Red", start, start.AddMinutes(90), start.AddMinutes(-45),
            "Field 5", "Bettye Wilson Soccer Complex", "7353 Eugene Ave, Las Vegas, NV", "Rebels", true,
            req.Language == Language.Spanish ? "Local (blanco)" : "Home (white)", ShoeType.Cleats, null, null, false);
        EventSnapshot? before = null;
        IReadOnlyList<EventField> changes = Array.Empty<EventField>();
        if (req.Kind == EventEmailKind.Updated)
        {
            before = sample with { StartsAt = start.AddHours(-1), EndsAt = start.AddMinutes(30), Location = "Field 2" };
            changes = EventSnapshot.Diff(before, sample);
        }
        var kids = new[] { new KidRsvp("Ana", AttendanceStatus.Pending, "#") };
        var wording = new EventEmailWording(req.Subject ?? string.Empty, req.Message ?? string.Empty, req.Footer ?? string.Empty);
        var (subject, _, html) = EventEmail.Build(text, wording, sample, before, changes, "Maria", kids, "#");
        return Ok(new EventEmailPreviewDto(subject, html));
    }
}

public record EventEmailTemplateDto(
    EventEmailKind Kind, Language Language, string Subject, string Message, string Footer, bool IsCustom,
    DateTime? UpdatedAt, string? UpdatedBy, string DefaultSubject, string DefaultMessage, string DefaultFooter);

public record EventEmailTemplatesResponse(IReadOnlyList<EventEmailTemplateDto> Templates, IReadOnlyList<EventEmailPlaceholder> Placeholders);

public class SaveEventEmailTemplateRequest
{
    [Required, MaxLength(256)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(4000)] public string Message { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Footer { get; set; }
}

public class EventEmailPreviewRequest
{
    public EventEmailKind Kind { get; set; }
    public Language Language { get; set; }
    [MaxLength(256)] public string? Subject { get; set; }
    [MaxLength(4000)] public string? Message { get; set; }
    [MaxLength(2000)] public string? Footer { get; set; }
}

public record EventEmailPreviewDto(string Subject, string Html);
