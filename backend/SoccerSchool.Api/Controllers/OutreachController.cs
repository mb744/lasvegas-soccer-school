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

[ApiController]
[Route("api/outreach")]
[Authorize(Roles = Roles.Admin)]
public class OutreachController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOutreachSender _sender;
    private readonly AppOptions _app;

    public OutreachController(AppDbContext db, IOutreachSender sender, IOptions<AppOptions> app)
    {
        _db = db;
        _sender = sender;
        _app = app.Value;
    }

    [HttpPost]
    public async Task<ActionResult<OutreachResponse>> Create(
        [FromBody] CreateOutreachRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest("Provide an email or phone number.");

        var outreach = new Outreach
        {
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Language = request.Language,
            Status = OutreachStatus.Pending
        };
        _db.Outreaches.Add(outreach);
        await _db.SaveChangesAsync(ct);

        var link = BuildLink(outreach);
        var send = await _sender.SendAsync(outreach, link, ct);

        outreach.Status = send.Success ? OutreachStatus.Sent : OutreachStatus.Failed;
        outreach.StatusMessage = send.Message;
        outreach.SentAt = send.Success ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(outreach, link));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OutreachResponse>>> List(CancellationToken ct)
    {
        var items = await _db.Outreaches
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return Ok(items.Select(o => ToResponse(o, BuildLink(o))));
    }

    [HttpPost("{id:int}/resend")]
    public async Task<ActionResult<OutreachResponse>> Resend(int id, CancellationToken ct)
    {
        var outreach = await _db.Outreaches.FindAsync(new object?[] { id }, ct);
        if (outreach is null) return NotFound();

        var link = BuildLink(outreach);
        var send = await _sender.SendAsync(outreach, link, ct);

        outreach.Status = send.Success ? OutreachStatus.Sent : OutreachStatus.Failed;
        outreach.StatusMessage = send.Message;
        if (send.Success) outreach.SentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(outreach, link));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var outreach = await _db.Outreaches.FindAsync(new object?[] { id }, ct);
        if (outreach is null) return NotFound();
        _db.Outreaches.Remove(outreach);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private string BuildLink(Outreach o)
    {
        var baseUrl = _app.PublicBaseUrl.TrimEnd('/');
        var lang = o.Language == Language.Spanish ? "es" : "en";
        return $"{baseUrl}/signup?lang={lang}";
    }

    private static OutreachResponse ToResponse(Outreach o, string link) => new(
        o.Id, o.Email, o.Phone, o.Language, o.Status, o.StatusMessage, link,
        o.CreatedAt, o.SentAt, o.AccountCreatedAt, o.RegisteredAt, o.ParentAccountId
    );
}
