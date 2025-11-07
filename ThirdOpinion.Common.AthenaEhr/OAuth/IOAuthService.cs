namespace ThirdOpinion.Common.AthenaEhr;

public interface IOAuthService
{
    Task<OAuthToken> GetTokenAsync(CancellationToken cancellationToken = default);
    Task<OAuthToken> RefreshTokenAsync(CancellationToken cancellationToken = default);
}