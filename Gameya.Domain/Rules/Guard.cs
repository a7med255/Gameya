using Gameya.Domain.Exceptions;

namespace Gameya.Domain.Rules;

internal static class Guard
{
    public static void Against(bool condition, string code, string message)
    {
        if (condition) throw new DomainException(code, message);
    }
}
