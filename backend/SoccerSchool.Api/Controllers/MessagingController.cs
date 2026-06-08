using System.Globalization;
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
    private readonly IEmailSender _emailSender;
    private readonly IRecipientResolver _resolver;
    private readonly IConversationService _conversations;
    private readonly IPhraseTranslator _translator;
    private readonly ITwilioMessageReconciler _reconciler;
    private readonly TwilioOptions _twilio;
    private readonly AppOptions _app;

    public MessagingController(
        AppDbContext db,
        IMessageSender sender,
        IEmailSender emailSender,
        IRecipientResolver resolver,
        IConversationService conversations,
        IPhraseTranslator translator,
        ITwilioMessageReconciler reconciler,
        IOptions<TwilioOptions> twilio,
        IOptions<AppOptions> app)
    {
        _db = db;
        _sender = sender;
        _emailSender = emailSender;
        _resolver = resolver;
        _conversations = conversations;
        _translator = translator;
        _reconciler = reconciler;
        _twilio = twilio.Value;
        _app = app.Value;
    }

    /// <summary>Admin-triggered backfill of Twilio messages our DB is missing. The hourly
    /// background reconciler covers a 6h window; this lets the admin scan a wider lookback
    /// (default 7d, max 30d) when recovering from a past gap. Same idempotent logic — rows
    /// whose TwilioSid we already have are skipped.</summary>
    [HttpPost("twilio/reconcile")]
    public async Task<ActionResult<ReconcileMessagesResult>> ReconcileTwilio(
        [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(days, 1, 30);
        var result = await _reconciler.ReconcileAsync(TimeSpan.FromDays(clamped), ct);
        return Ok(result);
    }

    // --- Capabilities probe ---

    [HttpGet("config")]
    public ActionResult<MessagingConfigDto> Config() =>
        Ok(new MessagingConfigDto(
            Sms: _twilio.IsSmsConfigured,
            WhatsApp: _twilio.IsWhatsAppConfigured,
            Email: _emailSender.IsAvailable,
            Conversations: _twilio.IsSmsConfigured || _twilio.IsWhatsAppConfigured));

    // --- Curated groups ---

    [HttpGet("groups")]
    public async Task<ActionResult<object>> ListGroups(CancellationToken ct)
    {
        // MemberCount mirrors what ResolveAsync(CustomGroup) will actually fan out to: reachable
        // (phone or email present) AND not opted out at the family level. Without this filter the
        // dropdown shows inflated counts vs. who actually receives the broadcast.
        var curated = await _db.MessageGroups
            .OrderBy(g => g.Name)
            .Select(g => new MessageGroupSummary(
                g.Id, g.Name, g.Description, g.Language,
                g.Members.Count(m =>
                    ((m.Phone != null && m.Phone != "") || (m.Email != null && m.Email != ""))
                    && (m.ParentAccount == null || !m.ParentAccount.NoCommunications)),
                g.CreatedAt))
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
            g.Members.Select(m => new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Email, m.Language, m.ParentAccountId)).ToList()));
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
        // Hydrate ParentAccount on members so the reachable+not-opted-out count matches ListGroups.
        await _db.Entry(g).Collection(x => x.Members).Query()
            .Include(m => m.ParentAccount).LoadAsync(ct);
        var memberCount = g.Members.Count(m =>
            ((!string.IsNullOrWhiteSpace(m.Phone)) || (!string.IsNullOrWhiteSpace(m.Email)))
            && (m.ParentAccount == null || !m.ParentAccount.NoCommunications));
        return Ok(new MessageGroupSummary(g.Id, g.Name, g.Description, g.Language, memberCount, g.CreatedAt));
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
        var phone = PhoneNormalizer.Normalize(request.Phone) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone)) return BadRequest("Phone is required.");
        if (await _db.MessageGroupMembers.AnyAsync(m => m.MessageGroupId == id && m.Phone == phone, ct))
            return Conflict("This phone is already in the group.");
        var m = new MessageGroupMember
        {
            MessageGroupId = id,
            Name = request.Name?.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Language = request.Language ?? g.Language,
            ParentAccountId = request.ParentAccountId
        };
        _db.MessageGroupMembers.Add(m);
        await _db.SaveChangesAsync(ct);
        return Ok(new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Email, m.Language, m.ParentAccountId));
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
        return Ok(new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Email, m.Language, m.ParentAccountId));
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
                Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email,
                Language = memberLang,
                ParentAccountId = r.ParentAccountId
            });
            existing.Add(r.Phone);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new MessageGroupDetail(
            g.Id, g.Name, g.Description, g.Language, g.CreatedAt,
            g.Members.Select(m => new MessageGroupMemberDto(m.Id, m.Name, m.Phone, m.Email, m.Language, m.ParentAccountId)).ToList()));
    }

    // --- Broadcasts (fan-out) ---

    [HttpPost("broadcasts")]
    public async Task<ActionResult<BroadcastDetail>> CreateBroadcast(
        [FromBody] CreateBroadcastRequest request, CancellationToken ct)
    {
        if (!_sender.IsAvailable(request.Channel))
            return BadRequest($"{request.Channel} not configured on this server.");

        // Two send modes: free-form (with optional bilingual bodies) or WhatsApp Content template.
        var isWhatsAppTemplate = request.WhatsAppTemplateId.HasValue;
        var isEmailTemplate = request.EmailTemplateId.HasValue;
        WhatsAppTemplate? template = null;
        EmailTemplate? emailTemplate = null;
        Dictionary<string, string> templateVars = new();
        var bodyEn = request.BodyEn?.Trim();
        var bodyEs = request.BodyEs?.Trim();
        var subjectEn = request.SubjectEn?.Trim();
        var subjectEs = request.SubjectEs?.Trim();

        if (isWhatsAppTemplate)
        {
            if (request.Channel != MessageChannel.WhatsApp)
                return BadRequest("WhatsApp templates can only be used on the WhatsApp channel.");
            if (isEmailTemplate)
                return BadRequest("Specify either WhatsAppTemplateId or EmailTemplateId, not both.");
            template = await _db.WhatsAppTemplates
                .Include(t => t.Variables)
                .FirstOrDefaultAsync(t => t.Id == request.WhatsAppTemplateId, ct);
            if (template is null) return BadRequest("WhatsApp template not found.");
            templateVars = (request.TemplateVariables ?? new())
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);
            var missing = template.Variables
                .Select(v => v.Position.ToString(CultureInfo.InvariantCulture))
                .Where(key => !templateVars.ContainsKey(key) || string.IsNullOrWhiteSpace(templateVars[key]))
                .ToList();
            if (missing.Count > 0)
                return BadRequest($"Template variables missing: {string.Join(", ", missing)}.");
        }
        else if (isEmailTemplate)
        {
            if (request.Channel != MessageChannel.Email)
                return BadRequest("Email templates can only be used on the Email channel.");
            emailTemplate = await _db.EmailTemplates
                .Include(t => t.Variables)
                .FirstOrDefaultAsync(t => t.Id == request.EmailTemplateId, ct);
            if (emailTemplate is null) return BadRequest("Email template not found.");
            templateVars = (request.TemplateVariables ?? new())
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);
            var missing = emailTemplate.Variables
                .Select(v => v.Position.ToString(CultureInfo.InvariantCulture))
                .Where(key => !templateVars.ContainsKey(key) || string.IsNullOrWhiteSpace(templateVars[key]))
                .ToList();
            if (missing.Count > 0)
                return BadRequest($"Template variables missing: {string.Join(", ", missing)}.");
        }
        else
        {
            // Free-form: at least one body required. For email, subject also required.
            if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyEs))
                return BadRequest("Either BodyEn, BodyEs, or a template is required.");
            if (request.Channel == MessageChannel.Email &&
                string.IsNullOrWhiteSpace(subjectEn) && string.IsNullOrWhiteSpace(subjectEs))
                return BadRequest("Email broadcasts require a subject.");
        }

        var target = MapTarget(request.Target);
        var resolved = await _resolver.ResolveAsync(target, ct);
        if (resolved.Recipients.Count == 0)
            return BadRequest("No recipients matched the selected target.");

        // For template sends, look up the language pair so we can route each recipient to the
        // template matching their language. Pair lookup is by base name with opposite Language.
        // If no pair exists, every recipient gets the primary template and the log notes the mismatch.
        WhatsAppTemplate? pairedTemplate = null;
        EmailTemplate? pairedEmailTemplate = null;
        if (isWhatsAppTemplate)
            pairedTemplate = await FindPairAsync(template!, ct);
        if (isEmailTemplate)
            pairedEmailTemplate = await FindEmailPairAsync(emailTemplate!, ct);

        // For WhatsApp-template sends, persist a rendered preview of BOTH the primary template
        // AND its paired-language sibling so the thread/history view can show each recipient the
        // body that actually went out (Twilio renders the real substitution server-side, per
        // recipient language). Without this, Spanish recipients saw the English rendering in the
        // inbox because BodyEs was null. For Email templates, the subject + body live on the
        // broadcast itself so we copy those in directly.
        string? whatsAppRenderedEn = null;
        string? whatsAppRenderedEs = null;
        if (isWhatsAppTemplate)
        {
            var renderedPrimary = RenderTemplatePreview(template!.PreviewText, template.Name, templateVars);
            var renderedPair = pairedTemplate is not null
                ? RenderTemplatePreview(pairedTemplate.PreviewText, pairedTemplate.Name, templateVars)
                : null;
            if (template.Language == Language.English)
            {
                whatsAppRenderedEn = renderedPrimary;
                whatsAppRenderedEs = renderedPair;
            }
            else
            {
                whatsAppRenderedEs = renderedPrimary;
                whatsAppRenderedEn = renderedPair;
            }
        }

        var broadcast = new Broadcast
        {
            Channel = request.Channel,
            BodyEn = isEmailTemplate
                ? RenderTemplateString(emailTemplate!.Body, emailTemplate.Variables, templateVars)
                : (bodyEn ?? whatsAppRenderedEn),
            BodyEs = isEmailTemplate && pairedEmailTemplate is not null
                ? RenderTemplateString(pairedEmailTemplate.Body, pairedEmailTemplate.Variables, templateVars)
                : (bodyEs ?? whatsAppRenderedEs),
            SubjectEn = isEmailTemplate
                ? RenderTemplateString(emailTemplate!.Subject, emailTemplate.Variables, templateVars)
                : subjectEn,
            SubjectEs = isEmailTemplate && pairedEmailTemplate is not null
                ? RenderTemplateString(pairedEmailTemplate.Subject, pairedEmailTemplate.Variables, templateVars)
                : subjectEs,
            TargetLabel = resolved.Label,
            WhatsAppTemplateId = template?.Id,
            TemplateVariablesJson = (isWhatsAppTemplate || isEmailTemplate) ? JsonSerializer.Serialize(templateVars) : null,
            ScheduledGameId = request.ScheduledGameId,
            TournamentId = request.TournamentId,
            PlayerId = request.PlayerId,
            BatchId = request.BatchId,
        };
        foreach (var r in resolved.Recipients)
        {
            var lang = r.Language ?? request.DefaultLanguage;
            broadcast.Recipients.Add(new BroadcastRecipient
            {
                Name = r.Name,
                Phone = r.Phone,
                Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email,
                Language = lang,
                Status = MessageDeliveryStatus.Pending
            });
        }
        _db.Broadcasts.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        // Build a phone → HasWhatsApp lookup from the resolved recipients so the WhatsApp skip
        // below has O(1) access to each recipient's stored flag. Recipients with an unknown flag
        // (individual sends, ad-hoc list, group members not linked to a ParentAccount) are absent
        // from the dict and fall through to the normal send path.
        var hasWhatsAppByPhone = resolved.Recipients
            .Where(r => r.HasWhatsApp.HasValue && !string.IsNullOrWhiteSpace(r.Phone))
            .GroupBy(r => r.Phone)
            .ToDictionary(g => g.Key, g => g.First().HasWhatsApp!.Value);

        // Synchronous fan-out. Branches by channel: SMS/WhatsApp go through IMessageSender,
        // Email goes through IEmailSender. Each recipient gets the language-matching content;
        // for templates we use the paired template when the recipient's language differs.
        foreach (var recipient in broadcast.Recipients)
        {
            // Skip WhatsApp sends to recipients we know don't have WhatsApp. Saves a guaranteed
            // Twilio failure and surfaces the reason cleanly in the History row.
            if (request.Channel == MessageChannel.WhatsApp
                && hasWhatsAppByPhone.TryGetValue(recipient.Phone, out var has) && !has)
            {
                recipient.Status = MessageDeliveryStatus.Failed;
                recipient.StatusMessage = "Skipped: recipient does not have WhatsApp on file. Use SMS or Email instead.";
                continue;
            }

            if (request.Channel == MessageChannel.Email)
            {
                await SendEmailRecipientAsync(recipient, broadcast, emailTemplate, pairedEmailTemplate, templateVars, ct);
            }
            else if (isWhatsAppTemplate)
            {
                await SendWhatsAppTemplateRecipientAsync(recipient, template!, pairedTemplate, templateVars, ct);
            }
            else
            {
                var body = recipient.Language == Language.Spanish ? (bodyEs ?? bodyEn) : (bodyEn ?? bodyEs);
                var send = await _sender.SendAsync(request.Channel, recipient.Phone, body ?? string.Empty, ct);
                recipient.TwilioSid = send.TwilioSid;
                recipient.Status = send.Status;
                recipient.StatusMessage = send.Message;
            }
        }
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(broadcast));
    }

    /// <summary>Re-sends the most recent message for an event to the rostered players' guardians who
    /// haven't confirmed yet ("no response"). Clones the original's channel + template/body and runs
    /// the normal broadcast pipeline against the <c>event-pending-{id}</c> audience.</summary>
    [HttpPost("events/{eventId:int}/resend")]
    public Task<ActionResult<BroadcastDetail>> ResendEventMessage(int eventId, CancellationToken ct) =>
        ResendEventToTargetAsync(eventId, $"{RecipientResolver.DynamicEventPendingPrefix}{eventId}", ct);

    /// <summary>Re-sends the event's most recent message to a single rostered player's guardians.</summary>
    [HttpPost("events/{eventId:int}/resend/{playerId:int}")]
    public Task<ActionResult<BroadcastDetail>> ResendEventToPlayer(int eventId, int playerId, CancellationToken ct) =>
        ResendEventToTargetAsync(eventId, $"{RecipientResolver.DynamicPlayerPrefix}{playerId}", ct);

    private async Task<ActionResult<BroadcastDetail>> ResendEventToTargetAsync(int eventId, string dynamicGroupKey, CancellationToken ct)
    {
        var latest = await _db.Broadcasts
            .Where(b => b.ScheduledGameId == eventId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (latest is null)
            return BadRequest("No message has been sent for this event yet.");

        Dictionary<string, string>? vars = null;
        if (!string.IsNullOrWhiteSpace(latest.TemplateVariablesJson))
        {
            try { vars = JsonSerializer.Deserialize<Dictionary<string, string>>(latest.TemplateVariablesJson); }
            catch { vars = null; }
        }

        var request = new CreateBroadcastRequest
        {
            Channel = latest.Channel,
            BodyEn = latest.BodyEn,
            BodyEs = latest.BodyEs,
            SubjectEn = latest.SubjectEn,
            SubjectEs = latest.SubjectEs,
            WhatsAppTemplateId = latest.WhatsAppTemplateId,
            TemplateVariables = vars,
            ScheduledGameId = eventId,
            Target = new BroadcastTargetDto
            {
                Kind = RecipientTargetKindDto.DynamicGroup,
                DynamicGroupKey = dynamicGroupKey,
            },
        };
        return await CreateBroadcast(request, ct);
    }

    private async Task SendWhatsAppTemplateRecipientAsync(
        BroadcastRecipient recipient,
        WhatsAppTemplate template,
        WhatsAppTemplate? pairedTemplate,
        Dictionary<string, string> templateVars,
        CancellationToken ct)
    {
        var sendTemplate = recipient.Language == template.Language ? template
            : (pairedTemplate?.Language == recipient.Language ? pairedTemplate : template);

        // Values were authored against the primary template's language (the admin filled them in
        // while looking at the primary template's variable labels). Translate them into the sent
        // template's language so body + variables read in the same language — covers both the
        // "use the paired template" case and any future mismatch path.
        var varsToSend = templateVars;
        if (sendTemplate.Language != template.Language)
        {
            var translated = new Dictionary<string, string>();
            foreach (var kv in templateVars)
            {
                var outcome = await _translator.TranslateAsync(
                    kv.Value, template.Language, sendTemplate.Language, ct);
                translated[kv.Key] = outcome.Translated;
            }
            varsToSend = translated;
        }

        var send = await _sender.SendTemplateAsync(recipient.Phone, sendTemplate.ContentSid, varsToSend, ct);
        if (sendTemplate.Language != recipient.Language)
            send = send with { Message = $"[No {recipient.Language} template; sent {sendTemplate.Language} body] {send.Message}" };

        recipient.TemplateUsed = sendTemplate.Name;
        recipient.TwilioSid = send.TwilioSid;
        recipient.Status = send.Status;
        recipient.StatusMessage = send.Message;
    }

    private async Task SendEmailRecipientAsync(
        BroadcastRecipient recipient,
        Broadcast broadcast,
        EmailTemplate? emailTemplate,
        EmailTemplate? pairedEmailTemplate,
        Dictionary<string, string> templateVars,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            recipient.Status = MessageDeliveryStatus.Failed;
            recipient.StatusMessage = "No email address on file.";
            return;
        }

        string subject;
        string body;
        if (emailTemplate is not null)
        {
            // Pick the language-matching template; fall back to the primary if no pair exists.
            var pickedTemplate = recipient.Language == emailTemplate.Language ? emailTemplate
                : (pairedEmailTemplate?.Language == recipient.Language ? pairedEmailTemplate : emailTemplate);
            // Values were authored against the primary template's language; translate when we're
            // actually sending via the paired one so subject/body + variables read in the same lang.
            var vars = templateVars;
            if (pickedTemplate.Language != emailTemplate.Language)
            {
                var translated = new Dictionary<string, string>();
                foreach (var kv in templateVars)
                {
                    var outcome = await _translator.TranslateAsync(
                        kv.Value, emailTemplate.Language, pickedTemplate.Language, ct);
                    translated[kv.Key] = outcome.Translated;
                }
                vars = translated;
            }
            subject = RenderTemplateString(pickedTemplate.Subject, pickedTemplate.Variables, vars);
            body = RenderTemplateString(pickedTemplate.Body, pickedTemplate.Variables, vars);
            recipient.TemplateUsed = pickedTemplate.Name;
        }
        else
        {
            // Free-form: pick subject + body matching recipient language, falling back to the
            // other side if their preferred language is empty.
            subject = recipient.Language == Language.Spanish
                ? (broadcast.SubjectEs ?? broadcast.SubjectEn ?? string.Empty)
                : (broadcast.SubjectEn ?? broadcast.SubjectEs ?? string.Empty);
            body = recipient.Language == Language.Spanish
                ? (broadcast.BodyEs ?? broadcast.BodyEn ?? string.Empty)
                : (broadcast.BodyEn ?? broadcast.BodyEs ?? string.Empty);
        }

        var send = await _emailSender.SendAsync(recipient.Email, subject, body, ct);
        recipient.TwilioSid = send.MessageId;
        recipient.Status = send.Success ? MessageDeliveryStatus.Queued : MessageDeliveryStatus.Failed;
        recipient.StatusMessage = send.Message;
    }

    private static string RenderTemplateString(
        string template,
        IEnumerable<EmailTemplateVariable> templateVars,
        IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var result = template;
        foreach (var v in templateVars)
        {
            var key = v.Position.ToString(CultureInfo.InvariantCulture);
            var val = values.TryGetValue(key, out var x) ? x : "";
            result = result.Replace($"{{{{{key}}}}}", val);
        }
        return result;
    }

    private async Task<EmailTemplate?> FindEmailPairAsync(EmailTemplate t, CancellationToken ct)
    {
        var baseName = BaseName(t.Name);
        string[] candidates =
        {
            baseName,
            baseName + "_en", baseName + "_es",
            baseName + "_english", baseName + "_spanish"
        };
        return await _db.EmailTemplates
            .Include(x => x.Variables)
            .FirstOrDefaultAsync(x =>
                x.Id != t.Id &&
                x.Language != t.Language &&
                candidates.Contains(x.Name),
                ct);
    }


    /// <summary>Read-only "why is this message missing from the inbox?" diagnostic. Looks up a
    /// BroadcastRecipient by its Twilio SID OR by any phone-form variant of <paramref name="phone"/>,
    /// returns the raw stored Phone string + the parent Broadcast headline, and reports whether
    /// the row would have matched the per-phone thread query. Handy when a phone format wasn't
    /// normalized (the row is in the DB but never surfaces in the inbox).</summary>
    [HttpGet("diagnostic/message")]
    public async Task<ActionResult<object>> DiagnoseMessage(
        [FromQuery] string? sid, [FromQuery] string? phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sid) && string.IsNullOrWhiteSpace(phone))
            return BadRequest("Provide either sid (Twilio SM/MM/IM) or phone.");

        var matchedBySid = !string.IsNullOrWhiteSpace(sid)
            ? await _db.BroadcastRecipients
                .Where(r => r.TwilioSid == sid.Trim())
                .Select(r => new
                {
                    r.Id,
                    r.BroadcastId,
                    StoredPhone = r.Phone,
                    r.Email,
                    r.Language,
                    r.Status,
                    r.ErrorCode,
                    r.StatusMessage,
                    r.TwilioSid,
                    BroadcastCreatedAt = r.Broadcast!.CreatedAt,
                    BroadcastChannel = r.Broadcast.Channel,
                    BroadcastBodyEn = r.Broadcast.BodyEn,
                    BroadcastBodyEs = r.Broadcast.BodyEs,
                    r.Broadcast.TargetLabel,
                })
                .ToListAsync(ct)
            : null;

        // Match by every phone-form variant the inbox would use. Also do a digit-suffix LIKE so
        // punctuated rows (e.g. "(831) 756-8859") surface even though Variants doesn't produce
        // that exact string.
        List<string>? phoneVariants = null;
        object? matchedByPhone = null;
        string? digitSuffix = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            phoneVariants = PhoneNormalizer.Variants(phone).ToList();
            // Last 10 digits = strongest "same human" signal for US numbers.
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            digitSuffix = digits.Length >= 10 ? digits[^10..] : digits;
            var likePattern = $"%{digitSuffix}%";

            var byVariants = await _db.BroadcastRecipients
                .Where(r => phoneVariants.Contains(r.Phone))
                .Select(r => new { r.Id, r.BroadcastId, r.Phone, r.TwilioSid, r.Status, MatchMode = "variant", r.Broadcast!.CreatedAt })
                .ToListAsync(ct);
            var byLike = await _db.BroadcastRecipients
                .Where(r => EF.Functions.Like(r.Phone, likePattern) && !phoneVariants.Contains(r.Phone))
                .Select(r => new { r.Id, r.BroadcastId, r.Phone, r.TwilioSid, r.Status, MatchMode = "digit-suffix-only", r.Broadcast!.CreatedAt })
                .ToListAsync(ct);
            var inboundByVariants = await _db.InboundMessages
                .Where(m => m.FromPhone != null && phoneVariants.Contains(m.FromPhone))
                .Select(m => new { m.Id, FromPhone = m.FromPhone, m.TwilioSid, m.ReceivedAt, MatchMode = "variant" })
                .ToListAsync(ct);
            matchedByPhone = new
            {
                BroadcastRecipientsByVariant = byVariants,
                BroadcastRecipientsByDigitSuffixOnly = byLike,
                InboundMessagesByVariant = inboundByVariants,
            };
        }

        return Ok(new
        {
            InputSid = sid,
            InputPhone = phone,
            PhoneVariantsTried = phoneVariants,
            DigitSuffix = digitSuffix,
            MatchedBySid = matchedBySid,
            MatchedByPhone = matchedByPhone,
        });
    }


    [HttpGet("inbound")]
    public async Task<ActionResult<IEnumerable<InboundMessageDto>>> ListInbound(CancellationToken ct)
    {
        var items = await _db.InboundMessages
            .OrderByDescending(m => m.ReceivedAt)
            .Take(200)
            .Select(m => new InboundMessageDto(
                m.Id, m.Channel, m.FromPhone, m.ToPhone, m.Body, m.TwilioSid, m.ReceivedAt,
                m.BroadcastId,
                // Short preview of the original broadcast so the UI can show "Re: <preview>" without
                // an extra fetch. Falls back to subject/label/template id depending on what's set.
                m.Broadcast == null ? null
                    : (m.Broadcast.BodyEn ?? m.Broadcast.BodyEs ?? m.Broadcast.SubjectEn ?? m.Broadcast.SubjectEs ?? m.Broadcast.TargetLabel)))
            .ToListAsync(ct);
        return Ok(items);
    }

    // --- Threaded view: list distinct phones, fetch full thread, reply --------

    /// <summary>One row per distinct phone we've exchanged messages with. Powers the admin Inbox.
    /// Scoped to inbounds that came to a currently-configured sender number (SMS or WhatsApp), so
    /// legacy history from retired sender numbers drops off automatically when the env var rolls.</summary>
    [HttpGet("threads")]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> ListThreads(CancellationToken ct)
    {
        // Build the set of "our active receivers" — every configured sender number, in both raw
        // and variant form, so Twilio's E.164 ToPhone matches regardless of stored format.
        var ourNumbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in PhoneNormalizer.Variants(_twilio.SmsFromNumber)) ourNumbers.Add(v);
        foreach (var v in PhoneNormalizer.Variants(_twilio.WhatsAppFromNumber)) ourNumbers.Add(v);

        if (ourNumbers.Count == 0)
            return Ok(Array.Empty<ThreadSummaryDto>());

        // Pull last 6 months of activity into memory and group there — keeps the SQL simple and
        // works against both InboundMessages and BroadcastRecipients without a complex union join.
        var since = DateTime.UtcNow.AddMonths(-6);
        var inbound = await _db.InboundMessages
            .Where(m => m.ReceivedAt >= since && m.ToPhone != null && ourNumbers.Contains(m.ToPhone))
            .Select(m => new { m.FromPhone, m.Body, At = m.ReceivedAt, Direction = ThreadDirection.Inbound })
            .ToListAsync(ct);

        // Only show outbound history for phones that have actually replied to a current sender —
        // that's the "Inbox" semantics. Sends to parents who never replied stay in the History
        // tab; they're not conversations.
        var activePhones = inbound
            .Where(x => !string.IsNullOrWhiteSpace(x.FromPhone))
            .Select(x => x.FromPhone)
            .Distinct()
            .ToList();
        var outbound = activePhones.Count == 0
            ? new List<dynamic>().Select(_ => new { FromPhone = "", Body = (string?)null, At = DateTime.MinValue, Direction = ThreadDirection.Outbound }).ToList()
            : await _db.BroadcastRecipients
                .Where(r => r.Broadcast!.CreatedAt >= since && activePhones.Contains(r.Phone))
                .Select(r => new
                {
                    FromPhone = r.Phone,
                    Body = r.Broadcast!.BodyEn ?? r.Broadcast.BodyEs ?? r.Broadcast.SubjectEn ?? r.Broadcast.SubjectEs,
                    At = r.Broadcast.CreatedAt,
                    Direction = ThreadDirection.Outbound
                })
                .ToListAsync(ct);

        var byPhone = inbound.Concat(outbound)
            .Where(x => !string.IsNullOrWhiteSpace(x.FromPhone))
            .GroupBy(x => x.FromPhone)
            .ToList();

        // Look up parents by every common form variant of each thread phone so a parent stored
        // as +18317568859 still matches inbounds/outbounds recorded as 8317568859 (or vice versa).
        var phones = byPhone.Select(g => g.Key).ToList();
        var allCandidates = phones.SelectMany(PhoneNormalizer.Variants).Distinct().ToList();
        var parentsByForm = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && allCandidates.Contains(p.CellPhone))
            .ToDictionaryAsync(p => p.CellPhone!, p => p, ct);
        // Also index additional guardians (ParentContact) by phone, so a co-parent texting from
        // their own number is identified by name even though they have no login of their own.
        var contactsByForm = (await _db.ParentContacts
            .Where(c => c.CellPhone != null && allCandidates.Contains(c.CellPhone))
            .ToListAsync(ct))
            .GroupBy(c => c.CellPhone!)
            .ToDictionary(g => g.Key, g => g.First());

        // Resolve a thread phone to a known person: registered account holder first, then any
        // additional guardian. "Known" suppresses the inbox's unregistered badge either way.
        (string? Name, int? ParentAccountId, bool Known) ResolveIdentity(string phone)
        {
            foreach (var v in PhoneNormalizer.Variants(phone))
                if (parentsByForm.TryGetValue(v, out var p))
                    return ($"{p.FirstName} {p.LastName}".Trim(), p.Id, true);
            foreach (var v in PhoneNormalizer.Variants(phone))
                if (contactsByForm.TryGetValue(v, out var c))
                    return ($"{c.FirstName} {c.LastName}".Trim(), c.ParentAccountId, true);
            return (null, null, false);
        }

        var summaries = byPhone
            .Select(g =>
            {
                var last = g.OrderByDescending(x => x.At).First();
                var inboundCount = g.Count(x => x.Direction == ThreadDirection.Inbound);
                var outboundCount = g.Count(x => x.Direction == ThreadDirection.Outbound);
                var who = ResolveIdentity(g.Key);
                return new ThreadSummaryDto(
                    g.Key,
                    string.IsNullOrWhiteSpace(who.Name) ? null : who.Name,
                    who.ParentAccountId,
                    who.Known,
                    last.At,
                    last.Body,
                    last.Direction,
                    inboundCount,
                    outboundCount);
            })
            .OrderByDescending(s => s.LastAt)
            .Take(200)
            .ToList();

        return Ok(summaries);
    }


    /// <summary>Search registered parents for the Inbox "Message a parent" picker. Lets the
    /// admin start a conversation with a parent who hasn't replied yet (so they don't show up
    /// in <see cref="ListThreads"/>). <paramref name="q"/> matches first/last/full name and
    /// trailing-digit phone substring; <paramref name="unrepliedOnly"/>=true filters to parents
    /// with no inbound on record in the last 6 months.</summary>
    [HttpGet("parents-search")]
    public async Task<ActionResult<IEnumerable<InboxParentDto>>> SearchInboxParents(
        [FromQuery] string? q, [FromQuery] bool unrepliedOnly = false, [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var cap = Math.Clamp(limit, 1, 200);
        // Only parents we can actually reach by SMS/WhatsApp — phone-required.
        var query = _db.ParentAccounts
            .Where(p => !p.NoCommunications && p.CellPhone != null && p.CellPhone != "");
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            var digits = new string(needle.Where(char.IsDigit).ToArray());
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName ?? "", $"%{needle}%")
                || EF.Functions.Like(p.LastName ?? "", $"%{needle}%")
                || EF.Functions.Like((p.FirstName ?? "") + " " + (p.LastName ?? ""), $"%{needle}%")
                || (digits.Length > 0 && EF.Functions.Like(p.CellPhone!, $"%{digits}%")));
        }
        var parents = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(cap * 2) // overshoot so the unreplied filter still has enough to return.
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.CellPhone, p.Language })
            .ToListAsync(ct);
        if (parents.Count == 0) return Ok(Array.Empty<InboxParentDto>());

        // Resolve "has replied" by checking InboundMessages over the last 6 months against every
        // phone-form variant — mirrors the threads list semantics.
        var since = DateTime.UtcNow.AddMonths(-6);
        var allCandidates = parents
            .Where(p => !string.IsNullOrWhiteSpace(p.CellPhone))
            .SelectMany(p => PhoneNormalizer.Variants(p.CellPhone!))
            .Distinct()
            .ToList();
        var repliedPhones = await _db.InboundMessages
            .Where(m => m.ReceivedAt >= since && m.FromPhone != null && allCandidates.Contains(m.FromPhone))
            .Select(m => m.FromPhone!)
            .Distinct()
            .ToListAsync(ct);
        var repliedSet = new HashSet<string>(repliedPhones, StringComparer.Ordinal);

        bool HasReplied(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            foreach (var v in PhoneNormalizer.Variants(phone))
                if (repliedSet.Contains(v)) return true;
            return false;
        }

        var rows = parents
            .Select(p => new InboxParentDto(
                p.Id,
                $"{p.FirstName} {p.LastName}".Trim(),
                p.CellPhone!,
                p.Language,
                HasReplied(p.CellPhone)))
            .Where(d => !unrepliedOnly || !d.HasReplied)
            .Take(cap)
            .ToList();
        return Ok(rows);
    }


    /// <summary>Full chronological thread for one phone — inbounds + outbounds interleaved.
    /// Match by every common phone-form variant so a recipient stored as 8317568859 still surfaces
    /// the broadcast row recorded as +18317568859 (and vice versa) — otherwise outbounds get
    /// dropped from the inbox view depending on which form was stored.</summary>
    [HttpGet("threads/{phone}")]
    public async Task<ActionResult<ThreadDetailDto>> GetThread(string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return BadRequest("Phone is required.");
        var phoneVariants = PhoneNormalizer.Variants(phone).ToList();

        var inbound = await _db.InboundMessages
            .Where(m => m.FromPhone != null && phoneVariants.Contains(m.FromPhone))
            .Select(m => new ThreadMessageDto(
                ThreadDirection.Inbound,
                m.Channel,
                m.Body ?? string.Empty,
                m.ReceivedAt,
                null,
                null,
                m.BroadcastId))
            .ToListAsync(ct);

        var outbound = await _db.BroadcastRecipients
            .Where(r => phoneVariants.Contains(r.Phone))
            .Select(r => new ThreadMessageDto(
                ThreadDirection.Outbound,
                r.Broadcast!.Channel,
                r.Language == Language.Spanish
                    ? (r.Broadcast.BodyEs ?? r.Broadcast.BodyEn ?? r.Broadcast.SubjectEs ?? r.Broadcast.SubjectEn ?? string.Empty)
                    : (r.Broadcast.BodyEn ?? r.Broadcast.BodyEs ?? r.Broadcast.SubjectEn ?? r.Broadcast.SubjectEs ?? string.Empty),
                r.Broadcast.CreatedAt,
                r.Status,
                r.StatusMessage,
                r.BroadcastId))
            .ToListAsync(ct);

        var messages = inbound.Concat(outbound).OrderBy(m => m.At).ToList();

        var parent = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && phoneVariants.Contains(p.CellPhone))
            .FirstOrDefaultAsync(ct);

        string? name = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
        int? parentAccountId = parent?.Id;
        bool known = parent is not null;
        Language? language = parent?.Language;
        if (parent is null)
        {
            // Fall back to an additional guardian so the thread is still identified by name and
            // linked to the family, even though that guardian has no login of their own.
            var contact = await _db.ParentContacts
                .Where(c => c.CellPhone != null && phoneVariants.Contains(c.CellPhone))
                .FirstOrDefaultAsync(ct);
            if (contact is not null)
            {
                name = $"{contact.FirstName} {contact.LastName}".Trim();
                parentAccountId = contact.ParentAccountId;
                known = true;
                language = contact.Language;
            }
        }

        return Ok(new ThreadDetailDto(
            phone,
            string.IsNullOrWhiteSpace(name) ? null : name,
            parentAccountId,
            known,
            language,
            messages));
    }

    /// <summary>Sends a one-off reply on the chosen channel as a single-recipient broadcast. Reuses
    /// the broadcast pipeline so the outbound shows up in History + the thread automatically.</summary>
    [HttpPost("threads/{phone}/reply")]
    public async Task<ActionResult<ThreadMessageDto>> SendThreadReply(
        string phone, [FromBody] SendThreadReplyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return BadRequest("Phone is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) return BadRequest("Body is required.");
        if (!_sender.IsAvailable(request.Channel))
            return BadRequest($"{request.Channel} not configured on this server.");

        // Normalize so the stored Phone is in E.164 — that way the same recipient row will
        // surface in the thread regardless of which phone-form variant the next URL uses.
        var normalizedPhone = PhoneNormalizer.Normalize(phone) ?? phone.Trim();
        var variants = PhoneNormalizer.Variants(phone);
        var parent = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && variants.Contains(p.CellPhone))
            .FirstOrDefaultAsync(ct);
        var name = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
        var lang = parent?.Language ?? Language.English;

        var broadcast = new Broadcast
        {
            Channel = request.Channel,
            BodyEn = lang == Language.English ? request.Body.Trim() : null,
            BodyEs = lang == Language.Spanish ? request.Body.Trim() : null,
            TargetLabel = $"Reply to {normalizedPhone}",
        };
        var recipient = new BroadcastRecipient
        {
            Name = name,
            Phone = normalizedPhone,
            Email = null,
            Language = lang,
            Status = MessageDeliveryStatus.Pending
        };
        broadcast.Recipients.Add(recipient);
        _db.Broadcasts.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        var send = await _sender.SendAsync(request.Channel, normalizedPhone, request.Body.Trim(), ct);
        recipient.TwilioSid = send.TwilioSid;
        recipient.Status = send.Status;
        recipient.StatusMessage = send.Message;
        await _db.SaveChangesAsync(ct);

        return Ok(new ThreadMessageDto(
            ThreadDirection.Outbound,
            request.Channel,
            request.Body.Trim(),
            broadcast.CreatedAt,
            recipient.Status,
            recipient.StatusMessage,
            broadcast.Id));
    }

    // --- Monthly-fee one-click broadcast ----------------------------------

    private const string MonthlyFeeTemplateBaseName = "monthlyfee";

    /// <summary>Preview the monthly-fee broadcast: counts, template availability, and required
    /// variable shape so the admin UI knows what inputs to show before firing.</summary>
    [HttpGet("monthly-fee/preview")]
    public async Task<ActionResult<MonthlyFeePreviewDto>> MonthlyFeePreview(CancellationToken ct)
    {
        var target = new RecipientTarget(RecipientTargetKind.DynamicGroup, DynamicGroupKey: RecipientResolver.DynamicTrialOverParents);
        var resolved = await _resolver.ResolveAsync(target, ct);

        var enTemplate = await FindMonthlyFeeTemplateAsync(Language.English, ct);
        var esTemplate = await FindMonthlyFeeTemplateAsync(Language.Spanish, ct);

        // Variable shape is taken from whichever template exists. Spanish version is expected to
        // share the same positional variable layout.
        var sourceForVars = enTemplate ?? esTemplate;
        var variables = sourceForVars?.Variables
            .OrderBy(v => v.Position)
            .Select(v => new WhatsAppTemplateVariableDto(v.Id, v.Position, v.Label, v.Example, v.PropertyKey))
            .ToList() ?? new List<WhatsAppTemplateVariableDto>();

        // Suggest sensible defaults for the two variables on the monthly-fee template. The
        // approved templates use positional placeholders ({{1}} = date, {{2}} = phone), so
        // positional matching is the primary rule; label-keyword matching is the backup in case
        // the admin renamed labels in the Templates tab.
        var settings = await _db.MessagingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var suggested = new Dictionary<string, string>();
        var firstOfNextMonth = FirstOfNextMonth(DateTime.UtcNow);
        foreach (var v in variables)
        {
            var label = v.Label?.ToLowerInvariant() ?? string.Empty;
            var key = v.Position.ToString(CultureInfo.InvariantCulture);
            var isDate = v.Position == 1
                || label.Contains("date") || label.Contains("month") || label.Contains("due");
            var isPhone = v.Position == 2
                || label.Contains("zelle") || label.Contains("phone")
                || label.Contains("teléfono") || label.Contains("telefono");
            if (isDate)
            {
                suggested[key] = firstOfNextMonth;
            }
            else if (isPhone)
            {
                if (!string.IsNullOrWhiteSpace(settings?.ZellePhone))
                    suggested[key] = settings.ZellePhone!;
            }
        }

        return Ok(new MonthlyFeePreviewDto(
            RecipientCount: resolved.Recipients.Count,
            EnglishCount: resolved.Recipients.Count(r => (r.Language ?? Language.English) == Language.English),
            SpanishCount: resolved.Recipients.Count(r => r.Language == Language.Spanish),
            EnglishTemplateConfigured: enTemplate is not null,
            SpanishTemplateConfigured: esTemplate is not null,
            Variables: variables,
            SuggestedValues: suggested,
            EnglishTemplateName: enTemplate?.Name,
            SpanishTemplateName: esTemplate?.Name,
            EnglishPreviewText: enTemplate?.PreviewText,
            SpanishPreviewText: esTemplate?.PreviewText));
    }

    /// <summary>"MM/DD/YYYY" string for the first day of the month following <paramref name="now"/>.
    /// Used as the default due-date suggestion on the monthly-fee form.</summary>
    private static string FirstOfNextMonth(DateTime now)
    {
        var d = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        return d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Fire the monthly-fee broadcast: pulls trial-over parents in the active season,
    /// routes each one to the language-matching <c>monthlyfee_*</c> template, and returns the
    /// broadcast detail. Same fan-out pipeline as the regular Compose-tab template sends.</summary>
    [HttpPost("monthly-fee/send")]
    public async Task<ActionResult<BroadcastDetail>> SendMonthlyFee(
        [FromBody] SendMonthlyFeeRequest request, CancellationToken ct)
    {
        if (!_sender.IsAvailable(MessageChannel.WhatsApp))
            return BadRequest("WhatsApp not configured on this server.");

        var enTemplate = await FindMonthlyFeeTemplateAsync(Language.English, ct);
        var esTemplate = await FindMonthlyFeeTemplateAsync(Language.Spanish, ct);
        if (enTemplate is null && esTemplate is null)
            return BadRequest($"No {MonthlyFeeTemplateBaseName} templates configured. Add `{MonthlyFeeTemplateBaseName}_english` and `{MonthlyFeeTemplateBaseName}_spanish` under the Templates tab.");

        // Pick the primary (English by default; falls back to Spanish if only that one exists).
        // The send loop pairs to the other language automatically via FindPairAsync's base-name match.
        var primary = enTemplate ?? esTemplate!;
        var paired = primary == enTemplate ? esTemplate : enTemplate;

        var templateVars = (request.TemplateVariables ?? new())
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);
        var missing = primary.Variables
            .Select(v => v.Position.ToString(CultureInfo.InvariantCulture))
            .Where(key => !templateVars.ContainsKey(key) || string.IsNullOrWhiteSpace(templateVars[key]))
            .ToList();
        if (missing.Count > 0)
            return BadRequest($"Template variables missing: {string.Join(", ", missing)}.");

        var target = new RecipientTarget(RecipientTargetKind.DynamicGroup, DynamicGroupKey: RecipientResolver.DynamicTrialOverParents);
        var resolved = await _resolver.ResolveAsync(target, ct);
        if (resolved.Recipients.Count == 0)
            return BadRequest("No parents with the free-trial-over flag set; nothing to send.");

        var broadcast = new Broadcast
        {
            Channel = MessageChannel.WhatsApp,
            BodyEn = RenderTemplatePreview(primary.PreviewText, primary.Name, templateVars),
            BodyEs = paired is null ? null : RenderTemplatePreview(paired.PreviewText, paired.Name, templateVars),
            TargetLabel = $"Monthly fee — {resolved.Recipients.Count} parents (trial over)",
            WhatsAppTemplateId = primary.Id,
            TemplateVariablesJson = JsonSerializer.Serialize(templateVars),
        };
        foreach (var r in resolved.Recipients)
        {
            broadcast.Recipients.Add(new BroadcastRecipient
            {
                Name = r.Name,
                Phone = r.Phone,
                Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email,
                Language = r.Language ?? Language.English,
                Status = MessageDeliveryStatus.Pending
            });
        }
        _db.Broadcasts.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        // Skip recipients we know don't have WhatsApp, mirroring CreateBroadcast.
        var hasWhatsAppByPhone = resolved.Recipients
            .Where(r => r.HasWhatsApp.HasValue && !string.IsNullOrWhiteSpace(r.Phone))
            .GroupBy(r => r.Phone)
            .ToDictionary(g => g.Key, g => g.First().HasWhatsApp!.Value);

        foreach (var recipient in broadcast.Recipients)
        {
            if (hasWhatsAppByPhone.TryGetValue(recipient.Phone, out var has) && !has)
            {
                recipient.Status = MessageDeliveryStatus.Failed;
                recipient.StatusMessage = "Skipped: recipient does not have WhatsApp on file.";
                continue;
            }
            await SendWhatsAppTemplateRecipientAsync(recipient, primary, paired, templateVars, ct);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(broadcast));
    }

    private async Task<WhatsAppTemplate?> FindMonthlyFeeTemplateAsync(Language language, CancellationToken ct)
        => await FindLatestVersionedTemplateAsync(MonthlyFeeTemplateBaseName, language, ct);

    private const string TournamentTemplateBaseName = "tournamentparticipation";

    private async Task<WhatsAppTemplate?> FindTournamentTemplateAsync(Language language, CancellationToken ct)
        => await FindLatestVersionedTemplateAsync(TournamentTemplateBaseName, language, ct);

    private const string TournamentFeeTemplateBaseName = "tournamentfee";
    private const string LeagueFeeTemplateBaseName = "leaguefee";

    /// <summary>Resolves the fee-reminder template base for a tournament — Kind=Tournament uses
    /// <c>tournamentfee_*</c>, Kind=League uses <c>leaguefee_*</c>. Same versioning rules as
    /// the tournamentparticipation lookup.</summary>
    private async Task<WhatsAppTemplate?> FindFeeTemplateAsync(Tournament tournament, Language language, CancellationToken ct)
    {
        var baseName = tournament.Kind == TournamentKind.League
            ? LeagueFeeTemplateBaseName
            : TournamentFeeTemplateBaseName;
        return await FindLatestVersionedTemplateAsync(baseName, language, ct);
    }

    /// <summary>Picks the highest-versioned WhatsApp template matching the convention
    /// <c>{baseName}_{english|spanish|en|es}</c> (treated as v1) or
    /// <c>{baseName}v{N}_{english|spanish|en|es}</c> for vN. An optional underscore is allowed
    /// before the <c>v{N}</c> token, so both <c>monthlyfeev2_english</c> and
    /// <c>monthlyfee_v2_english</c> are accepted. Returns null when nothing matches — caller
    /// surfaces the "no template configured" error.
    ///
    /// Lets admins iterate template copy by uploading <c>{base}v2_english</c> alongside the
    /// existing v1; the next send automatically picks v2 without any code change.</summary>
    private async Task<WhatsAppTemplate?> FindLatestVersionedTemplateAsync(string baseName, Language language, CancellationToken ct)
    {
        var langSuffixes = language == Language.English
            ? new[] { "_english", "_en" }
            : new[] { "_spanish", "_es" };
        var pattern = $"^{System.Text.RegularExpressions.Regex.Escape(baseName)}(?:_?v(\\d+))?({string.Join("|", langSuffixes.Select(System.Text.RegularExpressions.Regex.Escape))})$";
        var rx = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var all = await _db.WhatsAppTemplates
            .Include(t => t.Variables)
            .Where(t => t.Language == language)
            .ToListAsync(ct);

        return all
            .Select(t => new { Template = t, Match = rx.Match(t.Name) })
            .Where(x => x.Match.Success)
            // No v{N} group = v1. Anything else parses the captured number; bigger wins.
            .OrderByDescending(x => x.Match.Groups[1].Success && int.TryParse(x.Match.Groups[1].Value, out var v) ? v : 1)
            .FirstOrDefault()?.Template;
    }

    /// <summary>Fans out a tournament confirmation request to every rostered player on the
    /// tournament's team — one Broadcast per player so each parent gets a message with their
    /// own kid's name baked into template parameter 2. Variables: 1 = formatted dates,
    /// 2 = player name, 3 = cost per player. The Broadcast carries this tournament's id so
    /// inbound WhatsApp replies are routed to <see cref="TournamentAttendance"/> by the webhook.</summary>
    [HttpPost("tournaments/{tournamentId:int}/send-confirmations")]
    public Task<ActionResult<SendTournamentConfirmationsResult>> SendTournamentConfirmations(
        int tournamentId, [FromBody] SendTournamentConfirmationsRequest? request, CancellationToken ct) =>
            SendTournamentTeamConfirmationsInternal(tournamentId, teamId: null, includePlayerIds: null, request?.Overrides, ct);

    /// <summary>Builds the side-by-side EN/ES preview shown in the admin's "Send confirmations"
    /// modal. Uses the first rostered player as a sample for variable 2 (player name); variables
    /// 1 (dates) and 3 (cost) come from the same formatter the actual send uses, so the preview
    /// matches what families receive byte-for-byte.</summary>
    [HttpGet("tournaments/{tournamentId:int}/teams/{teamId:int}/send-preview")]
    public async Task<ActionResult<TournamentSendPreviewDto>> GetTournamentSendPreview(
        int tournamentId, int teamId, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        if (tournament.StartDate is null)
            return BadRequest("Set the tournament's start date before previewing.");
        if (tournament.CostPerPlayer is null)
            return BadRequest("Set the cost per player before previewing.");

        var team = await _db.Teams
            .Include(t => t.Roster).ThenInclude(tp => tp.Player!).ThenInclude(p => p.ParentAccount!).ThenInclude(pa => pa.User)
            .FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (team.Roster.Count == 0)
            return BadRequest("This team has no rostered players.");

        var template = await FindTournamentTemplateAsync(Language.English, ct);
        if (template is null)
            return BadRequest($"No tournament template configured. Add `{TournamentTemplateBaseName}_english` and `{TournamentTemplateBaseName}_spanish` under Messaging → Templates first.");

        var samplePlayer = team.Roster
            .Where(tp => tp.Player != null)
            .OrderBy(tp => tp.Player!.LastName).ThenBy(tp => tp.Player!.FirstName)
            .First().Player!;
        var sampleName = $"{samplePlayer.FirstName} {samplePlayer.LastName}".Trim();
        var datesStr = FormatTournamentDates(tournament.StartDate.Value, tournament.EndDate);
        var costStr = tournament.CostPerPlayer.Value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        // Reuse the existing TemplatePreview action so the EN/ES rendering pipeline (paired
        // template lookup + phrase-dictionary translation for the variable values) is identical
        // to the Compose tab's preview.
        var props = BuildTournamentProperties(tournament, team, samplePlayer);
        var legacy = new Dictionary<int, string> { [1] = datesStr, [2] = sampleName, [3] = costStr };
        var sampleValues = BuildVariableValuesFromMapping(template, props, legacy);
        var previewResult = await TemplatePreview(new TemplatePreviewRequest
        {
            TemplateId = template.Id,
            Values = sampleValues,
        }, ct);
        if (previewResult.Result is not OkObjectResult ok || ok.Value is not TemplatePreviewResponse p)
            return BadRequest("Could not build the preview. Check the template configuration.");

        var variableDtos = template.Variables
            .OrderBy(v => v.Position)
            .Select(v => new TournamentSendPreviewVariableDto(
                v.Position,
                v.Label,
                v.PropertyKey,
                sampleValues.TryGetValue(v.Position.ToString(System.Globalization.CultureInfo.InvariantCulture), out var val) ? val : ""))
            .ToList();

        return Ok(new TournamentSendPreviewDto(
            SamplePlayerName: sampleName,
            DatesValue: datesStr,
            CostValue: costStr,
            RosterCount: team.Roster.Count,
            TemplateId: template.Id,
            Variables: variableDtos,
            EnglishTemplateName: p.English.TemplateName,
            EnglishRendered: p.English.Rendered,
            SpanishTemplateName: p.Spanish.TemplateName,
            SpanishRendered: p.Spanish.Rendered));
    }

    /// <summary>Per-team confirmation send for multi-team tournaments. Each team in the
    /// tournament has its own "Send confirmations" button — this fans out to that team's
    /// rostered players, tagging the Broadcasts with the tournament id so inbound replies
    /// flow to <see cref="TournamentAttendance"/>.</summary>
    [HttpPost("tournaments/{tournamentId:int}/teams/{teamId:int}/send-confirmations")]
    public Task<ActionResult<SendTournamentConfirmationsResult>> SendTournamentTeamConfirmations(
        int tournamentId, int teamId, [FromBody] SendTournamentConfirmationsRequest? request, CancellationToken ct) =>
        SendTournamentTeamConfirmationsInternal(tournamentId, teamId, includePlayerIds: null, request?.Overrides, ct);

    /// <summary>Per-team resend: same fan-out as send-confirmations but scoped to a subset of
    /// the roster computed from the filter checkboxes (failed delivery / never delivered / no
    /// response). The admin picks which buckets to include; we union the matching player ids
    /// and resend only to those.</summary>
    [HttpPost("tournaments/{tournamentId:int}/teams/{teamId:int}/resend-confirmations")]
    public async Task<ActionResult<SendTournamentConfirmationsResult>> ResendTournamentTeamConfirmations(
        int tournamentId, int teamId,
        [FromBody] ResendTournamentConfirmationsRequest request,
        CancellationToken ct)
    {
        if (!request.IncludeFailed && !request.IncludeUndelivered && !request.IncludeNoResponse)
            return BadRequest("Pick at least one re-send filter.");

        var rosterIds = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => tp.PlayerId)
            .ToListAsync(ct);
        if (rosterIds.Count == 0)
            return BadRequest("This team has no rostered players.");

        // Last broadcast per player for this tournament (PlayerId-tagged sends only — legacy
        // sends without PlayerId are treated as "no broadcast on record").
        var lastBroadcastByPlayer = await _db.Broadcasts
            .Where(b => b.TournamentId == tournamentId && b.PlayerId != null && rosterIds.Contains(b.PlayerId!.Value))
            .GroupBy(b => b.PlayerId!.Value)
            .Select(g => new { PlayerId = g.Key, Last = g.OrderByDescending(b => b.CreatedAt).Select(b => new { b.Id, b.CreatedAt }).First() })
            .ToListAsync(ct);
        var lastBroadcastIds = lastBroadcastByPlayer.Select(x => x.Last.Id).ToList();
        var recipientsByBroadcast = await _db.BroadcastRecipients
            .Where(r => lastBroadcastIds.Contains(r.BroadcastId))
            .GroupBy(r => r.BroadcastId)
            .Select(g => new { BroadcastId = g.Key, Rows = g.Select(r => new { r.Status, r.ErrorCode }).ToList() })
            .ToDictionaryAsync(x => x.BroadcastId, x => x.Rows, ct);
        var lastStatusesByPlayer = lastBroadcastByPlayer
            .ToDictionary(
                x => x.PlayerId,
                x => recipientsByBroadcast.TryGetValue(x.Last.Id, out var rs)
                    ? rs.Select(r => r.Status).ToList()
                    : new List<MessageDeliveryStatus>());
        // Rate-limit (WhatsApp 131049) cool-down: any player whose last broadcast was within
        // the backoff window AND had a 131049 on any recipient. We exclude these from the
        // Failed bucket to honor Meta's "do not retry immediately" guidance.
        var rateLimitCutoff = DateTime.UtcNow - WhatsAppRateLimitBackoff;
        var rateLimitedPlayers = lastBroadcastByPlayer
            .Where(x => x.Last.CreatedAt > rateLimitCutoff
                && recipientsByBroadcast.TryGetValue(x.Last.Id, out var rs)
                && rs.Any(r => r.ErrorCode == WhatsAppRateLimitErrorCode))
            .Select(x => x.PlayerId)
            .ToHashSet();

        var pendingAttendance = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId
                && rosterIds.Contains(a.PlayerId)
                && a.Status == AttendanceStatus.Pending)
            .Select(a => a.PlayerId)
            .ToHashSetAsync(ct);
        // Players with NO TournamentAttendance row at all also count as Pending (never replied).
        var hasAttendanceRow = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId && rosterIds.Contains(a.PlayerId))
            .Select(a => a.PlayerId)
            .ToHashSetAsync(ct);

        // Buckets are disjoint: each Pending player falls into exactly one based on last
        // delivery. Players who already responded (Confirmed/Declined/Maybe) are skipped.
        // Rate-limited players (131049 within backoff window) are carved out of Failed and
        // counted separately so the admin sees how many were intentionally not retried.
        var include = new HashSet<int>();
        int rateLimitedSkipped = 0;
        foreach (var pid in rosterIds)
        {
            var isPending = pendingAttendance.Contains(pid) || !hasAttendanceRow.Contains(pid);
            if (!isPending) continue;
            if (!lastStatusesByPlayer.TryGetValue(pid, out var statuses) || statuses.Count == 0)
            {
                // No PlayerId-tagged broadcast on record (player added after the original send,
                // or only legacy broadcasts exist).
                if (request.IncludeUndelivered) include.Add(pid);
                continue;
            }
            var anySuccess = statuses.Any(s => s == MessageDeliveryStatus.Sent || s == MessageDeliveryStatus.Delivered);
            if (anySuccess)
            {
                // Family received the message but hasn't replied yet.
                if (request.IncludeNoResponse) include.Add(pid);
            }
            else
            {
                if (rateLimitedPlayers.Contains(pid))
                {
                    // 131049 hit within the backoff window — do NOT retry immediately, but
                    // surface the count so the admin sees the skip was intentional.
                    if (request.IncludeFailed) rateLimitedSkipped++;
                    continue;
                }
                // Every family recipient failed/undelivered (or only intermediate statuses with
                // no success) — treat as Failed.
                if (request.IncludeFailed) include.Add(pid);
            }
        }

        var result = await SendTournamentTeamConfirmationsInternal(tournamentId, teamId, include, overrides: null, ct);
        if (result.Result is ObjectResult ok && ok.Value is SendTournamentConfirmationsResult inner)
        {
            return Ok(inner with { RateLimitedSkipped = rateLimitedSkipped });
        }
        return result;
    }

    private async Task<ActionResult<SendTournamentConfirmationsResult>> SendTournamentTeamConfirmationsInternal(
        int tournamentId, int? teamId, IReadOnlySet<int>? includePlayerIds,
        IReadOnlyDictionary<int, string>? overrides, CancellationToken ct)
    {
        if (!_sender.IsAvailable(MessageChannel.WhatsApp))
            return BadRequest("WhatsApp not configured on this server.");

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        if (tournament.StartDate is null)
            return BadRequest("Set the tournament's start date before sending confirmations.");
        if (tournament.CostPerPlayer is null)
            return BadRequest("Set the cost per player before sending confirmations.");

        // Per-team route: explicit team id. Legacy route: fall back to Tournament.TeamId so
        // pre-multi-team tournaments keep working when the new UI hasn't migrated them.
        var effectiveTeamId = teamId ?? tournament.TeamId;
        if (effectiveTeamId is null)
            return BadRequest("This tournament has no team. Add a team to the tournament first.");

        var team = await _db.Teams
            .Include(tt => tt.Roster).ThenInclude(tp => tp.Player!).ThenInclude(p => p.ParentAccount!).ThenInclude(pa => pa.User)
            .FirstOrDefaultAsync(tt => tt.Id == effectiveTeamId, ct);
        if (team is null) return NotFound();
        if (team.Roster.Count == 0)
            return BadRequest("This team has no rostered players.");

        var enTemplate = await FindTournamentTemplateAsync(Language.English, ct);
        var esTemplate = await FindTournamentTemplateAsync(Language.Spanish, ct);
        if (enTemplate is null && esTemplate is null)
            return BadRequest($"No tournament templates configured. Add `{TournamentTemplateBaseName}_english` and `{TournamentTemplateBaseName}_spanish` under Messaging → Templates first.");
        var primary = enTemplate ?? esTemplate!;

        var datesStr = FormatTournamentDates(tournament.StartDate.Value, tournament.EndDate);
        var costStr = tournament.CostPerPlayer.Value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        // One batch id per fan-out so the History view can collapse the per-player rows into
        // one summary line ("Tournament X — confirmations (N players)").
        var batchId = Guid.NewGuid();
        int sent = 0, skipped = 0, targeted = 0;
        foreach (var tp in team.Roster)
        {
            var player = tp.Player;
            if (player is null) { skipped++; continue; }
            if (includePlayerIds is not null && !includePlayerIds.Contains(player.Id)) continue;
            targeted++;
            // Resolve per-player properties + variable values via the template's mapping.
            // Falls back to the legacy hard-coded positions when the template hasn't been
            // mapped yet, so this is safe to call on any tournament template.
            var props = BuildTournamentProperties(tournament, team, player);
            var legacy = new Dictionary<int, string>
            {
                [1] = datesStr,
                [2] = $"{player.FirstName} {player.LastName}".Trim(),
                [3] = costStr,
            };
            var values = BuildVariableValuesFromMapping(primary, props, legacy);
            // Apply admin overrides from the preview modal. Variables mapped to a per-player
            // property (PropertyKey starts with "player.") are left as the computed value so
            // each recipient still gets their own name; everything else (dates, cost, team
            // name, etc.) takes the admin's edited string verbatim.
            if (overrides is not null && overrides.Count > 0)
            {
                foreach (var v in primary.Variables)
                {
                    if (!overrides.TryGetValue(v.Position, out var edited)) continue;
                    if (v.PropertyKey is not null && v.PropertyKey.StartsWith("player.", StringComparison.Ordinal)) continue;
                    values[v.Position.ToString(System.Globalization.CultureInfo.InvariantCulture)] = edited;
                }
            }
            var req = new CreateBroadcastRequest
            {
                Channel = MessageChannel.WhatsApp,
                WhatsAppTemplateId = primary.Id,
                TemplateVariables = values,
                TournamentId = tournamentId,
                PlayerId = player.Id,
                BatchId = batchId,
                Target = new BroadcastTargetDto
                {
                    Kind = RecipientTargetKindDto.DynamicGroup,
                    DynamicGroupKey = $"{RecipientResolver.DynamicPlayerPrefix}{player.Id}",
                },
            };
            var result = await CreateBroadcast(req, ct);
            if (result.Result is ObjectResult ok && ok.StatusCode == 200) sent++;
            else skipped++;
        }

        var total = includePlayerIds is null ? team.Roster.Count : targeted;
        return Ok(new SendTournamentConfirmationsResult(sent, skipped, total, null));
    }

    /// <summary>Bilingual EN/ES preview for the fee-reminder send. Mirrors GetTournamentSendPreview
    /// but uses the fee template lookup (tournamentfee_*/leaguefee_*) and samples the first
    /// unpaid rostered player so the preview matches the first send byte-for-byte.</summary>
    [HttpGet("tournaments/{tournamentId:int}/teams/{teamId:int}/fee-send-preview")]
    public async Task<ActionResult<TournamentSendPreviewDto>> GetTournamentFeeSendPreview(
        int tournamentId, int teamId, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        if (tournament.StartDate is null)
            return BadRequest("Set the tournament's start date before previewing.");
        if (tournament.CostPerPlayer is null)
            return BadRequest("Set the cost per player before previewing.");

        var team = await _db.Teams
            .Include(t => t.Roster).ThenInclude(tp => tp.Player!).ThenInclude(p => p.ParentAccount!).ThenInclude(pa => pa.User)
            .FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (team.Roster.Count == 0)
            return BadRequest("This team has no rostered players.");

        var paidPlayerIds = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId && a.Paid)
            .Select(a => a.PlayerId)
            .ToHashSetAsync(ct);
        var unpaidRoster = team.Roster
            .Where(tp => tp.Player is not null && !paidPlayerIds.Contains(tp.PlayerId))
            .ToList();
        if (unpaidRoster.Count == 0)
            return BadRequest("Every rostered player is already marked Paid — nothing to preview.");

        var template = await FindFeeTemplateAsync(tournament, Language.English, ct);
        if (template is null)
        {
            var baseName = tournament.Kind == TournamentKind.League ? LeagueFeeTemplateBaseName : TournamentFeeTemplateBaseName;
            return BadRequest($"No fee templates configured. Add `{baseName}_english` and `{baseName}_spanish` under Messaging → Templates first.");
        }

        var samplePlayer = unpaidRoster
            .OrderBy(tp => tp.Player!.LastName).ThenBy(tp => tp.Player!.FirstName)
            .First().Player!;
        var sampleName = $"{samplePlayer.FirstName} {samplePlayer.LastName}".Trim();
        var datesStr = FormatTournamentDates(tournament.StartDate.Value, tournament.EndDate);
        var costStr = tournament.CostPerPlayer.Value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        var props = BuildTournamentProperties(tournament, team, samplePlayer);
        var legacy = new Dictionary<int, string> { [1] = datesStr, [2] = sampleName, [3] = costStr };
        var sampleValues = BuildVariableValuesFromMapping(template, props, legacy);
        var previewResult = await TemplatePreview(new TemplatePreviewRequest
        {
            TemplateId = template.Id,
            Values = sampleValues,
        }, ct);
        if (previewResult.Result is not OkObjectResult ok || ok.Value is not TemplatePreviewResponse p)
            return BadRequest("Could not build the preview. Check the template configuration.");

        var variableDtos = template.Variables
            .OrderBy(v => v.Position)
            .Select(v => new TournamentSendPreviewVariableDto(
                v.Position,
                v.Label,
                v.PropertyKey,
                sampleValues.TryGetValue(v.Position.ToString(System.Globalization.CultureInfo.InvariantCulture), out var val) ? val : ""))
            .ToList();

        return Ok(new TournamentSendPreviewDto(
            SamplePlayerName: sampleName,
            DatesValue: datesStr,
            CostValue: costStr,
            RosterCount: unpaidRoster.Count,
            TemplateId: template.Id,
            Variables: variableDtos,
            EnglishTemplateName: p.English.TemplateName,
            EnglishRendered: p.English.Rendered,
            SpanishTemplateName: p.Spanish.TemplateName,
            SpanishRendered: p.Spanish.Rendered));
    }

    /// <summary>Per-team fee-reminder send. Mirrors the confirmations fan-out but uses the
    /// <c>tournamentfee_*</c> (Kind=Tournament) or <c>leaguefee_*</c> (Kind=League) template
    /// and skips any rostered player whose <see cref="Domain.TournamentAttendance.Paid"/> is
    /// true. Per-player Broadcast + BatchId tagging matches the confirmations send so the
    /// History view collapses the rows. Accepts inline overrides from the preview modal —
    /// same skip-on-player.* rule as confirmations so each recipient still gets their own name.</summary>
    [HttpPost("tournaments/{tournamentId:int}/teams/{teamId:int}/send-fee-reminders")]
    public async Task<ActionResult<SendTournamentConfirmationsResult>> SendTournamentTeamFeeReminders(
        int tournamentId, int teamId, [FromBody] SendTournamentConfirmationsRequest? request, CancellationToken ct)
    {
        if (!_sender.IsAvailable(MessageChannel.WhatsApp))
            return BadRequest("WhatsApp not configured on this server.");

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        if (tournament.StartDate is null)
            return BadRequest("Set the tournament's start date before sending fee reminders.");
        if (tournament.CostPerPlayer is null)
            return BadRequest("Set the cost per player before sending fee reminders.");

        var team = await _db.Teams
            .Include(tt => tt.Roster).ThenInclude(tp => tp.Player)
            .FirstOrDefaultAsync(tt => tt.Id == teamId, ct);
        if (team is null) return NotFound();
        if (team.Roster.Count == 0) return BadRequest("This team has no rostered players.");

        var paidPlayerIds = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId && a.Paid)
            .Select(a => a.PlayerId)
            .ToHashSetAsync(ct);
        var unpaidRoster = team.Roster
            .Where(tp => tp.Player is not null && !paidPlayerIds.Contains(tp.PlayerId))
            .ToList();
        if (unpaidRoster.Count == 0)
            return Ok(new SendTournamentConfirmationsResult(0, 0, 0, "Every rostered player is already marked Paid."));

        var enTemplate = await FindFeeTemplateAsync(tournament, Language.English, ct);
        var esTemplate = await FindFeeTemplateAsync(tournament, Language.Spanish, ct);
        if (enTemplate is null && esTemplate is null)
        {
            var baseName = tournament.Kind == TournamentKind.League ? LeagueFeeTemplateBaseName : TournamentFeeTemplateBaseName;
            return BadRequest($"No fee templates configured. Add `{baseName}_english` and `{baseName}_spanish` under Messaging → Templates first.");
        }
        var primary = enTemplate ?? esTemplate!;

        var datesStr = FormatTournamentDates(tournament.StartDate.Value, tournament.EndDate);
        var costStr = tournament.CostPerPlayer.Value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        var batchId = Guid.NewGuid();
        int sent = 0, skipped = 0;
        foreach (var tp in unpaidRoster)
        {
            var player = tp.Player!;
            var props = BuildTournamentProperties(tournament, team, player);
            var legacy = new Dictionary<int, string>
            {
                [1] = datesStr,
                [2] = $"{player.FirstName} {player.LastName}".Trim(),
                [3] = costStr,
            };
            var values = BuildVariableValuesFromMapping(primary, props, legacy);
            // Apply admin overrides from the preview modal (same rule as confirmations send):
            // skip variables mapped to a per-player property so each recipient still gets
            // their own name; everything else takes the admin's edited value verbatim.
            if (request?.Overrides is { Count: > 0 })
            {
                foreach (var v in primary.Variables)
                {
                    if (!request.Overrides.TryGetValue(v.Position, out var edited)) continue;
                    if (v.PropertyKey is not null && v.PropertyKey.StartsWith("player.", StringComparison.Ordinal)) continue;
                    values[v.Position.ToString(System.Globalization.CultureInfo.InvariantCulture)] = edited;
                }
            }
            var req = new CreateBroadcastRequest
            {
                Channel = MessageChannel.WhatsApp,
                WhatsAppTemplateId = primary.Id,
                TemplateVariables = values,
                TournamentId = tournamentId,
                PlayerId = player.Id,
                BatchId = batchId,
                Target = new BroadcastTargetDto
                {
                    Kind = RecipientTargetKindDto.DynamicGroup,
                    DynamicGroupKey = $"{RecipientResolver.DynamicPlayerPrefix}{player.Id}",
                },
            };
            var result = await CreateBroadcast(req, ct);
            if (result.Result is ObjectResult ok && ok.StatusCode == 200) sent++;
            else skipped++;
        }
        return Ok(new SendTournamentConfirmationsResult(sent, skipped, unpaidRoster.Count, null));
    }

    /// <summary>Per-property resolved values for a tournament confirmation send. Keys here match
    /// the property registry exposed by <see cref="TemplatePropertyRegistry"/>; mapped variables
    /// pull their value from this dict at fan-out time.</summary>
    private Dictionary<string, string> BuildTournamentProperties(
        Tournament tournament, Domain.Team team, Domain.Player player)
    {
        var us = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var props = new Dictionary<string, string>
        {
            // --- Tournament / League ---
            ["tournament.name"] = tournament.Name,
            ["tournament.kind"] = tournament.Kind == TournamentKind.League ? "League" : "Tournament",
            ["tournament.dates"] = tournament.StartDate is null ? string.Empty
                : FormatTournamentDates(tournament.StartDate.Value, tournament.EndDate),
            ["tournament.startDate"] = tournament.StartDate?.ToString("MMM d, yyyy", us) ?? string.Empty,
            ["tournament.endDate"] = tournament.EndDate?.ToString("MMM d, yyyy", us) ?? string.Empty,
            ["tournament.startDateLong"] = tournament.StartDate?.ToString("MMMM d, yyyy", us) ?? string.Empty,
            ["tournament.endDateLong"] = tournament.EndDate?.ToString("MMMM d, yyyy", us) ?? string.Empty,
            ["tournament.startDateShort"] = tournament.StartDate?.ToString("MM/dd", us) ?? string.Empty,
            ["tournament.endDateShort"] = tournament.EndDate?.ToString("MM/dd", us) ?? string.Empty,
            ["tournament.startDayOfWeek"] = tournament.StartDate?.ToDateTime(TimeOnly.MinValue).ToString("dddd", us) ?? string.Empty,
            ["tournament.endDayOfWeek"] = tournament.EndDate?.ToDateTime(TimeOnly.MinValue).ToString("dddd", us) ?? string.Empty,
            ["tournament.costPerPlayer"] = tournament.CostPerPlayer?.ToString("C", us) ?? string.Empty,
            ["tournament.costPerPlayerPlain"] = tournament.CostPerPlayer?.ToString("0.00", us) ?? string.Empty,
            ["tournament.totalCost"] = tournament.TotalCost?.ToString("C", us) ?? string.Empty,
            ["tournament.totalCostPlain"] = tournament.TotalCost?.ToString("0.00", us) ?? string.Empty,
            // --- Team ---
            ["team.name"] = team.Name,
            // --- Player ---
            ["player.firstName"] = player.FirstName,
            ["player.lastName"] = player.LastName,
            ["player.fullName"] = $"{player.FirstName} {player.LastName}".Trim(),
        };

        // --- Parent / guardian (resolved from the player's primary account when loaded) ---
        var parent = player.ParentAccount;
        props["parent.firstName"] = parent?.FirstName ?? string.Empty;
        props["parent.lastName"] = parent?.LastName ?? string.Empty;
        props["parent.fullName"] = parent is null ? string.Empty : $"{parent.FirstName} {parent.LastName}".Trim();
        props["parent.cellPhone"] = parent?.CellPhone ?? string.Empty;
        props["parent.email"] = parent?.User?.Email ?? string.Empty;

        // --- App-level (admin settings) ---
        // Lazy-load from MessagingSettings exactly once per call. The build is invoked per
        // player so multiple lookups inside a fan-out would add up; cache via a small private
        // helper that fetches once per controller scope.
        props["app.zellePhone"] = _cachedZellePhone ??= ResolveZellePhone();
        props["app.activeSeason"] = _app.ActiveSeason ?? string.Empty;
        props["app.publicBaseUrl"] = _app.PublicBaseUrl?.TrimEnd('/') ?? string.Empty;
        return props;
    }

    /// <summary>Per-request cache so the per-player fan-out doesn't re-query MessagingSettings
    /// for every recipient. Set on first call to <see cref="BuildTournamentProperties"/>.</summary>
    private string? _cachedZellePhone;

    private string ResolveZellePhone()
    {
        var s = _db.MessagingSettings.AsNoTracking().FirstOrDefault();
        return s?.ZellePhone ?? string.Empty;
    }

    /// <summary>Walks the template's variables and fills each one from the resolved property
    /// dict using <see cref="WhatsAppTemplateVariable.PropertyKey"/>. Falls back to the
    /// caller's positional defaults for variables that haven't been mapped yet (so legacy
    /// templates still send correctly until admin assigns mappings).</summary>
    private static Dictionary<string, string> BuildVariableValuesFromMapping(
        WhatsAppTemplate template,
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyDictionary<int, string> legacyByPosition)
    {
        var values = new Dictionary<string, string>();
        foreach (var v in template.Variables.OrderBy(v => v.Position))
        {
            var key = v.Position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(v.PropertyKey) &&
                properties.TryGetValue(v.PropertyKey, out var resolved) &&
                !string.IsNullOrEmpty(resolved))
            {
                values[key] = resolved;
            }
            else if (legacyByPosition.TryGetValue(v.Position, out var legacy))
            {
                values[key] = legacy;
            }
            else
            {
                // Placeholder so the broadcast validator (every variable must be non-empty)
                // doesn't reject the send. Surfaces to the admin as a literal em-dash in the
                // preview — a clear signal that the variable needs a mapping.
                values[key] = "—";
            }
        }
        return values;
    }

    /// <summary>"May 31, 2026" for a single day, "May 31 – Jun 2, 2026" when the range stays in
    /// one year, or full "Dec 30, 2025 – Jan 2, 2026" across years. Matches the seeded phrase
    /// dictionary's short-month entries so the Spanish render translates the month names too.</summary>
    private static string FormatTournamentDates(DateOnly start, DateOnly? end)
    {
        var us = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var startDt = start.ToDateTime(TimeOnly.MinValue);
        if (end is null || end.Value == start)
            return startDt.ToString("MMM d, yyyy", us);
        var endDt = end.Value.ToDateTime(TimeOnly.MinValue);
        // Same month + year → "Jun 5–12, 2026" (don't repeat the month name).
        if (startDt.Year == endDt.Year && startDt.Month == endDt.Month)
            return $"{startDt.ToString("MMM d", us)}–{endDt.Day} {startDt.Year}";
        // Same year, different months → "Jun 28 – Jul 4, 2026".
        if (startDt.Year == endDt.Year)
            return $"{startDt.ToString("MMM d", us)} – {endDt.ToString("MMM d, yyyy", us)}";
        return $"{startDt.ToString("MMM d, yyyy", us)} – {endDt.ToString("MMM d, yyyy", us)}";
    }

    [HttpGet("broadcasts")]
    public async Task<ActionResult<IEnumerable<BroadcastSummary>>> ListBroadcasts(CancellationToken ct)
    {
        // Pull recent broadcasts then group fan-out batches (one BatchId = N per-player rows)
        // into a single summary so the History view doesn't drown in per-player rows. Take a
        // wider raw window so we don't truncate the children of a large batch.
        var raw = await _db.Broadcasts
            .OrderByDescending(b => b.CreatedAt)
            .Take(2000)
            .Select(b => new
            {
                b.Id,
                b.Channel,
                b.BodyEn,
                b.BodyEs,
                b.SubjectEn,
                b.SubjectEs,
                b.TargetLabel,
                b.CreatedAt,
                b.BatchId,
                Total = b.Recipients.Count,
                Queued = b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Queued || r.Status == MessageDeliveryStatus.Sent || r.Status == MessageDeliveryStatus.Pending),
                Delivered = b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Delivered),
                Failed = b.Recipients.Count(r => r.Status == MessageDeliveryStatus.Failed || r.Status == MessageDeliveryStatus.Undelivered),
                RateLimited = b.Recipients.Count(r => r.ErrorCode == WhatsAppRateLimitErrorCode),
            })
            .ToListAsync(ct);

        var items = raw
            .GroupBy(b => b.BatchId.HasValue ? $"batch:{b.BatchId.Value}" : $"id:{b.Id}")
            .Select(g =>
            {
                var head = g.OrderByDescending(x => x.CreatedAt).First();
                return new BroadcastSummary(
                    head.Id,
                    head.Channel,
                    head.BodyEn,
                    head.BodyEs,
                    head.SubjectEn,
                    head.SubjectEs,
                    head.TargetLabel,
                    g.Max(x => x.CreatedAt),
                    g.Sum(x => x.Total),
                    g.Sum(x => x.Queued),
                    g.Sum(x => x.Delivered),
                    g.Sum(x => x.Failed),
                    head.BatchId,
                    g.Count(),
                    g.Sum(x => x.RateLimited));
            })
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToList();
        return Ok(items);
    }

    /// <summary>WhatsApp per-user marketing template rate limit code. Meta's recommended
    /// remediation is to retry in increasing intervals — never immediately. The resend flow
    /// uses this to carve a rate-limited bucket out of the generic Failed bucket.</summary>
    private const string WhatsAppRateLimitErrorCode = "131049";

    /// <summary>How long after a 131049 failure to keep treating that recipient as
    /// "rate-limited, do not retry yet". Meta doesn't publish the actual cap, so this is a
    /// conservative starting heuristic — the admin can still manually re-send earlier.</summary>
    private static readonly TimeSpan WhatsAppRateLimitBackoff = TimeSpan.FromHours(24);

    /// <summary>Returns all per-player child broadcasts for a fan-out batch, with their
    /// recipients flattened into one list. Used by the History view's batch row expand.</summary>
    [HttpGet("batches/{batchId:guid}")]
    public async Task<ActionResult<BroadcastDetail>> GetBatch(Guid batchId, CancellationToken ct)
    {
        var rows = await _db.Broadcasts
            .Include(b => b.Recipients)
            .Where(b => b.BatchId == batchId)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);
        if (rows.Count == 0) return NotFound();
        var head = rows[0];
        var recipients = rows
            .SelectMany(b => b.Recipients.Select(r => new BroadcastRecipientDto(
                r.Id, r.Name, r.Phone, r.Email, r.Language, r.Status, r.StatusMessage, r.TwilioSid, r.TemplateUsed, r.ErrorCode)))
            .ToList();
        return Ok(new BroadcastDetail(
            head.Id,
            head.Channel,
            head.BodyEn,
            head.BodyEs,
            head.SubjectEn,
            head.SubjectEs,
            head.TargetLabel,
            head.CreatedAt,
            recipients));
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
        // Group siblings by base name so each template can surface its opposite-language pair.
        var byBase = items.GroupBy(t => BaseName(t.Name)).ToDictionary(g => g.Key, g => g.ToList());
        return Ok(items.Select(t => ToDto(t, FindPairFromGroup(t, byBase))));
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
            Context = request.Context,
            Variables = MapVariables(request.Variables)
        };
        _db.WhatsAppTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(template, await FindPairAsync(template, ct)));
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
        template.Context = request.Context;
        // Wipe existing variables and replace — simpler than diffing for a small list.
        _db.WhatsAppTemplateVariables.RemoveRange(template.Variables);
        template.Variables = MapVariables(request.Variables);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(template, await FindPairAsync(template, ct)));
    }

    /// <summary>Lists the property registry for a given <see cref="TemplateContext"/>. Drives
    /// the admin's per-variable "Map to" dropdown so they don't have to memorize key strings.</summary>
    [HttpGet("template-properties/{context}")]
    public ActionResult<IEnumerable<TemplatePropertyDto>> ListTemplateProperties(TemplateContext context)
    {
        var props = TemplatePropertyRegistry.ForContext(context)
            .Select(p => new TemplatePropertyDto(p.Key, p.Label))
            .ToList();
        return Ok(props);
    }

    /// <summary>Hard-coded list of the contexts the admin can pick from for a template, used
    /// to populate the Context dropdown on the Templates tab.</summary>
    [HttpGet("template-contexts")]
    public ActionResult<IEnumerable<TemplateContextOptionDto>> ListTemplateContexts()
    {
        var opts = new[]
        {
            new TemplateContextOptionDto(TemplateContext.FreeForm, "Free-form (admin fills variables manually)"),
            new TemplateContextOptionDto(TemplateContext.TournamentConfirmation, "Tournament confirmation"),
            new TemplateContextOptionDto(TemplateContext.EventReminder, "Event reminder (game/practice) — coming soon"),
            new TemplateContextOptionDto(TemplateContext.EventCancellation, "Event cancellation — coming soon"),
            new TemplateContextOptionDto(TemplateContext.MonthlyFee, "Monthly fee — coming soon"),
        };
        return Ok(opts);
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

    // --- Email templates ---

    [HttpGet("email-templates")]
    public async Task<ActionResult<IEnumerable<EmailTemplateDto>>> ListEmailTemplates(CancellationToken ct)
    {
        var items = await _db.EmailTemplates
            .Include(t => t.Variables)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        var byBase = items.GroupBy(t => BaseName(t.Name)).ToDictionary(g => g.Key, g => g.ToList());
        return Ok(items.Select(t => ToEmailDto(t, FindEmailPairFromGroup(t, byBase))));
    }

    [HttpPost("email-templates")]
    public async Task<ActionResult<EmailTemplateDto>> CreateEmailTemplate(
        [FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var subject = request.Subject.Trim();
        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(subject)) return BadRequest("Subject is required.");
        if (string.IsNullOrWhiteSpace(body)) return BadRequest("Body is required.");
        if (await _db.EmailTemplates.AnyAsync(t => t.Name == name, ct))
            return Conflict($"An email template named '{name}' already exists.");

        var template = new EmailTemplate
        {
            Name = name,
            Language = request.Language,
            Description = request.Description?.Trim(),
            Subject = subject,
            Body = body,
            Variables = MapEmailVariables(request.Variables)
        };
        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return Ok(ToEmailDto(template, await FindEmailPairAsync(template, ct)));
    }

    [HttpPut("email-templates/{id:int}")]
    public async Task<ActionResult<EmailTemplateDto>> UpdateEmailTemplate(
        int id, [FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
    {
        var template = await _db.EmailTemplates
            .Include(t => t.Variables)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        var name = request.Name.Trim();
        var subject = request.Subject.Trim();
        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(subject)) return BadRequest("Subject is required.");
        if (string.IsNullOrWhiteSpace(body)) return BadRequest("Body is required.");
        if (await _db.EmailTemplates.AnyAsync(t => t.Name == name && t.Id != id, ct))
            return Conflict($"An email template named '{name}' already exists.");

        template.Name = name;
        template.Language = request.Language;
        template.Description = request.Description?.Trim();
        template.Subject = subject;
        template.Body = body;
        template.UpdatedAt = DateTime.UtcNow;
        _db.EmailTemplateVariables.RemoveRange(template.Variables);
        template.Variables = MapEmailVariables(request.Variables);
        await _db.SaveChangesAsync(ct);
        return Ok(ToEmailDto(template, await FindEmailPairAsync(template, ct)));
    }

    [HttpDelete("email-templates/{id:int}")]
    public async Task<IActionResult> DeleteEmailTemplate(int id, CancellationToken ct)
    {
        var template = await _db.EmailTemplates.FindAsync(new object?[] { id }, ct);
        if (template is null) return NotFound();
        _db.EmailTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Messaging settings (auto-reply text + toggle) ---

    [HttpGet("settings")]
    public async Task<ActionResult<MessagingSettingsDto>> GetSettings(CancellationToken ct)
    {
        var s = await GetOrCreateSettingsAsync(ct);
        return Ok(new MessagingSettingsDto(s.AutoReplyEnabled, s.AutoReplyTextEn, s.AutoReplyTextEs, s.ZellePhone, s.UpdatedAt));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<MessagingSettingsDto>> UpdateSettings(
        [FromBody] SaveMessagingSettingsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AutoReplyTextEn) || string.IsNullOrWhiteSpace(request.AutoReplyTextEs))
            return BadRequest("Both English and Spanish auto-reply texts are required.");

        var s = await GetOrCreateSettingsAsync(ct);
        s.AutoReplyEnabled = request.AutoReplyEnabled;
        s.AutoReplyTextEn = request.AutoReplyTextEn.Trim();
        s.AutoReplyTextEs = request.AutoReplyTextEs.Trim();
        s.ZellePhone = string.IsNullOrWhiteSpace(request.ZellePhone) ? null : request.ZellePhone.Trim();
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new MessagingSettingsDto(s.AutoReplyEnabled, s.AutoReplyTextEn, s.AutoReplyTextEs, s.ZellePhone, s.UpdatedAt));
    }

    private async Task<MessagingSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var existing = await _db.MessagingSettings.FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        // Race-safe enough for the singleton: only Admin endpoints write here, and the migration
        // pre-seeds Id=1, so this fallback path mostly handles fresh-db edge cases.
        var fresh = new MessagingSettings();
        _db.MessagingSettings.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
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

    /// <summary>Renders the bilingual side-by-side preview for the template Compose modal.
    /// Returns each language's rendered text, marking which side comes from an approved template
    /// vs. dictionary-translated values vs. unavailable.</summary>
    [HttpPost("template-preview")]
    public async Task<ActionResult<TemplatePreviewResponse>> TemplatePreview(
        [FromBody] TemplatePreviewRequest request, CancellationToken ct)
    {
        var template = await _db.WhatsAppTemplates
            .Include(t => t.Variables)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (template is null) return NotFound();
        var pair = await FindPairAsync(template, ct);

        var values = request.Values ?? new();

        async Task<TemplatePreviewSide> BuildSideAsync(Language target)
        {
            // Pick which approved template's body to use for this side.
            var pickedTemplate = template.Language == target ? template
                : (pair?.Language == target ? pair : null);

            // If the picked template's language differs from the primary (where the admin entered
            // the values), translate the values via the dictionary so this side reads in `target`'s
            // language. This covers both the "paired template exists" path and the "no paired
            // template, render primary with translated values" fallback.
            var valuesForSide = values;
            var sourceLang = pickedTemplate?.Language ?? template.Language;
            if (sourceLang != template.Language)
            {
                var translated = new Dictionary<string, string>();
                foreach (var kv in values)
                {
                    var outcome = await _translator.TranslateAsync(kv.Value ?? string.Empty,
                        template.Language, sourceLang, ct);
                    translated[kv.Key] = outcome.Translated;
                }
                valuesForSide = translated;
            }

            if (pickedTemplate is not null)
            {
                var rendered = RenderTemplatePreviewBody(pickedTemplate.PreviewText, pickedTemplate.Variables, valuesForSide);
                return new TemplatePreviewSide(target, pickedTemplate.Name, rendered,
                    TemplatePreviewSource.ApprovedTemplate, valuesForSide);
            }

            // No template in the target language — fall back to the primary template's body and
            // translate the values via the dictionary into `target`'s language. Mirrors what the
            // send loop will actually do.
            var fallbackTranslated = new Dictionary<string, string>();
            foreach (var kv in values)
            {
                var outcome = await _translator.TranslateAsync(kv.Value ?? string.Empty,
                    template.Language, target, ct);
                fallbackTranslated[kv.Key] = outcome.Translated;
            }
            var fallbackRendered = RenderTemplatePreviewBody(template.PreviewText, template.Variables, fallbackTranslated);
            return new TemplatePreviewSide(target, template.Name, fallbackRendered,
                TemplatePreviewSource.TranslatedValues, fallbackTranslated);
        }

        var en = await BuildSideAsync(Language.English);
        var es = await BuildSideAsync(Language.Spanish);
        return Ok(new TemplatePreviewResponse(en, es));
    }

    private static string RenderTemplatePreviewBody(
        string? previewText,
        IEnumerable<WhatsAppTemplateVariable> templateVars,
        IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(previewText))
            return string.Join("\n", templateVars.Select(v =>
            {
                var key = v.Position.ToString(CultureInfo.InvariantCulture);
                return $"{v.Label}: {(values.TryGetValue(key, out var x) ? x : "")}";
            }));
        var result = previewText;
        foreach (var v in templateVars)
        {
            var key = v.Position.ToString(CultureInfo.InvariantCulture);
            var val = values.TryGetValue(key, out var x) ? x : "";
            // Approved templates use positional placeholders ({{1}}, {{2}}, ...). Match those.
            result = result.Replace($"{{{{{key}}}}}", val);
        }
        return result;
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

    /// <summary>Substitutes "{{KEY}}" placeholders in the template's preview text with the
    /// supplied values. Falls back to a "name(key=value, ...)" string if the admin hasn't set
    /// a preview text on the template. Display-only — Twilio does the real substitution against
    /// the approved template body when it actually delivers the message.</summary>
    private static string RenderTemplatePreview(string? preview, string templateName, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(preview))
            return $"[Template {templateName}] {string.Join(", ", vars.Select(kv => $"{kv.Key}={kv.Value}"))}";
        var result = preview;
        foreach (var kv in vars)
            result = result.Replace($"{{{{{kv.Key}}}}}", kv.Value);
        return result.Length > 2000 ? result[..2000] : result;
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
        b.Id, b.Channel, b.BodyEn, b.BodyEs, b.SubjectEn, b.SubjectEs, b.TargetLabel, b.CreatedAt,
        b.Recipients.Select(r => new BroadcastRecipientDto(
            r.Id, r.Name, r.Phone, r.Email, r.Language, r.Status, r.StatusMessage, r.TwilioSid, r.TemplateUsed, r.ErrorCode)).ToList());

    private static GroupConversationDetail ToDetail(GroupConversation c) => new(
        c.Id, c.Title, c.Channel, c.TwilioConversationSid, c.CreatedAt,
        c.Participants.Select(p => new GroupConversationParticipantDto(
            p.Id, p.Name, p.Phone, p.TwilioParticipantSid)).ToList());

    private static WhatsAppTemplateDto ToDto(WhatsAppTemplate t, WhatsAppTemplate? pair = null) => new(
        t.Id, t.Name, t.ContentSid, t.Language, t.Description, t.PreviewText, t.Context, t.CreatedAt,
        t.Variables.OrderBy(v => v.Position).Select(v => new WhatsAppTemplateVariableDto(
            v.Id, v.Position, v.Label, v.Example, v.PropertyKey)).ToList(),
        pair is null ? null : new TemplatePairDto(
            pair.Id, pair.Name, pair.ContentSid, pair.Language, pair.PreviewText,
            pair.Variables.OrderBy(v => v.Position).Select(v => new WhatsAppTemplateVariableDto(
                v.Id, v.Position, v.Label, v.Example, v.PropertyKey)).ToList()));

    /// <summary>Strips a trailing language suffix from a template name so pairs share a base
    /// name. Recognizes <c>_en</c>, <c>_es</c>, <c>_english</c>, <c>_spanish</c>. So
    /// <c>practice_english</c> ↔ <c>practice_spanish</c> both reduce to <c>practice</c>, and the
    /// older <c>practice_or_game</c> ↔ <c>practice_or_game_es</c> pattern still works too.</summary>
    private static string BaseName(string name)
    {
        string[] suffixes = { "_english", "_spanish", "_en", "_es" };
        foreach (var s in suffixes)
        {
            if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                return name[..^s.Length];
        }
        return name;
    }

    private static WhatsAppTemplate? FindPairFromGroup(
        WhatsAppTemplate t,
        IReadOnlyDictionary<string, List<WhatsAppTemplate>> byBaseName)
    {
        if (!byBaseName.TryGetValue(BaseName(t.Name), out var siblings)) return null;
        return siblings.FirstOrDefault(s => s.Id != t.Id && s.Language != t.Language);
    }

    private async Task<WhatsAppTemplate?> FindPairAsync(WhatsAppTemplate t, CancellationToken ct)
    {
        var baseName = BaseName(t.Name);
        // Match any common suffix style — `_en/_es`, `_english/_spanish`, or no suffix at all.
        string[] candidates =
        {
            baseName,
            baseName + "_en", baseName + "_es",
            baseName + "_english", baseName + "_spanish"
        };
        return await _db.WhatsAppTemplates
            .Include(x => x.Variables)
            .FirstOrDefaultAsync(x =>
                x.Id != t.Id &&
                x.Language != t.Language &&
                candidates.Contains(x.Name),
                ct);
    }

    private static EmailTemplateDto ToEmailDto(EmailTemplate t, EmailTemplate? pair = null) => new(
        t.Id, t.Name, t.Language, t.Description, t.Subject, t.Body, t.CreatedAt, t.UpdatedAt,
        t.Variables.OrderBy(v => v.Position).Select(v => new EmailTemplateVariableDto(
            v.Id, v.Position, v.Label, v.Example)).ToList(),
        pair is null ? null : new EmailTemplatePairDto(
            pair.Id, pair.Name, pair.Language, pair.Subject, pair.Body,
            pair.Variables.OrderBy(v => v.Position).Select(v => new EmailTemplateVariableDto(
                v.Id, v.Position, v.Label, v.Example)).ToList()));

    private static EmailTemplate? FindEmailPairFromGroup(
        EmailTemplate t,
        IReadOnlyDictionary<string, List<EmailTemplate>> byBaseName)
    {
        if (!byBaseName.TryGetValue(BaseName(t.Name), out var siblings)) return null;
        return siblings.FirstOrDefault(s => s.Id != t.Id && s.Language != t.Language);
    }

    private static List<EmailTemplateVariable> MapEmailVariables(IEnumerable<SaveTemplateVariableDto> input) =>
        input
            .Where(v => v.Position > 0 && !string.IsNullOrWhiteSpace(v.Label))
            .GroupBy(v => v.Position)
            .Select(g => g.Last())
            .Select(v => new EmailTemplateVariable
            {
                Position = v.Position,
                Label = v.Label.Trim(),
                Example = v.Example?.Trim()
            })
            .ToList();

    private static List<WhatsAppTemplateVariable> MapVariables(IEnumerable<SaveTemplateVariableDto> input) =>
        input
            .Where(v => v.Position > 0 && !string.IsNullOrWhiteSpace(v.Label))
            .GroupBy(v => v.Position) // dedupe: keep the last entry for any duplicate position
            .Select(g => g.Last())
            .Select(v => new WhatsAppTemplateVariable
            {
                Position = v.Position,
                Label = v.Label.Trim(),
                Example = v.Example?.Trim(),
                PropertyKey = string.IsNullOrWhiteSpace(v.PropertyKey) ? null : v.PropertyKey.Trim()
            })
            .ToList();
}
