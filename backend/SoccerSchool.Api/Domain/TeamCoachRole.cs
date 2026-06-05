namespace SoccerSchool.Api.Domain;

/// <summary>
/// How a <see cref="TeamCoach"/> is designated on the team. Drives the role badge in the
/// per-team coach editor and (in the future) which coaches get certain admin notifications
/// (head coach gets scheduling pings, assistants get the team-wide broadcast).
/// </summary>
public enum TeamCoachRole
{
    HeadCoach = 0,
    AssistantCoach = 1,
}
