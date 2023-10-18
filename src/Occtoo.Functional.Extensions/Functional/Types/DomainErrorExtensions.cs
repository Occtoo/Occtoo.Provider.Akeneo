using System.Runtime.CompilerServices;

namespace Occtoo.Functional.Extensions.Functional.Types;

public static class DomainErrorExtensions
{
    public static DomainError Combine(this IEnumerable<DomainError> errors)
        => new DomainError(string.Join(";", errors.Select(e => $"[{e.GetType().Name}: {e.Message}]")));

    public static ValidationError Combine(this IEnumerable<ValidationError> errors) => new(string.Join(";", errors.Select((e =>
    {
        var interpolatedStringHandler = new DefaultInterpolatedStringHandler(4, 2);
        interpolatedStringHandler.AppendLiteral("[");
        interpolatedStringHandler.AppendFormatted(e.Message);
        interpolatedStringHandler.AppendLiteral("]");
        return interpolatedStringHandler.ToStringAndClear();
    }))));
}