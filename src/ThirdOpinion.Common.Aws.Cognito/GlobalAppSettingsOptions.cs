namespace ThirdOpinion.Common.Cognito;

public class GlobalAppSettingsOptions
{
    public string TablePrefix { get; set; } = string.Empty;

    public CognitoOptions Cognito { get; set; } = new();
    public TenantOptions Tenants { get; set; } = new();

    public class TenantOptions
    {
        public Dictionary<string, List<string>> TenantGroups { get; set; } = new();
    }

    public class CognitoOptions
    {
        public string Region { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
    }
}