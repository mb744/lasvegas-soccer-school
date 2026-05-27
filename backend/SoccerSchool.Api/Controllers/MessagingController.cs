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
    private readonly TwilioOptions _twilio;

    public MessagingController(
        AppDbContext db,
        IMessageSender sender,
        IEmailSender emailSender,
        IRecipientResolver resolver,
        IConversationService conversations,
        IPhraseTranslator translator,
        IOptions<TwilioOptions> twilio)
    {
        _db = db;
        _sender = sender;
        _emailSender = emailSender;
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
            Email: _emailSender.IsAvailable,
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

        // For WhatsApp-template sends, persist a rendered preview in BodyEn so the history view
        // shows what went out (Twilio does the real substitution server-side). For Email templates,
        // the subject + body live on the broadcast itself so we copy those in instead.
        var renderedWhatsAppTemplate = isWhatsAppTemplate
            ? RenderTemplatePreview(template!.PreviewText, template.Name, templateVars)
            : null;

        var broadcast = new Broadcast
        {
            Channel = request.Channel,
            BodyEn = isEmailTemplate
                ? RenderTemplateString(emailTemplate!.Body, emailTemplate.Variables, templateVars)
                : (bodyEn ?? renderedWhatsAppTemplate),
            BodyEs = isEmailTemplate && pairedEmailTemplate is not null
                ? RenderTemplateString(pairedEmailTemplate.Body, pairedEmailTemplate.Variables, templateVars)
                : bodyEs,
            SubjectEn = isEmailTemplate
                ? RenderTemplateString(emailTemplate!.Subject, emailTemplate.Variables, templateVars)
                : subjectEn,
            SubjectEs = isEmailTemplate && pairedEmailTemplate is not null
                ? RenderTemplateString(pairedEmailTemplate.Subject, pairedEmailTemplate.Variables, templateVars)
                : subjectEs,
            TargetLabel = resolved.Label,
            WhatsAppTemplateId = template?.Id,
            TemplateVariablesJson = (isWhatsAppTemplate || isEmailTemplate) ? JsonSerializer.Serialize(templateVars) : null,
            ScheduledGameId = request.ScheduledGameId
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

    /// <summary>One row per distinct phone we've exchanged messages with. Powers the admin Inbox.</summary>
    [HttpGet("threads")]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> ListThreads(CancellationToken ct)
    {
        // Pull last 6 months of activity into memory and group there — keeps the SQL simple and
        // works against both InboundMessages and BroadcastRecipients without a complex union join.
        var since = DateTime.UtcNow.AddMonths(-6);
        var inbound = await _db.InboundMessages
            .Where(m => m.ReceivedAt >= since)
            .Select(m => new { m.FromPhone, m.Body, At = m.ReceivedAt, Direction = ThreadDirection.Inbound })
            .ToListAsync(ct);
        var outbound = await _db.BroadcastRecipients
            .Where(r => r.Broadcast!.CreatedAt >= since)
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

        // Look up parent records in one shot for name + registered flag.
        var phones = byPhone.Select(g => g.Key).ToList();
        var parents = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && phones.Contains(p.CellPhone))
            .ToDictionaryAsync(p => p.CellPhone!, p => p, ct);

        var summaries = byPhone
            .Select(g =>
            {
                var last = g.OrderByDescending(x => x.At).First();
                var inboundCount = g.Count(x => x.Direction == ThreadDirection.Inbound);
                var outboundCount = g.Count(x => x.Direction == ThreadDirection.Outbound);
                parents.TryGetValue(g.Key, out var parent);
                return new ThreadSummaryDto(
                    g.Key,
                    parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim(),
                    parent?.Id,
                    parent is not null,
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

    /// <summary>Full chronological thread for one phone — inbounds + outbounds interleaved.</summary>
    [HttpGet("threads/{phone}")]
    public async Task<ActionResult<ThreadDetailDto>> GetThread(string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return BadRequest("Phone is required.");

        var inbound = await _db.InboundMessages
            .Where(m => m.FromPhone == phone)
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
            .Where(r => r.Phone == phone)
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

        var parent = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.CellPhone == phone, ct);
        return Ok(new ThreadDetailDto(
            phone,
            parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim(),
            parent?.Id,
            parent is not null,
            parent?.Language,
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

        var parent = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.CellPhone == phone, ct);
        var name = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
        var lang = parent?.Language ?? Language.English;

        var broadcast = new Broadcast
        {
            Channel = request.Channel,
            BodyEn = lang == Language.English ? request.Body.Trim() : null,
            BodyEs = lang == Language.Spanish ? request.Body.Trim() : null,
            TargetLabel = $"Reply to {phone}",
        };
        var recipient = new BroadcastRecipient
        {
            Name = name,
            Phone = phone,
            Email = null,
            Language = lang,
            Status = MessageDeliveryStatus.Pending
        };
        broadcast.Recipients.Add(recipient);
        _db.Broadcasts.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        var send = await _sender.SendAsync(request.Channel, phone, request.Body.Trim(), ct);
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
                b.SubjectEn,
                b.SubjectEs,
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
        // Wipe existing variables and replace — simpler than diffing for a small list.
        _db.WhatsAppTemplateVariables.RemoveRange(template.Variables);
        template.Variables = MapVariables(request.Variables);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(template, await FindPairAsync(template, ct)));
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
        return Ok(new MessagingSettingsDto(s.AutoReplyEnabled, s.AutoReplyTextEn, s.AutoReplyTextEs, s.UpdatedAt));
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
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new MessagingSettingsDto(s.AutoReplyEnabled, s.AutoReplyTextEn, s.AutoReplyTextEs, s.UpdatedAt));
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
            r.Id, r.Name, r.Phone, r.Email, r.Language, r.Status, r.StatusMessage, r.TwilioSid)).ToList());

    private static GroupConversationDetail ToDetail(GroupConversation c) => new(
        c.Id, c.Title, c.Channel, c.TwilioConversationSid, c.CreatedAt,
        c.Participants.Select(p => new GroupConversationParticipantDto(
            p.Id, p.Name, p.Phone, p.TwilioParticipantSid)).ToList());

    private static WhatsAppTemplateDto ToDto(WhatsAppTemplate t, WhatsAppTemplate? pair = null) => new(
        t.Id, t.Name, t.ContentSid, t.Language, t.Description, t.PreviewText, t.CreatedAt,
        t.Variables.OrderBy(v => v.Position).Select(v => new WhatsAppTemplateVariableDto(
            v.Id, v.Position, v.Label, v.Example)).ToList(),
        pair is null ? null : new TemplatePairDto(
            pair.Id, pair.Name, pair.ContentSid, pair.Language, pair.PreviewText,
            pair.Variables.OrderBy(v => v.Position).Select(v => new WhatsAppTemplateVariableDto(
                v.Id, v.Position, v.Label, v.Example)).ToList()));

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
                Example = v.Example?.Trim()
            })
            .ToList();
}
