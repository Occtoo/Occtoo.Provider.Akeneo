using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Occtoo.Functional.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns true if the <see cref="str"/> is not null, empty or white space
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool NotEmpty(this string str) => !str.Empty();

        /// <summary>
        /// Returns true if the string is null, empty or whitespace
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool Empty(this string str) => string.IsNullOrWhiteSpace(str);


        public static readonly Regex ValidUrlPattern = new Regex(
            @"(?<scheme>https:\/\/|http:\/\/)(?<host>[a-zA-Z0-9-.]*)(?<port>:\d{2,5})?(?<endpoint>\/[a-zA-Z0-9-/]*)?(?<queryString>\?[^\?]*)?$",
            RegexOptions.Compiled
        );

        public static bool IsValidUrl(this string str) => ValidUrlPattern.IsMatch(str);

        public static string FromBase64(this string str) => Encoding.UTF8.GetString(Convert.FromBase64String(str));

        public static string ToBase64(this string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));

        /// <summary>
        /// If the <see cref="original"/> is empty uses <see cref="other"/> as a value
        /// </summary>
        /// <param name="original">original string</param>
        /// <param name="other">replacement for original string in case it's empty</param>
        /// <returns></returns>
        public static string IfEmpty(this string original, string other) => original.Empty() ? other : original;

        public static string Enquote(this string s, char quote = '\"') => $"{quote}{s}{quote}";

        public static string Capitalize(this string s) => s.Empty() ? s : char.ToUpper(s[0]) + s.Substring(1);
        public static string ToCamelCase(this string s) => s switch
        {
            null => null,
            "" => "",
            _ => s.Length == 1 ? char.ToLower(s[0]).ToString() : char.ToLower(s[0]) + s[1..]
        };

        public static ImmutableArray<string> SplitRemoveEmptyEntries(this string s, string splitBy) => s.Split(splitBy, StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();

        public static bool EqualsIgnoreCase(this string first, string second) =>
            string.Equals(first, second, StringComparison.InvariantCultureIgnoreCase);

        public static bool ContainsIgnoreCase(this string s, string contains) =>
            s.Contains(contains, StringComparison.InvariantCultureIgnoreCase);

        public static bool DoesNotParseInto<T>(this string s) where T : struct => !Enum.TryParse<T>(s, true, out _);

        public static bool IsEmpty(this string[]? s) => s is { Length: 0 };
        public static bool IsNotEmpty(this string[]? s) => s is { Length: > 0 };

        public static bool IsUri(this string s) => Uri.TryCreate(s, UriKind.Absolute, out _);
    }
}
