using System.Security.Claims;

namespace ThirdOpinion.Common.Cognito;

/// <summary>
///     Extension methods for ClaimsPrincipal to extract AWS Cognito user information
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    ///     Gets the user ID from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The user ID or empty string if not found</returns>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                principal.FindFirst("sub")?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Gets the username from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The username or empty string if not found</returns>
    public static string GetUsername(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst("cognito:username")?.Value ??
                principal.FindFirst(ClaimTypes.Name)?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Gets the email address from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The email address or empty string if not found</returns>
    public static string GetEmail(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.Email)?.Value ??
                principal.FindFirst("email")?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Gets the first name from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The first name or empty string if not found</returns>
    public static string GetFirstName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.GivenName)?.Value ??
                principal.FindFirst("given_name")?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Gets the last name from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The last name or empty string if not found</returns>
    public static string GetLastName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst(ClaimTypes.Surname)?.Value ??
                principal.FindFirst("family_name")?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Gets the list of groups the user belongs to from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>A list of group names or null if no groups found</returns>
    public static List<string>? GetGroups(this ClaimsPrincipal principal)
    {
        string groups = (principal.FindFirst("cognito:groups")?.Value ??
                         principal.FindFirst("groups")?.Value) ?? string.Empty;

        if (string.IsNullOrEmpty(groups)) return null;

        return groups.Split(',').Select(g => g.Trim()).ToList();
    }

    /// <summary>
    ///     Gets the tenant GUID from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The tenant GUID or null if not found or invalid</returns>
    public static Guid? GetTenantGuid(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirst("custom:tenantGuid")?.Value ??
                             principal.FindFirst("tenantGuid")?.Value, out Guid tenantGuid)
            ? tenantGuid
            : null;
    }

    /// <summary>
    ///     Gets the tenant name from the claims principal
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The tenant name or empty string if not found</returns>
    public static string GetTenantName(this ClaimsPrincipal principal)
    {
        return (principal.FindFirst("custom:tenantName")?.Value ??
                principal.FindFirst("tenantName")?.Value) ?? string.Empty;
    }

    /// <summary>
    ///     Checks if the user is in a specific group
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <param name="groupName">The name of the group to check</param>
    /// <returns>True if the user is in the specified group, false otherwise</returns>
    public static bool IsInGroup(this ClaimsPrincipal principal, string groupName)
    {
        return principal.HasClaim("cognito:groups", groupName);
    }
}