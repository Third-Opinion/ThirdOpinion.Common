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
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var httpContextAccessor = (IHttpContextAccessor)validationContext
            .GetService(typeof(IHttpContextAccessor));
        var options = (IOptions<GlobalAppSettingsOptions>)validationContext
            .GetService(typeof(IOptions<GlobalAppSettingsOptions>));

        if (httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated != true)
            return new ValidationResult("User is not authenticated");

        // Get the PersonGuid claim from the current user
        var groups = httpContextAccessor.HttpContext.User.GetGroups();

        if (groups == null) return new ValidationResult("User does not have a groups claim");

        // Get the value of the property being validated
        var propertyGuid = (Guid)value;

        //look in config for the tenantGuid
        List<string>? tenantGroups = null;
        if (options != null &&
            !options.Value.Tenants.TenantGroups.TryGetValue(propertyGuid.ToString(),
                out tenantGroups))
            return new ValidationResult($"TenantGuid {propertyGuid} not found in configuration");


        if (tenantGroups == null || tenantGroups.Count == 0 || !tenantGroups.Union(groups).Any())
            return new ValidationResult("TenantGuid does not match the authenticated user.");

        // Validation passed
        return ValidationResult.Success;
    }
}