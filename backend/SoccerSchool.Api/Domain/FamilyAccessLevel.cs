namespace SoccerSchool.Api.Domain;

/// <summary>What an additional person on a family (a <see cref="ParentContact"/> / linked
/// <see cref="ParentAccountCollaborator"/>) may do. The family owner always has full access.</summary>
public enum FamilyAccessLevel
{
    /// <summary>Parent/guardian: everything the owner can do for the kids (attendance, team chat,
    /// registrations, invoices, kid logins).</summary>
    Guardian = 0,

    /// <summary>Family member (grandparent, family friend): sees the kids' schedule, event details,
    /// photos and announcements. Can't change attendance, join team chat, or see registrations
    /// and invoices.</summary>
    Viewer = 1,
}
