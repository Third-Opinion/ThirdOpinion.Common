using System.Security.Claims;

namespace ThirdOpinion.Common.Cognito;

// Create a static class for your extensions
public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                principal.FindFirst("sub")?.Value) ?? string.Empty;
    }

    public static string GetUsername(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst("cognito:username")?.Value ??
                principal.FindFirst(ClaimTypes.Name)?.Value) ?? string.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.Email)?.Value ??
                principal.FindFirst("email")?.Value) ?? string.Empty;
    }

    public static string GetFirstName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.GivenName)?.Value ??
                principal.FindFirst("given_name")?.Value) ?? string.Empty;
    }

    public static string GetLastName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.Surname)?.Value ??
                principal.FindFirst("family_name")?.Value) ?? string.Empty;
    }

    public static List<string>? GetGroups(this ClaimsPrincipal principal)
    {
        string groups = (principal.FindFirst("cognito:groups")?.Value ??
                         principal.FindFirst("groups")?.Value) ?? string.Empty;

        if (string.IsNullOrEmpty(groups)) return null;

        return groups.Split(',').Select(g => g.Trim()).ToList();
    }

    public static Guid? GetTenantGuid(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirst("custom:tenantGuid")?.Value ??
                             principal.FindFirst("tenantGuid")?.Value, out Guid tenantGuid)
            ? tenantGuid
            : null;
    }

    public static string GetTenantName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst("custom:tenantName")?.Value ??
                principal.FindFirst("tenantName")?.Value) ?? string.Empty;
    }

    public static bool IsInGroup(this ClaimsPrincipal principal, string groupName)
    {
        return principal.HasClaim("cognito:groups", groupName);
    }
}