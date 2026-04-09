namespace Shared.Events.Exceptions;

/// <summary>
/// Thrown when a user provides incorrect credentials or an invalid/expired token.
/// Maps to HTTP 401 Unauthorized via GlobalExceptionHandler.
/// </summary>
public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}
