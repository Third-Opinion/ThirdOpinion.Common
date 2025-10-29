using System.Security.Cryptography;
using System.Text;

namespace ThirdOpinion.Common.Aws.Misc;

/// <summary>
/// Helper class for AWS Signature Version 4 signing operations
/// </summary>
public static class AWS4Signer
{
    /// <summary>
    /// Computes SHA256 hash of the input string
    /// </summary>
    public static string ComputeHash(string data)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature
    /// </summary>
    public static string HmacSha256(string data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = hmac.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Computes HMAC-SHA256 and returns byte array
    /// </summary>
    public static byte[] HmacSha256Bytes(string data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// Derives the signing key for AWS Signature Version 4
    /// </summary>
    public static byte[] GetSignatureKey(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes($"AWS4{secretKey}");
        var kDate = HmacSha256Bytes(dateStamp, kSecret);
        var kRegion = HmacSha256Bytes(region, kDate);
        var kService = HmacSha256Bytes(service, kRegion);
        var kSigning = HmacSha256Bytes("aws4_request", kService);
        return kSigning;
    }
}