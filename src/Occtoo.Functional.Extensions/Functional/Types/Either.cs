using CSharpFunctionalExtensions;

namespace Occtoo.Functional.Extensions.Functional.Types;

/// <summary>
/// Models the choice or union type. Very useful in situation when method/operation should logically return
/// either one or another type depending on some logic. 
/// </summary>
/// <typeparam name="T1"></typeparam>
/// <typeparam name="T2"></typeparam>
public class Either<T1, T2>
{
    private readonly Maybe<T1> _first;
    private readonly Maybe<T2> _second;

    public Either(Maybe<T1> first, Maybe<T2> second)
    {
        _first = first;
        _second = second;
    }

    public static Either<T1, T2> From(T1 first) => new(Maybe<T1>.From(first), Maybe<T2>.None);

    public static Either<T1, T2> From(T2 second) => new(Maybe<T1>.None, Maybe<T2>.From(second));

    public T Match<T>(Func<T1, T> matchFirst, Func<T2, T> matchSecond)
        => _first.Match(
            matchFirst,
            () => matchSecond(_second.Value));

    public Either<T3, T4> Map<T3, T4>(Func<T1, T3> matchFirst, Func<T2, T4> matchSecond)
    {
        return _first.Match(first =>
                Either<T3, T4>.From(matchFirst(first)),
            () => Either<T3, T4>.From(matchSecond(_second.Value)));
    }
}