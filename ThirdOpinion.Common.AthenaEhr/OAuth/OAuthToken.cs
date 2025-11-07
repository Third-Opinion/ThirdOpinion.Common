namespace ThirdOpinion.Common.AthenaEhr;

public class OAuthToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsExpired()
    {
        DateTimeOffset expirationTime = CreatedAt.AddSeconds(ExpiresIn);
        // Consider token expired 1 minute before actual expiration
        return DateTimeOffset.UtcNow >= expirationTime.AddMinutes(-1);
    }
}