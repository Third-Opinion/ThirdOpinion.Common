namespace ThirdOpinion.Common.AthenaEhr;

public class OAuthException : Exception
{
    public OAuthException() : base() { }
    
    public OAuthException(string message) : base(message) { }
    
    public OAuthException(string message, Exception innerException) : base(message, innerException) { }
}