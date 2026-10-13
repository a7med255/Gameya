using System.Text.RegularExpressions;
using Gameya.Domain.Exceptions;

namespace Gameya.Domain.ValueObjects;

public sealed partial record SubscriberCode
{
    public const int Length = 8;
    public string Value { get; }

    private SubscriberCode(string value) => Value = value;

    public static SubscriberCode Create(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized))
            throw new DomainException("person.invalid_subscriber_code", "Subscriber code must contain eight uppercase letters or digits.");
        return new SubscriberCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
