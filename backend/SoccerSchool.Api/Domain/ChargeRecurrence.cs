namespace SoccerSchool.Api.Domain;

/// <summary>
/// How often a <see cref="ChargeType"/> bills. v1 stores this on the catalog row so the admin
/// sees + can filter by it; auto-generating recurring invoices on schedule is a future job
/// that reads from this same field.
/// </summary>
public enum ChargeRecurrence
{
    OneTime = 0,
    Hourly = 1,
    Daily = 2,
    Weekly = 3,
    Monthly = 4,
    Yearly = 5,
}
