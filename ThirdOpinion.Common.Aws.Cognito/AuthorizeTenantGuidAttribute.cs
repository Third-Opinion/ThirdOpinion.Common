using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Cognito;

/// <summary>
///     Validates that the TenantGuid property matches the authenticated user's TenantGuid claim
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class AuthorizeTenantGuidAttribute : ValidationAttribute
{
    /// <summary>
    ///     Validates that the provided tenant GUID matches the authenticated user's authorized tenants
    /// </summary>
    /// <param name="value">The tenant GUID value to validate</param>
    /// <param name="validationContext">The validation context</param>
    /// <returns>A ValidationResult indicating success or failure with error message</returns>
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        var httpContextAccessor = (IHttpContextAccessor?)validationContext
            .GetService(typeof(IHttpContextAccessor));
        var options = (IOptions<GlobalAppSettingsOptions>?)validationContext
            .GetService(typeof(IOptions<GlobalAppSettingsOptions>));

        if (httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated != true)
            return new ValidationResult("User is not authenticated");

        // Get the PersonGuid claim from the current user
        List<string>? groups = httpContextAccessor.HttpContext.User.GetGroups();

        if (groups == null) return new ValidationResult("User does not have a groups claim");

        // Get the value of the property being validated
        if (value is not Guid propertyGuid)
            return new ValidationResult("Value must be a valid Guid");

        //look in config for the tenantGuid
        List<string>? tenantGroups = null;
        if (options != null &&
            !options.Value.Tenants.TenantGroups.TryGetValue(propertyGuid.ToString(),
                out tenantGroups))
            return new ValidationResult($"TenantGuid {propertyGuid} not found in configuration");


        if (tenantGroups == null || tenantGroups.Count == 0 ||
            !tenantGroups.Intersect(groups).Any())
            return new ValidationResult("TenantGuid does not match the authenticated user.");

        // Validation passed
        return ValidationResult.Success!;
    }
}