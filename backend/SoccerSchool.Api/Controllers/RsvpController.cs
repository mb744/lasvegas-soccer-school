using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// One-click attendance from event emails. The signed token names one event + one player, so no
/// login is needed. GET only reads; changing an answer is a POST that the web /rsvp page sends
/// from the browser, so link scanners that pre-open emailed URLs can't record answers.
/// </summary>
[ApiController]
[Route("api/rsvp")]
[AllowAnonymous]
public class RsvpController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRsvpTokens _tokens;

    public RsvpController(AppDbContext db, IRsvpTokens tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public const string InvalidLink = "This link has expired or isn't valid. Open the LV Soccer School app to answer.";

    [HttpGet]
    public async Task<ActionResult<RsvpDto>> Get([FromQuery] string t, CancellationToken ct)
    {
        if (!_tokens.TryRead(t, out var eventId, out var playerId)) return BadRequest(InvalidLink);
        var dto = await LoadAsync(eventId, playerId, ct);
        return dto is null ? BadRequest(InvalidLink) : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RsvpDto>> Answer([FromBody] RsvpAnswerRequest req, CancellationToken ct)
    {
        if (!_tokens.TryRead(req.Token, out var eventId, out var playerId)) return BadRequest(InvalidLink);
        AttendanceStatus? status = req.Answer?.Trim().ToLowerInvariant() switch
        {
            "going" => AttendanceStatus.Confirmed,
            "maybe" => AttendanceStatus.Maybe,
            "no" => AttendanceStatus.Declined,
            _ => null,
        };
        if (status is null) return BadRequest("Answer must be going, maybe or no.");

        var dto = await LoadAsync(eventId, playerId, ct);
        if (dto is null) return BadRequest(InvalidLink);
        if (!dto.CanChange) return Ok(dto); // cancelled or set by the coach: show why, change nothing

        var row = await _db.EventAttendances.FirstOrDefaultAsync(a => a.ScheduledGameId == eventId && a.PlayerId == playerId, ct);
        if (row is null)
        {
            row = new EventAttendance { ScheduledGameId = eventId, PlayerId = playerId };
            _db.EventAttendances.Add(row);
        }
        row.Status = status.Value;
        row.Source = AttendanceSource.ParentReply;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(dto with { Status = status.Value });
    }

    /// <summary>Null when the event is gone or the player is no longer on its team.</summary>
    private async Task<RsvpDto?> LoadAsync(int eventId, int playerId, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames.AsNoTracking()
            .Where(g => g.Id == eventId)
            .Select(g => new
            {
                g.TeamId, TeamName = g.Team!.Name, g.Kind, g.OpponentName, g.Summary, g.StartsAt, g.EndsAt, g.ArriveAt,
                g.IsCancelled, Place = g.Venue != null ? g.Venue.Name : g.Location,
            })
            .FirstOrDefaultAsync(ct);
        if (ev is null) return null;

        var player = await _db.TeamPlayers.AsNoTracking()
            .Where(tp => tp.TeamId == ev.TeamId && tp.PlayerId == playerId)
            .Select(tp => tp.Player!.FirstName)
            .FirstOrDefaultAsync(ct);
        if (player is null) return null;

        var row = await _db.EventAttendances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ScheduledGameId == eventId && a.PlayerId == playerId, ct);
        var lockedByCoach = row?.Source == AttendanceSource.Admin;

        return new RsvpDto(ev.TeamName, ev.Kind, ev.OpponentName, ev.Summary, ev.StartsAt, ev.EndsAt, ev.ArriveAt, ev.Place,
            player, row?.Status ?? AttendanceStatus.Pending, ev.IsCancelled, lockedByCoach,
            CanChange: !ev.IsCancelled && !lockedByCoach);
    }
}

public record RsvpAnswerRequest
{
    public string Token { get; init; } = string.Empty;
    public string? Answer { get; init; }
}

public record RsvpDto(
    string TeamName,
    ScheduledEventKind Kind,
    string? OpponentName,
    string? Summary,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? ArriveAt,
    string? Place,
    string PlayerFirstName,
    AttendanceStatus Status,
    bool IsCancelled,
    bool LockedByCoach,
    bool CanChange);
