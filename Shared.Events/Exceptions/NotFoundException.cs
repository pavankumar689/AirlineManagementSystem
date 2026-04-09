namespace Shared.Events.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// Maps to HTTP 404 Not Found via GlobalExceptionHandler.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} with key '{key}' was not found.") { }

    public NotFoundException(string message) : base(message) { }
}
