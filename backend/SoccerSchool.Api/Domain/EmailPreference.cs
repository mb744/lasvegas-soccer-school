namespace SoccerSchool.Api.Domain;

/// <summary>A parent's choice for one kind of automatic event email ("My Personal Game and Event
/// Notifications"). Default follows the club default, which today is to email.</summary>
public enum EmailPreference
{
    Default = 0,
    Email = 1,
    DontEmail = 2,
}
