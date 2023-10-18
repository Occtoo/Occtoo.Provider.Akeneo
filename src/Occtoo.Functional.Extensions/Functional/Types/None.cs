namespace Occtoo.Functional.Extensions.Functional.Types;

public record None
{
    private None() { }
    public static None Value => new();
}