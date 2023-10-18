namespace Occtoo.Functional.Extensions.Functional.Types;

/// <summary>
/// The base type that represents error that should be handled in the system/service
/// </summary>
/// <param name="Message"></param>
public record DomainError(string Message)
{
    public static DomainError FromException(Exception ex, string message = null)
        => new($"{(message ?? ex.Message) + "."} Exception type [{ex.GetType().Name}], message: {ex}");
}

public record UnauthorizedError(string Message) : DomainError(Message);

public record UploadError(string Message) : DomainError(Message);

public record MetadataParsingError(string Message) : DomainError(Message);

public record ForbiddenError(string Message) : DomainError(Message);

public record UnknownError(string Message) : DomainError(Message);

public record NotFoundError(string Message) : DomainError(Message);

public record EntityNotFoundError<T>(string Id = "") : NotFoundError($"{typeof(T).Name}{Id?.PadLeft(Id.Length + 1).IfEmpty(string.Empty)} not found");

public record ValidationError(string Message, IReadOnlyList<PropertyValidationFailure> Failures = default) : DomainError(Message);

public record PropertyValidationFailure(string PropertyName, string Message)
{
    public override string ToString() => $"{PropertyName}: {Message}";
}

public record ConflictError(string Message) : DomainError(Message);

public record EntityAlreadyExists<T>() : ConflictError($"{typeof(T).Name} already exists");

public record ServiceUnavailableError(string Message) : DomainError(Message);

public record TimeoutError(TimeSpan Timeout)
    : DomainError($"Operation timed out after '{Timeout}'");

public record TooManyRequestsError(string Message) : DomainError(Message)
{
}
