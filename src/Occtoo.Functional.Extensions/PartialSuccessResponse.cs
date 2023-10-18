using System.Collections.Immutable;

namespace Occtoo.Functional.Extensions;

public class PartialSuccessResponse<TKey, TResult, TFailure>
{
    private PartialSuccessResponse(IReadOnlyDictionary<TKey, TResult> succeeded, IReadOnlyDictionary<TKey, TFailure> failures)
    {
        Succeeded = succeeded;
        Failures = failures;
    }
    public IReadOnlyDictionary<TKey, TResult> Succeeded { get; }
    public IReadOnlyDictionary<TKey, TFailure> Failures { get; }

    public PartialSuccessResponse<TKey, TResult, TFailure> AddSuccess(TKey key, TResult result)
        => new(ImmutableDictionary.CreateRange(Succeeded).Add(key, result), Failures);

    public PartialSuccessResponse<TKey, TResult, TFailure> AddFailure(TKey key, TFailure failure)
        => new(Succeeded, ImmutableDictionary.CreateRange(Failures).Add(key, failure));

    public static PartialSuccessResponse<TKey, TResult, TFailure> Empty => new(ImmutableDictionary<TKey, TResult>.Empty,
        ImmutableDictionary<TKey, TFailure>.Empty);

    public PartialSuccessResponse<TKey, TOtherResult, TFailure> Map<TOtherResult>(
        Func<TResult, TOtherResult> mapSucceeded)
        => new(
            Succeeded.Select(pair => new KeyValuePair<TKey, TOtherResult>(pair.Key, mapSucceeded(pair.Value))).ToImmutableDictionary(),
            Failures);
    public PartialSuccessResponse<TKey, TResult, TFailure> MapKeys(
        Func<TKey, TKey> mapKey)
        => new(
            Succeeded.Select(pair => new KeyValuePair<TKey, TResult>(mapKey(pair.Key), pair.Value)).ToImmutableDictionary(),
            Failures.Select(pair => new KeyValuePair<TKey, TFailure>(mapKey(pair.Key), pair.Value)).ToImmutableDictionary());
}