using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Auth;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IInviteSender _sender;
    private readonly AppOptions _app;
    private readonly ILogger<InvitationsController> _logger;

    public InvitationsController(
        AppDbContext db,
        IInviteSender sender,
        IOptions<AppOptions> app,
        ILogger<InvitationsController> logger)
    {
        _db = db;
        _sender = sender;
        _app = app.Value;
        _logger = logger;
    }

    [HttpPost]
    [RequireAdmin]
    public async Task<ActionResult<InvitationResponse>> Create(
        [FromBody] CreateInvitationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest("Provide an email or phone number.");

        var invitation = new Invitation
        {
            Token = TokenGenerator.New(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Language = request.Language,
            Status = InvitationStatus.Pending
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        var link = BuildLink(invitation);
        var send = await _sender.SendAsync(invitation, link, ct);

        invitation.Status = send.Success ? InvitationStatus.Sent : InvitationStatus.Failed;
        invitation.StatusMessage = send.Message;
        invitation.SentAt = send.Success ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(invitation, link));
    }

    [HttpGet]
    [RequireAdmin]
    public async Task<ActionResult<IEnumerable<InvitationResponse>>> List(CancellationToken ct)
    {
        var items = await _db.Invitations
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return Ok(items.Select(i => ToResponse(i, BuildLink(i))));
    }

    [HttpGet("by-token/{token}")]
    public async Task<ActionResult<InvitationLookupResponse>> Lookup(string token, CancellationToken ct)
    {
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null) return NotFound();

        if (invite.Status != InvitationStatus.Registered && invite.OpenedAt is null)
        {
            invite.OpenedAt = DateTime.UtcNow;
            invite.Status = InvitationStatus.Opened;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new InvitationLookupResponse(
            invite.Token,
            invite.Language,
            invite.Status,
            invite.Email,
            invite.Phone,
            invite.Status == InvitationStatus.Registered
        ));
    }

    [HttpPost("{id:int}/resend")]
    [RequireAdmin]
    public async Task<ActionResult<InvitationResponse>> Resend(int id, CancellationToken ct)
    {
        var invite = await _db.Invitations.FindAsync(new object?[] { id }, ct);
        if (invite is null) return NotFound();

        var link = BuildLink(invite);
        var send = await _sender.SendAsync(invite, link, ct);

        invite.Status = send.Success ? InvitationStatus.Sent : InvitationStatus.Failed;
        invite.StatusMessage = send.Message;
        if (send.Success) invite.SentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(invite, link));
    }

    private string BuildLink(Invitation i)
    {
        var baseUrl = _app.PublicBaseUrl.TrimEnd('/');
        var lang = i.Language == Language.Spanish ? "es" : "en";
        return $"{baseUrl}/register/{i.Token}?lang={lang}";
    }

    private static InvitationResponse ToResponse(Invitation i, string link) => new(
        i.Id, i.Token, i.Email, i.Phone, i.Language, i.Status, i.StatusMessage, link,
        i.CreatedAt, i.SentAt, i.RegisteredAt
    );
}
