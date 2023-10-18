namespace Occtoo.Functional.Extensions;

public interface IUserCreatedEntity
{
    Guid CreatedByUser { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset LastModified { get; }
    Guid LastModifiedByUser { get; }
}