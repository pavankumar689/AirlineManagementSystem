namespace Shared.Events.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with existing data.
/// Example: registering with an email that already exists.
/// Maps to HTTP 409 Conflict via GlobalExceptionHandler.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string resource, string reason)
        : base($"{resource} conflict: {reason}") { }

    public ConflictException(string message) : base(message) { }
}
