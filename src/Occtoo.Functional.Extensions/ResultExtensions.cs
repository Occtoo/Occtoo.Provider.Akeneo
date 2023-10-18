using CSharpFunctionalExtensions;
using Occtoo.Functional.Extensions.Functional.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Occtoo.Functional.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Converts maybe to result. If maybe has value the result will be successful, otherwise the Failure will be returned with the specified error object
    /// </summary>
    /// <param name="task"></param>
    /// <param name="errorIfNone">Error to be returned if maybe has no value</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TE"></typeparam>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static Task<Result<T, DomainError>> Unwrap<T, TE>(this Task<Result<Maybe<T>, TE>> task, TE errorIfNone) where TE : DomainError =>
        task
            .Bind(maybe => maybe.ToResult(errorIfNone))
            .MapError(err => err as DomainError);
    [DebuggerStepThrough]
    public static async Task<Result<K, DomainError>> Bind<T, K, TE>(this Task<Result<T, DomainError>> task, Func<T, Task<Result<K, TE>>> onSuccess) where TE : DomainError
    {
        var result = await task;
        if (result.IsSuccess)
            return await onSuccess(result.Value).MapError(e => e as DomainError);
        return Result.Failure<K, DomainError>(result.Error);
    }
    [DebuggerStepThrough]
    public static async Task<Result<K, DomainError>> BindE<T, K, TE>(this Task<Result<T, TE>> task, Func<T, Task<Result<K, DomainError>>> onSuccess) where TE : DomainError
    {
        var result = await task;
        if (result.IsSuccess)
            return await onSuccess(result.Value);
        return Result.Failure<K, DomainError>(result.Error);
    }
    [DebuggerStepThrough]
    public static async Task<Result<K, DomainError>> Bind<T, K, TE>(this Result<T, TE> result, Func<T, Task<Result<K, DomainError>>> onSuccess) where TE : DomainError
    {
        if (result.IsSuccess)
            return await onSuccess(result.Value);
        return Result.Failure<K, DomainError>(result.Error);
    }
    [DebuggerStepThrough]
    public static Task<Result<T, DomainError>> AsTask<T>(this Result<T, DomainError> result)
    {
        return Task.FromResult(result);
    }
    [DebuggerStepThrough]
    public static async Task<Result<K, DomainError>> Bind<T, K, TE>(this Task<Result<T, DomainError>> result, Func<T, Result<K, TE>> onSuccess) where TE : DomainError
    {
        var r = await result;
        if (r.IsSuccess)
            return onSuccess(r.Value).MapError(err => err as DomainError);
        return Result.Failure<K, DomainError>(r.Error);
    }

    /// <summary>
    /// Executes the operation as normal with the addition of a time constraint as specified by the caller
    /// </summary>
    /// <param name="result">The operation that is supposed to complete within the time frame</param>
    /// <param name="timeout">The time frame for the timeout condition.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The result from the bind as normal if it completes within the given time frame,
    /// otherwise a TimeoutError is returned</returns>
    [DebuggerStepThrough]
    public static async Task<Result<T, DomainError>> WithinTimeout<T>(this Task<Result<T, DomainError>> result,
        TimeSpan timeout)
    {
        var winner = await Task.WhenAny(result, Task.Delay(timeout));
        if (winner == result)
        {
            return result.Result;
        }
        else
        {
            var error = new TimeoutError(timeout);
            return Result.Failure<T, DomainError>(error);
        }
    }

    [DebuggerStepThrough]
    public static Result<ImmutableList<T>, DomainError> CombineAll<T>(this IEnumerable<Result<T, DomainError>> results)
    {
        return results
            .Combine(errs => errs.Combine())
            .Map(enumerable => enumerable.ToImmutableList());
    }
    [DebuggerStepThrough]
    public static async Task<Result<IEnumerable<T>, DomainError>> CombineAll<T>(this IEnumerable<Task<Result<T, DomainError>>> tasks)
    {
        var results = await Task.WhenAll(tasks);
        return results
            .Combine(errors => new DomainError(errors
                .Aggregate(string.Empty, (acc, err) => $"{acc}{err.Message};")));
    }

    /// <summary>
    /// Use this method with caution as it will "swallow" the result and failure of the operation, and return successful result in both cases.
    /// Primary use case is when you want to ignore the result of the operation, e.g. "fire and forget" scenario.
    /// </summary>
    /// <param name="task"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<Result<None, DomainError>> IgnoreResultAndFailure<T>(this Task<Result<T, DomainError>> task)
    {
        return await task
            .Map(_ => None.Value)
            .OnFailureCompensate(_ => None.Value);
    }

    /// <summary>
    /// In contrast to normal <see cref="CombineAll{T}(System.Collections.Generic.IEnumerable{CSharpFunctionalExtensions.Result{T,DomainError}})"/>
    /// which will execute all operations and then `Bind` them together
    /// this operator won't just return either Result.Success or Result.Failure but a type containing both succeeded and failed operations so that consumer will have a chance to process both of them.
    /// Useful in situations when single failure in collection of results should not fail the whole operation.
    /// </summary>
    /// <param name="tasks">Collection of tasks and unique key attached to them</param>
    /// <typeparam name="TKey">Type of key</typeparam>
    /// <typeparam name="TResult">Result's success type</typeparam>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<PartialSuccessResponse<TKey, TResult, DomainError>> CombineWithFailures<TKey, TResult>(
        this IEnumerable<(TKey key, Task<Result<TResult, DomainError>> task)> tasks)
    {
        ImmutableList<(TKey key, Result<TResult, DomainError> result)> results = ImmutableList<(TKey key, Result<TResult, DomainError> result)>.Empty;
        foreach (var task in tasks)
        {
            Result<TResult, DomainError> result = await task.task;
            results = results.Add((task.key, result));
        }

        return results.Aggregate(
            PartialSuccessResponse<TKey, TResult, DomainError>.Empty,
            (acc, next) => next.result.IsFailure
                ? acc.AddFailure(next.key, next.result.Error)
                : acc.AddSuccess(next.key, next.result.Value));
    }
    [DebuggerStepThrough]
    public static async Task<Result<ImmutableList<T>, DomainError>> CombineSequentially<T>(this IEnumerable<Task<Result<T, DomainError>>> tasks)
    {
        ImmutableList<Result<T, DomainError>> results = ImmutableList<Result<T, DomainError>>.Empty;
        foreach (var task in tasks)
        {
            Result<T, DomainError> result = await task;
            results = results.Add(result);
        }


        return results.Combine(errs => errs.Combine()).Map(enumerable => enumerable.ToImmutableList());
    }
    [DebuggerStepThrough]
    public static Result<None, TE> Ignore<T, TE>(this Result<T, TE> result) => result.Map(_ => None.Value);
    [DebuggerStepThrough]
    public static Task<Result<None, TE>> Ignore<T, TE>(this Task<Result<T, TE>> result) => result.Map(_ => None.Value);

    [DebuggerStepThrough]
    public static Result<T, TE> AsResult<T, TE>(this T some) where TE : DomainError
        => Result.Success<T, TE>(some);

    [DebuggerStepThrough]
    public static Result<T, DomainError> AsResult<T>(this T some)
        => Result.Success<T, DomainError>(some);

    [DebuggerStepThrough]
    public static Result<T, TE> Ensure<T, TE>(this T obj, Predicate<T> predicate, Func<T, TE> error) => predicate(obj)
        ? Result.Success<T, TE>(obj)
        : Result.Failure<T, TE>(error(obj));

    [DebuggerStepThrough]
    public static Result<T, ValidationError> Validate<T>(this T obj,
        params (Predicate<T> predicate, Func<T, ValidationError> error)[] predicates) =>
        predicates
            .Aggregate(
                ImmutableList<Result<T, ValidationError>>.Empty,
                (acc, next) => next.predicate(obj)
                    ? acc.Add(obj.AsResult<T, ValidationError>())
                    : acc.Add(Result.Failure<T, ValidationError>(next.error(obj))))
            .Combine(
                _ => obj,
                errs => errs.Compose());

    [DebuggerStepThrough]
    public static async Task<Result<IReadOnlyList<K>, DomainError>> Bind<T, K, TE>(this Task<Result<T, TE>> task, Func<T, IAsyncEnumerable<K>> asyncEnum) where TE : DomainError
    {
        var taskResult = await task;
        if (taskResult.IsFailure)
            return Result.Failure<IReadOnlyList<K>, DomainError>(taskResult.Error);
        var list = new List<K>();
        await foreach (var entry in asyncEnum(taskResult.Value))
        {
            list.Add(entry);
        }

        return list;
    }
    [DebuggerStepThrough]
    public static async Task<Result<IReadOnlyList<T>, DomainError>> AsResult<T>(IAsyncEnumerable<T> asyncEnum)
    {
        var list = new List<T>();
        await foreach (var entry in asyncEnum)
        {
            list.Add(entry);
        }
        return list;
    }
    [DebuggerStepThrough]
    private static ValidationError Compose(this IEnumerable<ValidationError> errors) =>
        new(string.Join(";\n", errors.Select(e => e.Message)));

    [DebuggerStepThrough]
    public static async Task<Result<K, DomainError>> BindIf<T, K>(
        this Task<Result<T, DomainError>> task,
        Predicate<T> predicate, Func<T, Task<Result<K, DomainError>>> next,
        K defaultValue)
    {
        var result = await task.ConfigureAwait(false);
        if (result.IsFailure)
            return result.Error;
        if (predicate(result.Value))
        {
            return await result
                .Bind(next)
                .ConfigureAwait(false);
        }

        return defaultValue;
    }
}

public static class AsyncEnumerableExtensions
{
    [DebuggerStepThrough]
    public static async Task<Result<IReadOnlyList<K>, DomainError>> Bind<T, K, TE>(this Task<Result<T, TE>> task, Func<T, IAsyncEnumerable<K>> asyncEnum) where TE : DomainError
    {
        var taskResult = await task;
        if (taskResult.IsFailure)
            return Result.Failure<IReadOnlyList<K>, DomainError>(taskResult.Error);
        var list = new List<K>();
        await foreach (var entry in asyncEnum(taskResult.Value))
        {
            list.Add(entry);
        }

        return list;
    }
    [DebuggerStepThrough]
    public static async Task<Result<IReadOnlyList<T>, DomainError>> EnumerateResult<T>(this IAsyncEnumerable<T> asyncEnum)
    {
        try
        {
            var list = new List<T>();
            await foreach (var entry in asyncEnum)
            {
                list.Add(entry);
            }

            return list;
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }

    }

    public static async Task<Result<K, DomainError>> Map<T, K>(this IAsyncEnumerable<T> asyncEnum, Func<IReadOnlyList<T>, K> mapper) =>
        await asyncEnum
            .EnumerateResult()
            .Map(mapper);
}
