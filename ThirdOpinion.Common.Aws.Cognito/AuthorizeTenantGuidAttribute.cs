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
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var httpContextAccessor = validationContext
            .GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
        var options = validationContext
            .GetService(typeof(IOptions<GlobalAppSettingsOptions>)) as IOptions<GlobalAppSettingsOptions>;

        if (httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated != true)
            return new ValidationResult("User is not authenticated");

        // Get the PersonGuid claim from the current user
        var groups = httpContextAccessor.HttpContext.User.GetGroups();

        if (groups == null) return new ValidationResult("User does not have a groups claim");

        // Get the value of the property being validated
        if (value == null || !(value is Guid propertyGuid))
            return new ValidationResult("Value must be a valid Guid");

        //look in config for the tenantGuid
        List<string>? tenantGroups = null;
        if (options != null &&
            !options.Value.Tenants.TenantGroups.TryGetValue(propertyGuid.ToString(),
                out tenantGroups))
            return new ValidationResult($"TenantGuid {propertyGuid} not found in configuration");


        if (tenantGroups == null || tenantGroups.Count == 0 || !tenantGroups.Intersect(groups).Any())
            return new ValidationResult("TenantGuid does not match the authenticated user.");

        // Validation passed
        return ValidationResult.Success;
    }
}