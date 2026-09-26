namespace SoccerSchool.Api;

/// <summary>The roles a permission can be granted to by default. Admin always has everything.</summary>
public enum AccessRole
{
    Admin = 0,
    /// <summary>Automatic for any login linked to a team's coach card (TeamCoach.UserId).</summary>
    Coach = 1,
    /// <summary>Automatic for every adult login.</summary>
    Parent = 2,
}

/// <summary>How far a permission reaches for a non-admin. Enforced by each endpoint, not here.</summary>
public enum PermissionScope
{
    /// <summary>Everything (admins).</summary>
    All = 0,
    /// <summary>Only the coach's own teams and the players rostered on them.</summary>
    OwnTeams = 1,
    /// <summary>Only the parent's own family.</summary>
    OwnFamily = 2,
}

/// <summary>One entry in the permission catalogue.</summary>
/// <param name="Key">Stable id stored in the database and checked in code, e.g. <c>drills.create</c>.</param>
/// <param name="Area">Feature area, for grouping in the admin UI.</param>
/// <param name="DefaultRoles">Roles that get the permission when it first appears. Admin is implicit.</param>
/// <param name="Grantable">Admins can switch it on for an individual person (e.g. "Drill creator").</param>
public record PermissionInfo(
    string Key,
    string Area,
    string NameEn,
    string NameEs,
    AccessRole[] DefaultRoles,
    bool Grantable = false);

/// <summary>
/// The permission catalogue. Code checks permissions, never role names: roles are bundles of these
/// keys that an admin can edit (Coach and Parent; Admin always has all of them), plus individual
/// grants for grantable ones. Keys are persisted — never rename one; add a new key instead.
/// Scope (own teams / own family) is enforced by each endpoint; a coach's permission never reaches
/// beyond the teams their coach card links them to.
/// </summary>
public static class Permissions
{
    public const string AdminAccess = "admin.access";

    public const string DrillsView = "drills.view";
    public const string DrillsCreate = "drills.create";
    public const string DrillsEdit = "drills.edit";
    public const string DrillsAssign = "drills.assign";

    public const string EventsView = "events.view";
    public const string EventsCreate = "events.create";
    public const string AttendanceView = "attendance.view";
    public const string MediaModerate = "media.moderate";

    public const string TeamsView = "teams.view";
    public const string TeamsEdit = "teams.edit";
    public const string PlayersView = "players.view";
    public const string PlayersEdit = "players.edit";

    public const string RegistrationsManage = "registrations.manage";
    public const string MessagingSend = "messaging.send";
    public const string OutreachManage = "outreach.manage";
    public const string AnnouncementsManage = "announcements.manage";
    public const string ChatManage = "chat.manage";
    public const string InvoicesManage = "invoices.manage";
    public const string TournamentsManage = "tournaments.manage";
    public const string SettingsManage = "settings.manage";
    public const string ReportsView = "reports.view";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    public const string FamilyManage = "family.manage";

    private static readonly AccessRole[] AdminOnly = Array.Empty<AccessRole>();
    private static readonly AccessRole[] Coaches = { AccessRole.Coach };
    private static readonly AccessRole[] CoachesAndParents = { AccessRole.Coach, AccessRole.Parent };
    private static readonly AccessRole[] Parents = { AccessRole.Parent };

    /// <summary>Every permission, in display order. Defaults reproduce the app's behaviour from before
    /// RBAC: admins everything; coaches view/assign drills, see their teams' events, attendance and
    /// rosters and moderate their galleries; parents manage their own family. Drill and event
    /// creation are off for coaches until an admin grants them.</summary>
    public static readonly IReadOnlyList<PermissionInfo> All = new List<PermissionInfo>
    {
        new(AdminAccess, "admin", "Open the admin area", "Abrir el área de administración", AdminOnly),

        new(DrillsView, "drills", "View Daily Training drills", "Ver ejercicios de Entrenamiento Diario", Coaches),
        new(DrillsCreate, "drills", "Create drills (Drill creator)", "Crear ejercicios (Creador de ejercicios)", AdminOnly, Grantable: true),
        new(DrillsEdit, "drills", "Edit or archive any drill", "Editar o archivar cualquier ejercicio", AdminOnly),
        new(DrillsAssign, "drills", "Assign drills to players and teams", "Asignar ejercicios a jugadores y equipos", Coaches),

        new(EventsView, "events", "View events and schedules", "Ver eventos y calendarios", CoachesAndParents),
        new(EventsCreate, "events", "Create, edit and cancel events (Event creator)", "Crear, editar y cancelar eventos (Creador de eventos)", AdminOnly, Grantable: true),
        new(AttendanceView, "events", "View team attendance", "Ver asistencia del equipo", Coaches),
        new(MediaModerate, "events", "Remove photos and videos from galleries", "Quitar fotos y videos de las galerías", Coaches),

        new(TeamsView, "teams", "View teams and rosters", "Ver equipos y listas", Coaches),
        new(TeamsEdit, "teams", "Edit teams, rosters and coaches", "Editar equipos, listas y entrenadores", AdminOnly),
        new(PlayersView, "players", "View players", "Ver jugadores", Coaches),
        new(PlayersEdit, "players", "Edit players, uniforms and merges", "Editar jugadores, uniformes y fusiones", AdminOnly),

        new(RegistrationsManage, "registrations", "Manage registrations and waivers", "Administrar inscripciones y exenciones", AdminOnly),
        new(MessagingSend, "communication", "Send messages and broadcasts", "Enviar mensajes y difusiones", AdminOnly),
        new(OutreachManage, "communication", "Manage outreach invitations", "Administrar invitaciones", AdminOnly),
        new(AnnouncementsManage, "communication", "Post announcements", "Publicar anuncios", AdminOnly),
        new(ChatManage, "communication", "Manage chat groups", "Administrar grupos de chat", AdminOnly),
        new(InvoicesManage, "money", "Manage invoices and charges", "Administrar facturas y cargos", AdminOnly),
        new(TournamentsManage, "tournaments", "Manage hosted tournaments", "Administrar torneos", AdminOnly),
        new(SettingsManage, "settings", "Manage venues, uniforms, age groups and templates", "Administrar lugares, uniformes, grupos de edad y plantillas", AdminOnly),
        new(ReportsView, "reports", "View reports", "Ver reportes", AdminOnly),
        new(UsersManage, "access", "Manage users and their permissions", "Administrar usuarios y sus permisos", AdminOnly),
        new(RolesManage, "access", "Edit what each role can do", "Editar lo que puede hacer cada rol", AdminOnly),

        new(FamilyManage, "family", "Manage own family, registrations and kids' logins", "Administrar su familia, inscripciones y accesos de los niños", Parents),
    };

    public static readonly IReadOnlySet<string> Keys = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string key) => Keys.Contains(key);

    public static PermissionInfo? Find(string key) => All.FirstOrDefault(p => p.Key == key);
}
