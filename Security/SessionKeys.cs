namespace RaceDay.Api.Security;

/// <summary>Keys used to store the authenticated user in the server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>Role names. These match the rows seeded in the Roles table.</summary>
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static readonly string[] All = { Organiser, Participant };

    public static string? Normalise(string? value) =>
        All.FirstOrDefault(r => string.Equals(r, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
