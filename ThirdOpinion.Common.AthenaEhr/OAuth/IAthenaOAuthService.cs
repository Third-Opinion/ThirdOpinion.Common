namespace ThirdOpinion.Common.AthenaEhr;

/// <summary>
///     Interface for Athena-specific OAuth service
/// </summary>
public interface IAthenaOAuthService : IOAuthService
{
    // Inherits GetTokenAsync and RefreshTokenAsync from IOAuthService
}