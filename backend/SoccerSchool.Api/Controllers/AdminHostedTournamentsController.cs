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
/// Admin-side CRUD for tournaments/leagues that LVSS is HOSTING (as opposed to the participating
/// <see cref="Tournament"/> flow which tracks LVSS teams travelling to external tournaments).
/// Also owns the invited-teams catalog (external teams admin can roster into a hosted event
/// without retyping their coach contact each time) and the tournament↔team join CRUD.
/// </summary>
[ApiController]
[Route("api/admin/hosted-tournaments")]
[Authorize(Roles = Roles.Admin)]
public class AdminHostedTournamentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _app;

    public AdminHostedTournamentsController(
        AppDbContext db,
        IEmailSender emailSender,
        IOptions<AppOptions> app)
    {
        _db = db;
        _emailSender = emailSender;
        _app = app.Value;
    }

    // ------------------------------------------------------------
    // Hosted tournaments
    // ------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HostedTournamentDto>>> List(CancellationToken ct)
    {
        var rows = await LoadTournamentQuery().OrderByDescending(t => t.StartDate).ToListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HostedTournamentDto>> Get(int id, CancellationToken ct)
    {
        var row = await LoadTournamentQuery().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        return Ok(ToDto(row));
    }

    [HttpPost]
    public async Task<ActionResult<HostedTournamentDto>> Create(
        [FromBody] SaveHostedTournamentRequest req, CancellationToken ct)
    {
        var error = await ValidateAsync(req, id: null, ct);
        if (error is not null) return BadRequest(error);

        var now = DateTime.UtcNow;
        var t = new HostedTournament
        {
            Name = req.Name.Trim(),
            Kind = req.Kind,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            VenueId = req.VenueId,
            Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location!.Trim(),
            CostPerTeam = req.CostPerTeam,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim(),
            RulesOfPlay = string.IsNullOrWhiteSpace(req.RulesOfPlay) ? null : req.RulesOfPlay,
            ScheduleEmailBody = string.IsNullOrWhiteSpace(req.ScheduleEmailBody) ? null : req.ScheduleEmailBody,
            MatchDurationMinutes = req.MatchDurationMinutes,
            HalfMinutes = req.HalfMinutes,
            HalftimeMinutes = req.HalftimeMinutes,
            MinutesBetweenGames = req.MinutesBetweenGames,
            PublicSlug = await GenerateUniqueSlugAsync(req.Name, ct),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.HostedTournaments.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(t.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<HostedTournamentDto>> Update(
        int id, [FromBody] SaveHostedTournamentRequest req, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        var error = await ValidateAsync(req, id, ct);
        if (error is not null) return BadRequest(error);

        t.Name = req.Name.Trim();
        t.Kind = req.Kind;
        t.StartDate = req.StartDate;
        t.EndDate = req.EndDate;
        t.VenueId = req.VenueId;
        t.Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location!.Trim();
        t.CostPerTeam = req.CostPerTeam;
        t.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        t.RulesOfPlay = string.IsNullOrWhiteSpace(req.RulesOfPlay) ? null : req.RulesOfPlay;
        t.ScheduleEmailBody = string.IsNullOrWhiteSpace(req.ScheduleEmailBody) ? null : req.ScheduleEmailBody;
        t.MatchDurationMinutes = req.MatchDurationMinutes;
        t.HalfMinutes = req.HalfMinutes;
        t.HalftimeMinutes = req.HalftimeMinutes;
        t.MinutesBetweenGames = req.MinutesBetweenGames;
        // Backfill slug for events created before the public-link feature shipped.
        if (string.IsNullOrWhiteSpace(t.PublicSlug))
            t.PublicSlug = await GenerateUniqueSlugAsync(req.Name, ct);
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(t.Id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        _db.HostedTournaments.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------
    // Tournament ↔ team roster
    // ------------------------------------------------------------

    [HttpPost("{id:int}/teams")]
    public async Task<ActionResult<HostedTournamentDto>> AddTeam(
        int id, [FromBody] AddHostedTournamentTeamRequest req, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        var hasLvss = req.LvssTeamId.HasValue;
        var hasInvited = req.InvitedTeamId.HasValue;
        if (hasLvss == hasInvited)
            return BadRequest("Provide exactly one of LvssTeamId or InvitedTeamId.");
        if (hasLvss && !await _db.Teams.AnyAsync(x => x.Id == req.LvssTeamId, ct))
            return BadRequest("LVSS team not found.");
        if (hasInvited && !await _db.InvitedTeams.AnyAsync(x => x.Id == req.InvitedTeamId, ct))
            return BadRequest("Invited team not found.");
        // Prevent duplicate rosters — same team can only be added once per hosted event.
        var already = hasLvss
            ? await _db.HostedTournamentTeams.AnyAsync(r => r.HostedTournamentId == id && r.LvssTeamId == req.LvssTeamId, ct)
            : await _db.HostedTournamentTeams.AnyAsync(r => r.HostedTournamentId == id && r.InvitedTeamId == req.InvitedTeamId, ct);
        if (already) return Conflict("That team is already on this tournament.");

        _db.HostedTournamentTeams.Add(new HostedTournamentTeam
        {
            HostedTournamentId = id,
            LvssTeamId = req.LvssTeamId,
            InvitedTeamId = req.InvitedTeamId,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/teams/{teamRowId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> RemoveTeam(int id, int teamRowId, CancellationToken ct)
    {
        var row = await _db.HostedTournamentTeams
            .FirstOrDefaultAsync(x => x.Id == teamRowId && x.HostedTournamentId == id, ct);
        if (row is null) return NotFound();
        _db.HostedTournamentTeams.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Slot a rostered team into a tier (or clear the assignment with TierId=null).
    /// Rejects tiers that don't belong to the same event so a stray Id can't cross-link.</summary>
    [HttpPut("{id:int}/teams/{teamRowId:int}/tier")]
    public async Task<ActionResult<HostedTournamentDto>> AssignTeamTier(
        int id, int teamRowId, [FromBody] AssignTeamTierRequest req, CancellationToken ct)
    {
        var row = await _db.HostedTournamentTeams
            .FirstOrDefaultAsync(x => x.Id == teamRowId && x.HostedTournamentId == id, ct);
        if (row is null) return NotFound();
        if (req.TierId is int tid)
        {
            var ownsTier = await _db.HostedTournamentTiers.AnyAsync(t => t.Id == tid && t.HostedTournamentId == id, ct);
            if (!ownsTier) return BadRequest("Tier not found on this tournament.");
        }
        row.TierId = req.TierId;
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Toggle a team's paid status. Stamps PaidAt on the true→ false transition and
    /// clears it (and the payment details) on paid→ unpaid so unrelated invoicing state
    /// doesn't linger.</summary>
    [HttpPut("{id:int}/teams/{teamRowId:int}/paid")]
    public async Task<ActionResult<HostedTournamentDto>> SetTeamPaid(
        int id, int teamRowId, [FromBody] SetTeamPaidRequest req, CancellationToken ct)
    {
        var row = await _db.HostedTournamentTeams
            .FirstOrDefaultAsync(x => x.Id == teamRowId && x.HostedTournamentId == id, ct);
        if (row is null) return NotFound();
        var now = DateTime.UtcNow;
        if (req.Paid)
        {
            if (!row.Paid) row.PaidAt = now;
            row.Paid = true;
            row.PaymentMethod = TrimOrNull(req.PaymentMethod);
            row.PaymentReference = TrimOrNull(req.PaymentReference);
        }
        else
        {
            row.Paid = false;
            row.PaidAt = null;
            row.PaymentMethod = null;
            row.PaymentReference = null;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Slot a rostered team into a bracket (or clear the assignment with BracketId=null).
    /// Also syncs the team's TierId to the bracket's owning tier so downstream tier-scoped
    /// queries stay consistent.</summary>
    [HttpPut("{id:int}/teams/{teamRowId:int}/bracket")]
    public async Task<ActionResult<HostedTournamentDto>> AssignTeamBracket(
        int id, int teamRowId, [FromBody] AssignTeamBracketRequest req, CancellationToken ct)
    {
        var row = await _db.HostedTournamentTeams
            .FirstOrDefaultAsync(x => x.Id == teamRowId && x.HostedTournamentId == id, ct);
        if (row is null) return NotFound();
        if (req.BracketId is int bid)
        {
            var bracket = await _db.HostedTournamentBrackets
                .Include(x => x.Tier)
                .FirstOrDefaultAsync(x => x.Id == bid, ct);
            if (bracket is null || bracket.Tier is null || bracket.Tier.HostedTournamentId != id)
                return BadRequest("Bracket not found on this tournament.");
            row.BracketId = bid;
            row.TierId = bracket.TierId;
        }
        else
        {
            row.BracketId = null;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Tiers
    // ------------------------------------------------------------

    [HttpPost("{id:int}/tiers")]
    public async Task<ActionResult<HostedTournamentDto>> AddTier(
        int id, [FromBody] SaveHostedTournamentTierRequest req, CancellationToken ct)
    {
        if (!await _db.HostedTournaments.AnyAsync(t => t.Id == id, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Tier name is required.");
        _db.HostedTournamentTiers.Add(new HostedTournamentTier
        {
            HostedTournamentId = id,
            Name = req.Name.Trim(),
            SortOrder = req.SortOrder,
            Notes = TrimOrNull(req.Notes),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpPut("{id:int}/tiers/{tierId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateTier(
        int id, int tierId, [FromBody] SaveHostedTournamentTierRequest req, CancellationToken ct)
    {
        var tier = await _db.HostedTournamentTiers.FirstOrDefaultAsync(t => t.Id == tierId && t.HostedTournamentId == id, ct);
        if (tier is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Tier name is required.");
        tier.Name = req.Name.Trim();
        tier.SortOrder = req.SortOrder;
        tier.Notes = TrimOrNull(req.Notes);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/tiers/{tierId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> DeleteTier(int id, int tierId, CancellationToken ct)
    {
        var tier = await _db.HostedTournamentTiers.FirstOrDefaultAsync(t => t.Id == tierId && t.HostedTournamentId == id, ct);
        if (tier is null) return NotFound();
        // Rows referencing this tier get their TierId nulled via ClientSetNull; do it here in memory
        // so the tracked entities stay consistent before EF's cascade fires.
        var members = await _db.HostedTournamentTeams.Where(m => m.TierId == tierId).ToListAsync(ct);
        foreach (var m in members) m.TierId = null;
        _db.HostedTournamentTiers.Remove(tier);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Per-day schedule windows
    // ------------------------------------------------------------

    [HttpPost("{id:int}/days")]
    public async Task<ActionResult<HostedTournamentDto>> AddDay(
        int id, [FromBody] SaveHostedTournamentDayRequest req, CancellationToken ct)
    {
        if (!await _db.HostedTournaments.AnyAsync(t => t.Id == id, ct)) return NotFound();
        if (req.Date == default) return BadRequest("Date is required.");
        if (req.StartTime is TimeOnly s && req.EndTime is TimeOnly e && e < s)
            return BadRequest("End time can't be before start time.");
        if (await _db.HostedTournamentDays.AnyAsync(d => d.HostedTournamentId == id && d.Date == req.Date, ct))
            return Conflict("A day for that date already exists — edit the existing row instead.");
        _db.HostedTournamentDays.Add(new HostedTournamentDay
        {
            HostedTournamentId = id,
            Date = req.Date,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            Notes = TrimOrNull(req.Notes),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpPut("{id:int}/days/{dayId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateDay(
        int id, int dayId, [FromBody] SaveHostedTournamentDayRequest req, CancellationToken ct)
    {
        var day = await _db.HostedTournamentDays.FirstOrDefaultAsync(d => d.Id == dayId && d.HostedTournamentId == id, ct);
        if (day is null) return NotFound();
        if (req.Date == default) return BadRequest("Date is required.");
        if (req.StartTime is TimeOnly s && req.EndTime is TimeOnly e && e < s)
            return BadRequest("End time can't be before start time.");
        if (req.Date != day.Date
            && await _db.HostedTournamentDays.AnyAsync(d => d.HostedTournamentId == id && d.Date == req.Date && d.Id != dayId, ct))
            return Conflict("Another day already covers that date.");
        day.Date = req.Date;
        day.StartTime = req.StartTime;
        day.EndTime = req.EndTime;
        day.Notes = TrimOrNull(req.Notes);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/days/{dayId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> DeleteDay(int id, int dayId, CancellationToken ct)
    {
        var day = await _db.HostedTournamentDays.FirstOrDefaultAsync(d => d.Id == dayId && d.HostedTournamentId == id, ct);
        if (day is null) return NotFound();
        _db.HostedTournamentDays.Remove(day);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Brackets — sub-groups inside a tier
    // ------------------------------------------------------------

    [HttpPut("{id:int}/tiers/{tierId:int}/flags")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateTierFlags(
        int id, int tierId, [FromBody] UpdateTierFlagsRequest req, CancellationToken ct)
    {
        var tier = await _db.HostedTournamentTiers.FirstOrDefaultAsync(t => t.Id == tierId && t.HostedTournamentId == id, ct);
        if (tier is null) return NotFound();
        tier.CrossBracketPlay = req.CrossBracketPlay;
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpPost("{id:int}/tiers/{tierId:int}/brackets")]
    public async Task<ActionResult<HostedTournamentDto>> AddBracket(
        int id, int tierId, [FromBody] SaveHostedTournamentBracketRequest req, CancellationToken ct)
    {
        var tier = await _db.HostedTournamentTiers.FirstOrDefaultAsync(t => t.Id == tierId && t.HostedTournamentId == id, ct);
        if (tier is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Bracket name is required.");
        _db.HostedTournamentBrackets.Add(new HostedTournamentBracket
        {
            TierId = tierId,
            Name = req.Name.Trim(),
            SortOrder = req.SortOrder,
            Notes = TrimOrNull(req.Notes),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpPut("{id:int}/tiers/{tierId:int}/brackets/{bracketId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateBracket(
        int id, int tierId, int bracketId,
        [FromBody] SaveHostedTournamentBracketRequest req, CancellationToken ct)
    {
        var bracket = await _db.HostedTournamentBrackets
            .Include(x => x.Tier)
            .FirstOrDefaultAsync(x => x.Id == bracketId && x.TierId == tierId && x.Tier!.HostedTournamentId == id, ct);
        if (bracket is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Bracket name is required.");
        bracket.Name = req.Name.Trim();
        bracket.SortOrder = req.SortOrder;
        bracket.Notes = TrimOrNull(req.Notes);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/tiers/{tierId:int}/brackets/{bracketId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> DeleteBracket(
        int id, int tierId, int bracketId, CancellationToken ct)
    {
        var bracket = await _db.HostedTournamentBrackets
            .Include(x => x.Tier)
            .FirstOrDefaultAsync(x => x.Id == bracketId && x.TierId == tierId && x.Tier!.HostedTournamentId == id, ct);
        if (bracket is null) return NotFound();
        // Null out BracketId on teams that were in this bracket (ClientSetNull) so they don't
        // silently vanish from the roster on cascade.
        var members = await _db.HostedTournamentTeams.Where(m => m.BracketId == bracketId).ToListAsync(ct);
        foreach (var m in members) m.BracketId = null;
        _db.HostedTournamentBrackets.Remove(bracket);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Event fields — playing surfaces the schedule can slot matches into
    // ------------------------------------------------------------

    [HttpPost("{id:int}/fields")]
    public async Task<ActionResult<HostedTournamentDto>> AddField(
        int id, [FromBody] SaveHostedTournamentFieldRequest req, CancellationToken ct)
    {
        if (!await _db.HostedTournaments.AnyAsync(t => t.Id == id, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Field name is required.");
        if (req.VenueFieldId is int vf && !await _db.VenueFields.AnyAsync(v => v.Id == vf, ct))
            return BadRequest("Venue field not found.");
        _db.HostedTournamentFields.Add(new HostedTournamentField
        {
            HostedTournamentId = id,
            VenueFieldId = req.VenueFieldId,
            Name = req.Name.Trim(),
            SortOrder = req.SortOrder,
            Notes = TrimOrNull(req.Notes),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpPut("{id:int}/fields/{fieldId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateField(
        int id, int fieldId, [FromBody] SaveHostedTournamentFieldRequest req, CancellationToken ct)
    {
        var field = await _db.HostedTournamentFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.HostedTournamentId == id, ct);
        if (field is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Field name is required.");
        field.Name = req.Name.Trim();
        field.VenueFieldId = req.VenueFieldId;
        field.SortOrder = req.SortOrder;
        field.Notes = TrimOrNull(req.Notes);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/fields/{fieldId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> DeleteField(int id, int fieldId, CancellationToken ct)
    {
        var field = await _db.HostedTournamentFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.HostedTournamentId == id, ct);
        if (field is null) return NotFound();
        // Matches on this field get FieldId nulled; they stay on the schedule as "no field"
        // so the admin can re-slot them.
        var matches = await _db.HostedTournamentMatches.Where(m => m.FieldId == fieldId).ToListAsync(ct);
        foreach (var m in matches) m.FieldId = null;
        _db.HostedTournamentFields.Remove(field);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Schedule generator + email + slug
    // ------------------------------------------------------------

    /// <summary>Generate a round-robin schedule from current tiers/brackets/teams and slot the
    /// matches into the available fields + day windows. Uses a greedy scheduler: order all
    /// candidate matches by tier/bracket, then walk day-slot × field-slot pairs assigning the
    /// next match that doesn't conflict with a team already playing that slot. Matches that
    /// don't fit come back as-scheduled=null so the admin can extend a day or add a field.</summary>
    [HttpPost("{id:int}/generate-schedule")]
    public async Task<ActionResult<HostedTournamentDto>> GenerateSchedule(
        int id, [FromBody] GenerateScheduleRequest? req, CancellationToken ct)
    {
        var tournament = await LoadTournamentQuery().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament is null) return NotFound();
        if (tournament.Fields.Count == 0) return BadRequest("Add at least one field before generating a schedule.");
        if (tournament.Days.Count == 0) return BadRequest("Add at least one day with a time range before generating a schedule.");

        if (req?.ReplaceExisting ?? true)
        {
            var existing = await _db.HostedTournamentMatches.Where(m => m.HostedTournamentId == id).ToListAsync(ct);
            _db.HostedTournamentMatches.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }

        // Derive match window from the tournament's half/halftime settings so the admin can tune
        // scheduling by tweaking those alone. Fall back to the legacy MatchDurationMinutes when
        // halves are zero (older events that predate the timing fields). MinutesBetweenGames
        // widens the slot cursor between back-to-back matches on the same field.
        var halfBased = tournament.HalfMinutes > 0
            ? (tournament.HalfMinutes * 2) + Math.Max(0, tournament.HalftimeMinutes)
            : 0;
        var duration = Math.Max(15, halfBased > 0 ? halfBased : tournament.MatchDurationMinutes);
        var gap = Math.Max(0, tournament.MinutesBetweenGames);
        var slotStep = duration + gap;
        var candidates = new List<(int TierId, int TeamAId, int TeamBId)>();
        foreach (var tier in tournament.Tiers)
        {
            var brackets = tier.Brackets.OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToList();
            if (brackets.Count == 0) continue;
            var teamsByBracket = brackets.ToDictionary(
                b => b.Id,
                b => tournament.Teams.Where(t => t.BracketId == b.Id).ToList());
            if (tier.CrossBracketPlay)
            {
                // Every pair across DIFFERENT brackets in this tier.
                for (var i = 0; i < brackets.Count; i++)
                for (var j = i + 1; j < brackets.Count; j++)
                    foreach (var a in teamsByBracket[brackets[i].Id])
                    foreach (var b in teamsByBracket[brackets[j].Id])
                        candidates.Add((tier.Id, a.Id, b.Id));
            }
            else
            {
                // Round-robin within each bracket.
                foreach (var br in brackets)
                {
                    var teams = teamsByBracket[br.Id];
                    for (var i = 0; i < teams.Count; i++)
                    for (var j = i + 1; j < teams.Count; j++)
                        candidates.Add((tier.Id, teams[i].Id, teams[j].Id));
                }
            }
        }

        // Slot cursor: iterate day → time slot → field, dropping in the next candidate whose
        // teams aren't already busy in that slot. Same team can't play twice at the same time
        // across fields; two fields CAN run parallel matches with different teams.
        var scheduled = new List<HostedTournamentMatch>();
        var days = tournament.Days.OrderBy(d => d.Date).ToList();
        var fields = tournament.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();
        var remaining = new List<(int TierId, int TeamAId, int TeamBId)>(candidates);
        var now = DateTime.UtcNow;

        foreach (var day in days)
        {
            var start = day.StartTime ?? new TimeOnly(9, 0);
            var end = day.EndTime ?? start.AddMinutes(slotStep * Math.Max(1, remaining.Count));
            for (var slot = start; slot.AddMinutes(duration) <= end && remaining.Count > 0; slot = slot.AddMinutes(slotStep))
            {
                var busyThisSlot = new HashSet<int>();
                foreach (var field in fields)
                {
                    if (remaining.Count == 0) break;
                    var idx = remaining.FindIndex(c => !busyThisSlot.Contains(c.TeamAId) && !busyThisSlot.Contains(c.TeamBId));
                    if (idx < 0) break;
                    var next = remaining[idx];
                    remaining.RemoveAt(idx);
                    busyThisSlot.Add(next.TeamAId);
                    busyThisSlot.Add(next.TeamBId);
                    scheduled.Add(new HostedTournamentMatch
                    {
                        HostedTournamentId = id,
                        TierId = next.TierId,
                        TeamAId = next.TeamAId,
                        TeamBId = next.TeamBId,
                        FieldId = field.Id,
                        DayId = day.Id,
                        StartTime = slot,
                        DurationMinutes = duration,
                        CreatedAt = now,
                    });
                }
            }
        }

        // Any candidates that didn't fit still get persisted as unscheduled rows so the admin
        // sees the total match count and can manually place them (extend a day, add a field).
        foreach (var leftover in remaining)
        {
            scheduled.Add(new HostedTournamentMatch
            {
                HostedTournamentId = id,
                TierId = leftover.TierId,
                TeamAId = leftover.TeamAId,
                TeamBId = leftover.TeamBId,
                DurationMinutes = duration,
                CreatedAt = now,
                Notes = "Unscheduled — extend a day or add a field.",
            });
        }

        // Also generate the four projected knockout matches per tier that has exactly two
        // brackets — SF1, SF2, Consolation, Final. Teams start null (admin fills them in
        // once seeds are settled) but the rows exist so the admin can enter scores from the
        // Schedule table like any other match. Only add them if none exist yet for that
        // tier + slot so re-generating the group stage doesn't wipe playoff results.
        foreach (var tier in tournament.Tiers)
        {
            var bracketList = tier.Brackets.OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToList();
            if (bracketList.Count != 2) continue;
            foreach (var slot in new[] {
                PlayoffSlot.SemifinalOne,
                PlayoffSlot.SemifinalTwo,
                PlayoffSlot.Consolation,
                PlayoffSlot.Final })
            {
                var alreadyExists = tournament.Matches.Any(m => m.TierId == tier.Id && m.PlayoffSlot == slot)
                    || scheduled.Any(m => m.TierId == tier.Id && m.PlayoffSlot == slot);
                if (alreadyExists) continue;
                scheduled.Add(new HostedTournamentMatch
                {
                    HostedTournamentId = id,
                    TierId = tier.Id,
                    PlayoffSlot = slot,
                    DurationMinutes = duration,
                    CreatedAt = now,
                });
            }
        }

        _db.HostedTournamentMatches.AddRange(scheduled);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Admin edit of a single scheduled match — swap teams, move day / field / start
    /// time, tweak duration or notes. Validates FKs belong to the same event so a stray ID
    /// can't cross-link. Every field is optional; the row keeps its prior value when omitted.
    /// To unschedule a match (send it back to the "orphan" pool) explicitly pass null for
    /// DayId / FieldId / StartTime.</summary>
    [HttpPut("{id:int}/matches/{matchId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> UpdateMatch(
        int id, int matchId, [FromBody] SaveHostedTournamentMatchRequest req, CancellationToken ct)
    {
        var match = await _db.HostedTournamentMatches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.HostedTournamentId == id, ct);
        if (match is null) return NotFound();

        // Validate every FK the admin is trying to set actually belongs to this tournament.
        if (req.TierId is int tid && !await _db.HostedTournamentTiers.AnyAsync(x => x.Id == tid && x.HostedTournamentId == id, ct))
            return BadRequest("Tier not found on this tournament.");
        if (req.TeamAId is int aid && !await _db.HostedTournamentTeams.AnyAsync(x => x.Id == aid && x.HostedTournamentId == id, ct))
            return BadRequest("Team A not found on this tournament.");
        if (req.TeamBId is int bid && !await _db.HostedTournamentTeams.AnyAsync(x => x.Id == bid && x.HostedTournamentId == id, ct))
            return BadRequest("Team B not found on this tournament.");
        if (req.FieldId is int fid && !await _db.HostedTournamentFields.AnyAsync(x => x.Id == fid && x.HostedTournamentId == id, ct))
            return BadRequest("Field not found on this tournament.");
        if (req.DayId is int did && !await _db.HostedTournamentDays.AnyAsync(x => x.Id == did && x.HostedTournamentId == id, ct))
            return BadRequest("Day not found on this tournament.");
        if (req.TeamAId.HasValue && req.TeamBId.HasValue && req.TeamAId == req.TeamBId)
            return BadRequest("Team A and Team B must be different.");

        match.TierId = req.TierId;
        match.TeamAId = req.TeamAId;
        match.TeamBId = req.TeamBId;
        match.FieldId = req.FieldId;
        match.DayId = req.DayId;
        match.StartTime = req.StartTime;
        if (req.DurationMinutes is int dur) match.DurationMinutes = dur;
        match.TeamAScore = req.TeamAScore;
        match.TeamBScore = req.TeamBScore;
        match.Notes = TrimOrNull(req.Notes);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Delete a single scheduled match — useful when the admin wants to remove a
    /// leftover "unscheduled" placeholder or drop a match that shouldn't have been generated.</summary>
    [HttpDelete("{id:int}/matches/{matchId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> DeleteMatch(int id, int matchId, CancellationToken ct)
    {
        var match = await _db.HostedTournamentMatches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.HostedTournamentId == id, ct);
        if (match is null) return NotFound();
        _db.HostedTournamentMatches.Remove(match);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    /// <summary>Dry-run render of the schedule email — same composition the send path uses,
    /// but returns the resolved subject / body / recipient list instead of dispatching. Lets
    /// the admin confirm before hitting Send.</summary>
    [HttpPost("{id:int}/preview-schedule-email")]
    public async Task<ActionResult<SchedulePreviewDto>> PreviewScheduleEmail(
        int id, [FromBody] SendScheduleEmailRequest? req, CancellationToken ct)
    {
        var tournament = await LoadTournamentQuery().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament is null) return NotFound();
        if (string.IsNullOrWhiteSpace(tournament.PublicSlug))
        {
            tournament.PublicSlug = await GenerateUniqueSlugAsync(tournament.Name, ct);
            await _db.SaveChangesAsync(ct);
        }
        var (subject, body, link, recipients) = ComposeScheduleEmail(tournament, req);
        string? warning = null;
        if (!_emailSender.IsAvailable) warning = "Email is not configured on this server — sending will fail.";
        else if (recipients.Count == 0) warning = "No invited teams have a head-coach email on file.";
        return Ok(new SchedulePreviewDto(
            Subject: subject,
            Body: body,
            PublicUrl: link,
            Recipients: recipients.Select(r => new SchedulePreviewRecipient(r.Name, r.Email)).ToList(),
            Warning: warning));
    }

    /// <summary>Send the schedule + rules body to every invited team's head-coach email on file.
    /// LVSS teams don't have coach emails in this domain; those coaches receive the event
    /// through the normal messaging flow instead. Skips recipients without an email.</summary>
    [HttpPost("{id:int}/send-schedule-email")]
    public async Task<ActionResult<SendScheduleEmailResult>> SendScheduleEmail(
        int id, [FromBody] SendScheduleEmailRequest req, CancellationToken ct)
    {
        if (!_emailSender.IsAvailable)
            return BadRequest(new SendScheduleEmailResult(0, 0, "Email is not configured on this server."));
        var tournament = await LoadTournamentQuery().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament is null) return NotFound();
        if (string.IsNullOrWhiteSpace(tournament.PublicSlug))
        {
            tournament.PublicSlug = await GenerateUniqueSlugAsync(tournament.Name, ct);
            await _db.SaveChangesAsync(ct);
        }

        var (subject, text, _, recipients) = ComposeScheduleEmail(tournament, req);
        if (recipients.Count == 0)
            return BadRequest(new SendScheduleEmailResult(0, 0, "No invited teams have a head-coach email on file."));

        var sent = 0; var skipped = 0;
        foreach (var r in recipients)
        {
            var res = await _emailSender.SendAsync(r.Email, subject, text, ct);
            if (res.Success) sent++; else skipped++;
        }
        return Ok(new SendScheduleEmailResult(sent, skipped, sent > 0 ? $"Sent to {sent} coach(es)." : "No emails were queued."));
    }

    /// <summary>Shared composition for the preview + send paths so the two never drift.
    /// Assumes tournament is loaded (with Teams.InvitedTeam) and PublicSlug is set.</summary>
    private (string Subject, string Body, string Link, List<(string Name, string Email)> Recipients) ComposeScheduleEmail(
        HostedTournament tournament, SendScheduleEmailRequest? req)
    {
        var link = string.IsNullOrWhiteSpace(_app.PublicBaseUrl)
            ? $"/tournament/{tournament.PublicSlug}"
            : $"{_app.PublicBaseUrl.TrimEnd('/')}/tournament/{tournament.PublicSlug}";
        var subject = string.IsNullOrWhiteSpace(req?.Subject) ? $"{tournament.Name} — Schedule" : req!.Subject!.Trim();
        // Prefer the tournament's dedicated ScheduleEmailBody so admin can send a shorter /
        // friendlier note than the full RulesOfPlay block on the public page. Falls back to
        // RulesOfPlay for events created before the split so existing sends keep the prior copy.
        var stored = !string.IsNullOrWhiteSpace(tournament.ScheduleEmailBody)
            ? tournament.ScheduleEmailBody
            : tournament.RulesOfPlay;
        var body = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(req?.Intro)) body.Append(req!.Intro).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(stored)) body.Append(stored).Append("\n\n");
        body.Append("Schedule + updates: ").Append(link).Append('\n');

        var recipients = tournament.Teams
            .Select(t => t.InvitedTeam)
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t!.HeadCoachEmail))
            .Select(t => (Name: t!.HeadCoachName ?? t.Name, Email: t!.HeadCoachEmail!.Trim()))
            .GroupBy(r => r.Email.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        return (subject, body.ToString(), link, recipients);
    }

    // ------------------------------------------------------------
    // Invited teams catalog
    // ------------------------------------------------------------

    [HttpGet("~/api/admin/invited-teams")]
    public async Task<ActionResult<IEnumerable<InvitedTeamDto>>> ListInvited(CancellationToken ct)
    {
        var rows = await _db.InvitedTeams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        return Ok(rows.Select(ToInvitedDto).ToList());
    }

    [HttpPost("~/api/admin/invited-teams")]
    public async Task<ActionResult<InvitedTeamDto>> CreateInvited(
        [FromBody] SaveInvitedTeamRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        var now = DateTime.UtcNow;
        var t = new InvitedTeam
        {
            Name = req.Name.Trim(),
            HeadCoachName = TrimOrNull(req.HeadCoachName),
            HeadCoachPhone = TrimOrNull(req.HeadCoachPhone),
            HeadCoachEmail = TrimOrNull(req.HeadCoachEmail),
            AgeGroup = TrimOrNull(req.AgeGroup),
            Notes = TrimOrNull(req.Notes),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.InvitedTeams.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(ToInvitedDto(t));
    }

    [HttpPut("~/api/admin/invited-teams/{id:int}")]
    public async Task<ActionResult<InvitedTeamDto>> UpdateInvited(
        int id, [FromBody] SaveInvitedTeamRequest req, CancellationToken ct)
    {
        var t = await _db.InvitedTeams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        t.Name = req.Name.Trim();
        t.HeadCoachName = TrimOrNull(req.HeadCoachName);
        t.HeadCoachPhone = TrimOrNull(req.HeadCoachPhone);
        t.HeadCoachEmail = TrimOrNull(req.HeadCoachEmail);
        t.AgeGroup = TrimOrNull(req.AgeGroup);
        t.Notes = TrimOrNull(req.Notes);
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToInvitedDto(t));
    }

    [HttpDelete("~/api/admin/invited-teams/{id:int}")]
    public async Task<IActionResult> DeleteInvited(int id, CancellationToken ct)
    {
        var t = await _db.InvitedTeams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        // Block delete when the team is on any hosted tournament — force the admin to remove
        // roster rows first so the participation history isn't quietly orphaned.
        var inUse = await _db.HostedTournamentTeams.AnyAsync(r => r.InvitedTeamId == id, ct);
        if (inUse) return Conflict("This invited team is rostered on one or more hosted tournaments — remove it there first.");
        _db.InvitedTeams.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private IQueryable<HostedTournament> LoadTournamentQuery() =>
        _db.HostedTournaments
            .AsNoTracking()
            .Include(t => t.Venue)
            .Include(t => t.Teams).ThenInclude(r => r.LvssTeam)
            .Include(t => t.Teams).ThenInclude(r => r.InvitedTeam)
            .Include(t => t.Teams).ThenInclude(r => r.Tier)
            .Include(t => t.Teams).ThenInclude(r => r.Bracket)
            .Include(t => t.Tiers).ThenInclude(x => x.Brackets)
            .Include(t => t.Days)
            .Include(t => t.Fields).ThenInclude(f => f.VenueField)
            .Include(t => t.Matches).ThenInclude(m => m.TeamA).ThenInclude(t => t!.LvssTeam)
            .Include(t => t.Matches).ThenInclude(m => m.TeamA).ThenInclude(t => t!.InvitedTeam)
            .Include(t => t.Matches).ThenInclude(m => m.TeamB).ThenInclude(t => t!.LvssTeam)
            .Include(t => t.Matches).ThenInclude(m => m.TeamB).ThenInclude(t => t!.InvitedTeam)
            .Include(t => t.Matches).ThenInclude(m => m.Field)
            .Include(t => t.Matches).ThenInclude(m => m.Day)
            .Include(t => t.Matches).ThenInclude(m => m.Tier);

    private async Task<HostedTournamentDto> LoadAndMap(int id, CancellationToken ct)
    {
        var row = await LoadTournamentQuery().FirstAsync(t => t.Id == id, ct);
        return ToDto(row);
    }

    private async Task<string?> ValidateAsync(SaveHostedTournamentRequest req, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
        if (req.StartDate == default) return "Start date is required.";
        if (req.EndDate is DateOnly e && e < req.StartDate) return "End date can't be before the start date.";
        if (req.VenueId is int v && !await _db.Venues.AnyAsync(x => x.Id == v, ct)) return "Venue not found.";
        _ = id; // reserved for future name-uniqueness checks
        return null;
    }

    private static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static HostedTournamentDto ToDto(HostedTournament t) =>
        new(
            t.Id, t.Name, t.Kind, t.StartDate, t.EndDate,
            t.VenueId, t.Venue?.Name, t.Venue?.Address,
            t.Location, t.CostPerTeam, t.Notes,
            t.RulesOfPlay, t.ScheduleEmailBody, t.PublicSlug, t.MatchDurationMinutes,
            t.HalfMinutes, t.HalftimeMinutes, t.MinutesBetweenGames,
            t.CreatedAt, t.UpdatedAt,
            t.Teams
                .OrderBy(r => r.LvssTeam?.Name ?? r.InvitedTeam?.Name ?? string.Empty)
                .Select(r => new HostedTournamentTeamDto(
                    r.Id,
                    r.LvssTeamId, r.LvssTeam?.Name,
                    r.InvitedTeamId, r.InvitedTeam?.Name,
                    r.InvitedTeam?.AgeGroup,
                    r.InvitedTeam?.HeadCoachName,
                    r.InvitedTeam?.HeadCoachPhone,
                    r.InvitedTeam?.HeadCoachEmail,
                    r.Notes,
                    r.TierId, r.Tier?.Name,
                    r.BracketId, r.Bracket?.Name,
                    r.Paid, r.PaidAt, r.PaymentMethod, r.PaymentReference,
                    r.CreatedAt))
                .ToList(),
            t.Tiers
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new HostedTournamentTierDto(
                    x.Id, x.Name, x.SortOrder, x.Notes, x.CrossBracketPlay, x.CreatedAt,
                    x.Brackets
                        .OrderBy(br => br.SortOrder).ThenBy(br => br.Name)
                        .Select(br => new HostedTournamentBracketDto(br.Id, br.TierId, br.Name, br.SortOrder, br.Notes, br.CreatedAt))
                        .ToList()))
                .ToList(),
            t.Days
                .OrderBy(x => x.Date)
                .Select(x => new HostedTournamentDayDto(x.Id, x.Date, x.StartTime, x.EndTime, x.Notes, x.CreatedAt))
                .ToList(),
            t.Fields
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
                .Select(f => new HostedTournamentFieldDto(f.Id, f.VenueFieldId, f.Name, f.SortOrder, f.Notes, f.CreatedAt))
                .ToList(),
            t.Matches
                // Playoffs land after every group-stage row so the schedule ends with
                // SF1 → SF2 → Consolation → Final regardless of when the admin scheduled
                // them. Within each bucket, sort by day/time so the group stage keeps
                // chronological order.
                .OrderBy(m => m.PlayoffSlot.HasValue ? 1 : 0)
                .ThenBy(m => m.PlayoffSlot)
                .ThenBy(m => m.Day?.Date).ThenBy(m => m.StartTime).ThenBy(m => m.Field?.SortOrder ?? 0)
                .Select(m => new HostedTournamentMatchDto(
                    m.Id,
                    m.TierId, m.Tier?.Name,
                    m.TeamAId, TeamLabel(m.TeamA),
                    m.TeamBId, TeamLabel(m.TeamB),
                    m.FieldId, m.Field?.Name,
                    m.DayId, m.Day?.Date,
                    m.StartTime, m.DurationMinutes,
                    m.TeamAScore, m.TeamBScore,
                    m.Notes,
                    m.PlayoffSlot))
                .ToList());

    private static string? TeamLabel(HostedTournamentTeam? t) =>
        t?.LvssTeam?.Name ?? t?.InvitedTeam?.Name;

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var basePart = Slugify(name);
        if (string.IsNullOrEmpty(basePart)) basePart = "event";
        // Suffix with a short random tail so the URL isn't guessable from the event name
        // (guards against a stranger typing /tournament/spring-cup and finding a schedule
        // before we're ready to publish it).
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = basePart + "-" + Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
            if (!await _db.HostedTournaments.AnyAsync(x => x.PublicSlug == candidate, ct))
                return candidate;
        }
        // Absurd fallback if we happened to collide 10 times in a row.
        return basePart + "-" + Guid.NewGuid().ToString("N")[..12].ToLowerInvariant();
    }

    private static string Slugify(string s)
    {
        var lower = (s ?? string.Empty).Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        var lastDash = false;
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastDash = false; }
            else if (!lastDash) { sb.Append('-'); lastDash = true; }
        }
        return sb.ToString().Trim('-');
    }

    private static InvitedTeamDto ToInvitedDto(InvitedTeam t) =>
        new(t.Id, t.Name, t.HeadCoachName, t.HeadCoachPhone, t.HeadCoachEmail,
            t.AgeGroup, t.Notes, t.CreatedAt, t.UpdatedAt);
}
