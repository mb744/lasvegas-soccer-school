using System.Text.Json;
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
/// Admin-only chat/broadcast surface. Distinct from <see cref="OutreachController"/> which is
/// strictly about signup-link invites: this one sends free-form SMS or WhatsApp text to individuals,
/// curated groups, or dynamic groups, and manages true group conversations via Twilio Conversations.
/// </summary>
[ApiController]
[Route("api/messaging")]
[Authorize(Roles = Roles.Admin)]
public class MessagingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMessageSender _sender;
    private readonly IRecipientResolver _resolver;
    private readonly IConversationService _conversations;
    private readonly IPhraseTranslator _translator;
    private readonly TwilioOptions _twilio;

    public MessagingController(
        AppDbContext db,
        IMessageSender sender,
        IRecipientResolver resolver,
        IConversationService conversations,
        IPhraseTranslator translator,
        IOptions<TwilioOptions> twilio)
    {
        _db = db;
        _sender = sender;
        _resolver = resolver;
        _conversations = conversations;
        _translator = translator;
        _twilio = twilio.Value;
    }

    // --- Capabilities probe ---

    [HttpGet("config")]
    public ActionResult<MessagingConfigDto> Config() =>
        Ok(new MessagingConfigDto(
            Sms: _twilio.IsSmsConfigured,
            WhatsApp: _twilio.IsWhatsAppConfigured,
            Conversations: _twilio.IsSmsConfigured || _twilio.IsWhatsAppConfigured));

    // --- Curated groups ---

    [HttpGet("groups")]
    public async Task<ActionResult<object>> ListGroups(CancellationToken ct)
    {
        var curated = await _db.MessageGroups
            .OrderBy(g => g.Name)
            .Select(g => new MessageGroupSummary(
                g.Id, g.Name, g.Description, g.Language, g.Members.Count, g.CreatedAt))
            .ToListAsync(ct);
        var dynamicGroups = (await _resolver.ListDynamicGroupsAsync(ct))
            .Select(d => new DynamicGroupDto(d.Key, d.Label, d.Count))
            .ToList();
        return Ok(new { curated, dynamic = dynamicGroups });
    }

    [HttpGet("groups/{id:int}")]
    public async Task<ActionResult<MessageGroupDetail>> GetGroup(int id, CancellationToken ct)
    {
        var g = await _db.MessageGroups
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return NotFound();
        return Ok(new MessageGroupDetail(
            g.Id, g.Name, g.Description, g.Language, g.CreatedAt,
            g.Members.Select(m => new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Language, m.ParentAccountId)).ToList()));
    }

    [HttpPost("groups")]
    public async Task<ActionResult<MessageGroupSummary>> CreateGroup(
        [FromBody] SaveMessageGroupRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (await _db.MessageGroups.AnyAsync(g => g.Name == name, ct))
            return Conflict($"A group named '{name}' already exists.");
        var g = new MessageGroup
        {
            Name = name,
            Description = request.Description?.Trim(),
            Language = request.Language
        };
        _db.MessageGroups.Add(g);
        await _db.SaveChangesAsync(ct);
        return Ok(new MessageGroupSummary(g.Id, g.Name, g.Description, g.Language, 0, g.CreatedAt));
    }

    [HttpPut("groups/{id:int}")]
    public async Task<ActionResult<MessageGroupSummary>> UpdateGroup(
        int id, [FromBody] SaveMessageGroupRequest request, CancellationToken ct)
    {
        var g = await _db.MessageGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return NotFound();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (await _db.MessageGroups.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict($"A group named '{name}' already exists.");
        g.Name = name;
        g.Description = request.Description?.Trim();
        g.Language = request.Language;
        await _db.SaveChangesAsync(ct);
        return Ok(new MessageGroupSummary(g.Id, g.Name, g.Description, g.Language, g.Members.Count, g.CreatedAt));
    }

    [HttpDelete("groups/{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct)
    {
        var g = await _db.MessageGroups.FindAsync(new object?[] { id }, ct);
        if (g is null) return NotFound();
        _db.MessageGroups.Remove(g);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("groups/{id:int}/members")]
    public async Task<ActionResult<MessageGroupMemberDto>> AddMember(
        int id, [FromBody] AddMessageGroupMemberRequest request, CancellationToken ct)
    {
        var g = await _db.MessageGroups.FindAsync(new object?[] { id }, ct);
        if (g is null) return NotFound();
        var phone = request.Phone.Trim();
        if (string.IsNullOrWhiteSpace(phone)) return BadRequest("Phone is required.");
        if (await _db.MessageGroupMembers.AnyAsync(m => m.MessageGroupId == id && m.Phone == phone, ct))
            return Conflict("This phone is already in the group.");
        var m = new MessageGroupMember
        {
            MessageGroupId = id,
            Name = request.Name?.Trim(),
            Phone = phone,
            Language = request.Language ?? g.Language,
            ParentAccountId = request.ParentAccountId
        };
        _db.MessageGroupMembers.Add(m);
        await _db.SaveChangesAsync(ct);
        return Ok(new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Language, m.ParentAccountId));
    }

    [HttpPatch("groups/{id:int}/members/{memberId:int}/language")]
    public async Task<ActionResult<MessageGroupMemberDto>> UpdateMemberLanguage(
        int id, int memberId, [FromBody] UpdateMessageGroupMemberLanguageRequest request, CancellationToken ct)
    {
        var m = await _db.MessageGroupMembers
            .FirstOrDefaultAsync(x => x.MessageGroupId == id && x.Id == memberId, ct);
        if (m is null) return NotFound();
        m.Language = request.Language;
        await _db.SaveChangesAsync(ct);
        return Ok(new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Language, m.ParentAccountId));
    }

    [HttpDelete("groups/{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId, CancellationToken ct)
    {
        var m = await _db.MessageGroupMembers
            .FirstOrDefaultAsync(x => x.MessageGroupId == id && x.Id == memberId, ct);
        if (m is null) return NotFound();
        _db.MessageGroupMembers.Remove(m);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Bulk-add parents from the active season into the group. Skips parents already in the group
    /// and parents missing a phone number. Per-member language defaults to each parent's stored
    /// preference (<see cref="ParentAccount.Language"/>) so the bulk import gets bilingual routing
    /// right out of the box.
    /// </summary>
    [HttpPost("groups/{id:int}/import-active-season")]
    public async Task<ActionResult<MessageGroupDetail>> ImportActiveSeason(int id, CancellationToken ct)
    {
        var g = await _db.MessageGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return NotFound();

        var resolved = await _resolver.ResolveAsync(
            new RecipientTarget(RecipientTargetKind.DynamicGroup, DynamicGroupKey: RecipientResolver.DynamicActiveSeasonParents),
            ct);
        // Look up parent language preferences in one query so the import knows EN vs ES per member.
        var parentIds = resolved.Recipients.Select(r => r.ParentAccountId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var parentLangs = await _db.ParentAccounts
            .Where(p => parentIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Language, ct);

        var existing = g.Members.Select(m => m.Phone).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in resolved.Recipients)
        {
            if (existing.Contains(r.Phone)) continue;
            var memberLang = r.ParentAccountId is int pid && parentLangs.TryGetValue(pid, out var lang)
                ? lang
                : g.Language;
            g.Members.Add(new MessageGroupMember
            {
                MessageGroupId = g.Id,
                Name = r.Name,
                Phone = r.Phone,
                Language = memberLang,
                ParentAccountId = r.ParentAccountId
            });
            existing.Add(r.Phone);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new MessageGroupDetail(
            g.Id, g.Name, g.Description, g.Language, g.CreatedAt,
            g.Members.Select(m => new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Language, m.ParentAccountId)).ToList()));
    }

    // --- Broadcasts (fan-out) ---

    [HttpPost("broadcasts")]
    public async Task<ActionResult<BroadcastDetail>> CreateBroadcast(
        [FromBody] CreateBroadcastRequest request, CancellationToken ct)
    {
        if (!_sender.IsAvailable(request.Channel))
            return BadRequest($"{request.Channel} not configured on this server.");

        // Two send modes: free-form (with optional bilingual bodies) or WhatsApp Content template.
        var isTemplate = request.WhatsAppTemplateId.HasValue;
        WhatsAppTemplate? template = null;
        Dictionary<string, string> templateVars = new();
        var bodyEn = request.BodyEn?.Trim();
        var bodyEs = request.BodyEs?.Trim();

        if (isTemplate)
        {
            if (request.Channel != MessageChannel.WhatsApp)
                return BadRequest("Templates can only be used on the WhatsApp channel.");
            template = await _db.WhatsAppTemplates
                .Include(t => t.Variables)
                .FirstOrDefaultAsync(t => t.Id == request.WhatsAppTemplateId, ct);
            if (template is null) return BadRequest("WhatsApp template not found.");
            templateVars = (request.TemplateVariables ?? new())
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);
            var missing = template.Variables
                .Select(v => v.Position.ToString())
                .Where(pos => !templateVars.ContainsKey(pos) || string.IsNullOrWhiteSpace(templateVars[pos]))
                .ToList();
            if (missing.Count > 0)
                return BadRequest($"Template variables missing: {string.Join(", ", missing)}.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyEs))
                return BadRequest("Either BodyEn, BodyEs, or WhatsAppTemplateId is required.");
        }

        var target = MapTarget(request.Target);
        var resolved = await _resolver.ResolveAsync(target, ct);
        if (resolved.Recipients.Count == 0)
            return BadRequest("No recipients matched the selected target.");

        var broadcast = new Broadcast
        {
            Channel = request.Channel,
            BodyEn = bodyEn,
            BodyEs = bodyEs,
            TargetLabel = resolved.Label,
            WhatsAppTemplateId = template?.Id,
            TemplateVariablesJson = isTemplate ? JsonSerializer.Serialize(templateVars) : null
        };
        foreach (var r in resolved.Recipients)
        {
            // Recipient language: from the source (curated group) if present, else from the
            // broadcast's default (per admin spec: English unless overridden).
            var lang = r.Language ?? request.DefaultLanguage;
            broadcast.Recipients.Add(new BroadcastRecipient
            {
                Name = r.Name,
                Phone = r.Phone,
                Language = lang,
                Status = MessageDeliveryStatus.Pending
            });
        }
        _db.Broadcasts.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        // Synchronous fan-out. Each recipient gets the body matching their resolved language;
        // if that body is empty we fall back to whichever is set so we don't silently skip them.
        foreach (var recipient in broadcast.Recipients)
        {
            MessageSendResult send;
            if (isTemplate)
            {
                send = await _sender.SendTemplateAsync(recipient.Phone, template!.ContentSid, templateVars, ct);
            }
            else
            {
                var body = recipient.Language == Language.Spanish ? (bodyEs ?? bodyEn) : (bodyEn ?? bodyEs);
                send = await _sender.SendAsync(request.Channel, recipient.Phone, body ?? string.Empty, ct);
            }
            recipient.TwilioSid = send.TwilioSid;
            recipient.Status = send.Status;
            recipient.StatusMessage = send.Message;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(broadcast));
    }


    [HttpGet("broadcasts")]
    public async Task<ActionResult<IEnumerable<BroadcastSummary>>> ListBroadcasts(CancellationToken ct)
    {
        var items = await _db.Broadcasts
            .OrderByDescending(b => b.CreatedAt)
            .Take(200)
            .Select(b => new BroadcastSummary(
                b.Id,
                b.Channel,
                b.BodyEn,
                b.BodyEs,
                b.TargetLabel,
                b.CreatedAt,
                b.Recipients.Count,
                b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Queued || r.Status == MessageDeliveryStatus.Sent || r.Status == MessageDeliveryStatus.Pending),
                b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Delivered),
                b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Failed || r.Status == MessageDeliveryStatus.Undelivered)))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("broadcasts/{id:int}")]
    public async Task<ActionResult<BroadcastDetail>> GetBroadcast(int id, CancellationToken ct)
    {
        var b = await _db.Broadcasts
            .Include(x => x.Recipients)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return NotFound();
        return Ok(ToDetail(b));
    }

    // --- Conversations (true group chat) ---

    [HttpPost("conversations")]
    public async Task<ActionResult<GroupConversationDetail>> CreateConversation(
        [FromBody] CreateGroupConversationRequest request, CancellationToken ct)
    {
        if (!_conversations.IsAvailable(request.Channel))
            return BadRequest($"{request.Channel} not configured on this server.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");

        // Build participant list from either explicit input or a target descriptor.
        var inputs = new List<ConversationParticipantInput>();
        if (request.Participants is { Count: > 0 })
        {
            inputs.AddRange(request.Participants
                .Where(p => !string.IsNullOrWhiteSpace(p.Phone))
                .Select(p => new ConversationParticipantInput(p.Phone.Trim(), p.Name?.Trim())));
        }
        if (request.Target is not null)
        {
            var resolved = await _resolver.ResolveAsync(MapTarget(request.Target), ct);
            inputs.AddRange(resolved.Recipients.Select(r => new ConversationParticipantInput(r.Phone, r.Name)));
        }
        // De-dup on phone — same person added via both paths should only join once.
        inputs = inputs
            .GroupBy(p => p.Phone, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        if (inputs.Count == 0)
            return BadRequest("At least one participant is required.");

        var create = await _conversations.CreateAsync(request.Title.Trim(), request.Channel, inputs, ct);
        if (!create.Success || create.ConversationSid is null)
            return UnprocessableEntity(create.Message ?? "Failed to create conversation.");

        var convo = new GroupConversation
        {
            Title = request.Title.Trim(),
            Channel = request.Channel,
            TwilioConversationSid = create.ConversationSid
        };
        foreach (var added in create.Participants.Where(p => p.ParticipantSid is not null))
        {
            convo.Participants.Add(new GroupConversationParticipant
            {
                Name = added.Name,
                Phone = added.Phone,
                TwilioParticipantSid = added.ParticipantSid
            });
        }
        _db.GroupConversations.Add(convo);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(convo));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IEnumerable<GroupConversationSummary>>> ListConversations(CancellationToken ct)
    {
        var items = await _db.GroupConversations
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new GroupConversationSummary(
                c.Id, c.Title, c.Channel, c.TwilioConversationSid, c.Participants.Count, c.CreatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("conversations/{id:int}")]
    public async Task<ActionResult<GroupConversationDetail>> GetConversation(int id, CancellationToken ct)
    {
        var c = await _db.GroupConversations
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        return Ok(ToDetail(c));
    }

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<IActionResult> SendToConversation(
        int id, [FromBody] SendGroupConversationRequest request, CancellationToken ct)
    {
        var c = await _db.GroupConversations.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Body)) return BadRequest("Body is required.");
        var send = await _conversations.SendMessageAsync(c.TwilioConversationSid, request.Body.Trim(), ct);
        return send.Success ? Ok(new { messageSid = send.MessageSid }) : UnprocessableEntity(send.Message);
    }

    [HttpDelete("conversations/{id:int}/participants/{participantId:int}")]
    public async Task<IActionResult> RemoveParticipant(int id, int participantId, CancellationToken ct)
    {
        var c = await _db.GroupConversations
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var p = c.Participants.FirstOrDefault(x => x.Id == participantId);
        if (p is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(p.TwilioParticipantSid))
        {
            await _conversations.RemoveParticipantAsync(c.TwilioConversationSid, p.TwilioParticipantSid, ct);
        }
        _db.GroupConversationParticipants.Remove(p);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("conversations/{id:int}")]
    public async Task<IActionResult> DeleteConversation(int id, CancellationToken ct)
    {
        var c = await _db.GroupConversations.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(c.TwilioConversationSid))
        {
            await _conversations.DeleteConversationAsync(c.TwilioConversationSid, ct);
        }
        _db.GroupConversations.Remove(c);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- WhatsApp templates ---

    [HttpGet("whatsapp-templates")]
    public async Task<ActionResult<IEnumerable<WhatsAppTemplateDto>>> ListTemplates(CancellationToken ct)
    {
        var items = await _db.WhatsAppTemplates
            .Include(t => t.Variables)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(items.Select(ToDto));
    }

    [HttpPost("whatsapp-templates")]
    public async Task<ActionResult<WhatsAppTemplateDto>> CreateTemplate(
        [FromBody] SaveWhatsAppTemplateRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var sid = request.ContentSid.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(sid) || !sid.StartsWith("HX"))
            return BadRequest("ContentSid must start with HX.");
        if (await _db.WhatsAppTemplates.AnyAsync(t => t.Name == name, ct))
            return Conflict($"A template named '{name}' already exists.");

        var template = new WhatsAppTemplate
        {
            Name = name,
            ContentSid = sid,
            Language = request.Language,
            Description = request.Description?.Trim(),
            PreviewText = request.PreviewText?.Trim(),
            Variables = MapVariables(request.Variables)
        };
        _db.WhatsAppTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    [HttpPut("whatsapp-templates/{id:int}")]
    public async Task<ActionResult<WhatsAppTemplateDto>> UpdateTemplate(
        int id, [FromBody] SaveWhatsAppTemplateRequest request, CancellationToken ct)
    {
        var template = await _db.WhatsAppTemplates
            .Include(t => t.Variables)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        var name = request.Name.Trim();
        var sid = request.ContentSid.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(sid) || !sid.StartsWith("HX"))
            return BadRequest("ContentSid must start with HX.");
        if (await _db.WhatsAppTemplates.AnyAsync(t => t.Name == name && t.Id != id, ct))
            return Conflict($"A template named '{name}' already exists.");

        template.Name = name;
        template.ContentSid = sid;
        template.Language = request.Language;
        template.Description = request.Description?.Trim();
        template.PreviewText = request.PreviewText?.Trim();
        // Wipe existing variables and replace — simpler than diffing for a small list.
        _db.WhatsAppTemplateVariables.RemoveRange(template.Variables);
        template.Variables = MapVariables(request.Variables);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    [HttpDelete("whatsapp-templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        var template = await _db.WhatsAppTemplates.FindAsync(new object?[] { id }, ct);
        if (template is null) return NotFound();
        _db.WhatsAppTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Phrase translation dictionary ---

    [HttpGet("translations")]
    public async Task<ActionResult<IEnumerable<PhraseTranslationDto>>> ListTranslations(CancellationToken ct)
    {
        var items = await _db.PhraseTranslations
            .OrderBy(p => p.English)
            .Select(p => new PhraseTranslationDto(p.Id, p.English, p.Spanish, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("translations")]
    public async Task<ActionResult<PhraseTranslationDto>> CreateTranslation(
        [FromBody] SavePhraseTranslationRequest request, CancellationToken ct)
    {
        var en = request.English.Trim();
        var es = request.Spanish.Trim();
        if (string.IsNullOrWhiteSpace(en) || string.IsNullOrWhiteSpace(es))
            return BadRequest("Both English and Spanish phrases are required.");
        if (await _db.PhraseTranslations.AnyAsync(p => p.English == en, ct))
            return Conflict($"A translation for '{en}' already exists.");
        var p = new PhraseTranslation { English = en, Spanish = es };
        _db.PhraseTranslations.Add(p);
        await _db.SaveChangesAsync(ct);
        return Ok(new PhraseTranslationDto(p.Id, p.English, p.Spanish, p.CreatedAt, p.UpdatedAt));
    }

    [HttpPut("translations/{id:int}")]
    public async Task<ActionResult<PhraseTranslationDto>> UpdateTranslation(
        int id, [FromBody] SavePhraseTranslationRequest request, CancellationToken ct)
    {
        var p = await _db.PhraseTranslations.FindAsync(new object?[] { id }, ct);
        if (p is null) return NotFound();
        var en = request.English.Trim();
        var es = request.Spanish.Trim();
        if (string.IsNullOrWhiteSpace(en) || string.IsNullOrWhiteSpace(es))
            return BadRequest("Both English and Spanish phrases are required.");
        if (await _db.PhraseTranslations.AnyAsync(x => x.English == en && x.Id != id, ct))
            return Conflict($"A translation for '{en}' already exists.");
        p.English = en;
        p.Spanish = es;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new PhraseTranslationDto(p.Id, p.English, p.Spanish, p.CreatedAt, p.UpdatedAt));
    }

    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslation(int id, CancellationToken ct)
    {
        var p = await _db.PhraseTranslations.FindAsync(new object?[] { id }, ct);
        if (p is null) return NotFound();
        _db.PhraseTranslations.Remove(p);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Best-effort dictionary translation. Anything not in the dictionary stays in the
    /// source language; admin edits in the side-by-side preview.</summary>
    [HttpPost("translate")]
    public async Task<ActionResult<TranslateResponse>> Translate(
        [FromBody] TranslateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return Ok(new TranslateResponse(string.Empty, Array.Empty<string>(), true));
        var outcome = await _translator.TranslateAsync(request.Text, request.From, request.To, ct);
        return Ok(new TranslateResponse(outcome.Translated, outcome.MatchedPhrases, outcome.FullyTranslated));
    }

    // --- Helpers ---

    private static RecipientTarget MapTarget(BroadcastTargetDto dto) =>
        new(
            Kind: (RecipientTargetKind)dto.Kind,
            Phone: dto.Phone,
            Name: dto.Name,
            CustomGroupId: dto.CustomGroupId,
            DynamicGroupKey: dto.DynamicGroupKey,
            AdHocRecipients: dto.Recipients?
                .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
                .Select(r => new ResolvedRecipient(r.Phone.Trim(), r.Name?.Trim(), null))
                .ToList());

    private static BroadcastDetail ToDetail(Broadcast b) => new(
        b.Id, b.Channel, b.BodyEn, b.BodyEs, b.TargetLabel, b.CreatedAt,
        b.Recipients.Select(r => new BroadcastRecipientDto(
            r.Id, r.Name, r.Phone, r.Language, r.Status, r.StatusMessage, r.TwilioSid)).ToList());

    private static GroupConversationDetail ToDetail(GroupConversation c) => new(
        c.Id, c.Title, c.Channel, c.TwilioConversationSid, c.CreatedAt,
        c.Participants.Select(p => new GroupConversationParticipantDto(
            p.Id, p.Name, p.Phone, p.TwilioParticipantSid)).ToList());

    private static WhatsAppTemplateDto ToDto(WhatsAppTemplate t) => new(
        t.Id, t.Name, t.ContentSid, t.Language, t.Description, t.PreviewText, t.CreatedAt,
        t.Variables.OrderBy(v => v.Position).Select(v => new WhatsAppTemplateVariableDto(
            v.Id, v.Position, v.Label, v.Example)).ToList());

    private static List<WhatsAppTemplateVariable> MapVariables(IEnumerable<SaveTemplateVariableDto> input) =>
        input
            .Where(v => v.Position > 0 && !string.IsNullOrWhiteSpace(v.Label))
            .GroupBy(v => v.Position) // dedupe: keep the last entry for any duplicate position
            .Select(g => g.Last())
            .Select(v => new WhatsAppTemplateVariable
            {
                Position = v.Position,
                Label = v.Label.Trim(),
                Example = v.Example?.Trim()
            })
            .ToList();
}
