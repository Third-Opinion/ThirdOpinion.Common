using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Cognito;

public class AuthTenantGuid : TypeFilterAttribute
{
    public AuthTenantGuid() : base(typeof(AuthorizeTenantGuidPersonAttribute))
    {
    }
}

/// <summary>
///     Authorizes a request based on the tenantGuid parameter and the user's groups claim.
///     There is likelly a better way to do this.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AuthorizeTenantGuidPersonAttribute : ActionFilterAttribute
{
    public AuthorizeTenantGuidPersonAttribute(string parameterName = "tenantGuid")
    {
        ParameterName = parameterName;
    }

    public string ParameterName { get; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<AuthorizeTenantGuidPersonAttribute>>();
        try
        {
            var globalSettings = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<GlobalAppSettingsOptions>>().Value;


            // Check if we have the route parameter
            if (!context.RouteData.Values.TryGetValue(ParameterName, out object? routeValue) ||
                routeValue == null)
            {
                context.Result = new BadRequestResult();
                return;
            }

            if (!Guid.TryParse(routeValue.ToString(), out Guid parameterGuid))
            {
                context.Result = new BadRequestResult();
                return;
            }

            ClaimsPrincipal? user = context.HttpContext.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get the PersonGuid claim from the current user
            List<string>? groups = user.GetGroups();

            if (groups == null)
            {
                logger.LogError("User does not have a groups claim");
                context.Result = new ForbidResult("Bearer");
                return;
            }

            //look in config for the tenantGuid
            List<string>? tenantGroups =
                globalSettings.Tenants.TenantGroups.TryGetValue(parameterGuid.ToString(),
                    out tenantGroups)
                    ? tenantGroups
                    : null;

            if (tenantGroups == null)
            {
                logger.LogDebug("TenantGuid not found in config");
                context.Result = new ForbidResult("Bearer");
                return;
            }

            if (tenantGroups.Count == 0 ||
                !tenantGroups.Intersect(groups, StringComparer.OrdinalIgnoreCase).Any())
            {
                logger.LogDebug("TenantGuid does not match the authenticated user group");
                context.Result = new ForbidResult("Bearer");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error authorizing tenantGuid");
            context.Result = new StatusCodeResult(500);
        }
    }
}