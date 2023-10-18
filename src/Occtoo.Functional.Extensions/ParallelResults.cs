using CSharpFunctionalExtensions;
using Occtoo.Functional.Extensions.Functional.Types;

namespace Occtoo.Functional.Extensions;

public static class ParallelResults
{
    public static Result<(T1 firstResult, T2 secondResult), DomainError> WhenAll<T1, T2>(
        Result<T1, DomainError> first,
        Result<T2, DomainError> second)
    {
        return first.Bind(firstTaskResult =>
        {
            return second.Map(secondTaskResult => (firstTaskResult, secondTaskResult));
        });
    }

    public static async Task<Result<(T1 firstResult, T2 secondResult), DomainError>> WhenAll<T1, T2>(
        Task<Result<T1, DomainError>> first,
        Task<Result<T2, DomainError>> second)
    {
        await Task.WhenAll(first, second);
        return first.Result.Bind(firstTaskResult =>
        {
            return second.Result.Map(secondTaskResult => (firstTaskResult, secondTaskResult));
        });
    }

    public static async Task<Result<(T1 firstResult, T2 secondResult, T3 thirdResult), DomainError>> WhenAll<T1, T2, T3>(
        Task<Result<T1, DomainError>> first,
        Task<Result<T2, DomainError>> second,
        Task<Result<T3, DomainError>> third)
    {
        var firstAndSecond = WhenAll(first, second);
        await Task.WhenAll(third, firstAndSecond);
        return third.Result.Bind(thirdTaskResult =>
        {
            return firstAndSecond.Result.Map(firstAndSecondResult =>
                (firstAndSecondResult.firstResult, firstAndSecondResult.secondResult, thirdTaskResult));
        });
    }
}