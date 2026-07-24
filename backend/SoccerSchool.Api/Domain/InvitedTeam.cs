using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// External team on file, invited to LVSS-hosted tournaments/leagues. Just a lightweight
/// contact record — head coach name/phone/email + age group — so the admin can roster the
/// team into a <see cref="HostedTournament"/> without retyping the contact details each time.
/// Not an LVSS <see cref="Team"/> and doesn't carry a roster of players.
/// </summary>
public class InvitedTeam
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? HeadCoachName { get; set; }

    /// <summary>Stored in E.164 when possible; free-text otherwise. Not normalized on write —
    /// preserves whatever the admin typed since these are outside families we send to.</summary>
    [MaxLength(32)]
    public string? HeadCoachPhone { get; set; }

    [MaxLength(320)]
    public string? HeadCoachEmail { get; set; }

    /// <summary>Free-text age bracket — "U10", "2016-2017", "Boys 12U", etc. Not tied to
    /// AgeClassification since external teams follow their own bracket naming.</summary>
    [MaxLength(60)]
    public string? AgeGroup { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
