using CSharpFunctionalExtensions;
using System.Diagnostics;

namespace Occtoo.Functional.Extensions;

public static class MaybeExtensions
{
    /// <summary>
    /// Converts string to maybe. Empty, null, or whitespace strings become `Maybe.None`
    /// </summary>
    [DebuggerStepThrough]
    public static Maybe<string> ToMaybe(this string input) => input.NotEmpty()
        ? Maybe<string>.From(input)
        : Maybe<string>.None;
    public static Maybe<T> ToMaybe<T>(this T? input) where T : struct => input.HasValue
        ? input.Value!
        : Maybe<T>.None;
}