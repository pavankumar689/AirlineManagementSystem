namespace Shared.Events.Exceptions;

/// <summary>
/// Thrown when incoming data fails business/validation rules.
/// Maps to HTTP 400 Bad Request via ValidationExceptionHandler.
/// </summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string message)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}
