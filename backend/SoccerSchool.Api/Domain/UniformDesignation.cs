namespace SoccerSchool.Api.Domain;

/// <summary>
/// What a <see cref="Uniform"/> is the default kit for. At most one uniform may hold each non-None
/// designation (enforced by a filtered unique index + controller reassignment). Games map their
/// home/away setting to <see cref="Home"/> / <see cref="Away"/>; practice events map to
/// <see cref="Practice"/>. <see cref="None"/> means the uniform exists but isn't an auto-default.
/// </summary>
public enum UniformDesignation
{
    None = 0,
    Home = 1,
    Away = 2,
    Practice = 3,
}
