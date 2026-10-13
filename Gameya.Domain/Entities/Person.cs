using Gameya.Domain.Common;
using Gameya.Domain.Rules;
using Gameya.Domain.ValueObjects;

namespace Gameya.Domain.Entities;

public sealed class Person : AggregateRoot
{
    private Person() { }

    public Person(string fullName, string phoneNumber, SubscriberCode subscriberCode, DateTimeOffset now)
    {
        SetName(fullName, now);
        Guard.Against(string.IsNullOrWhiteSpace(phoneNumber), "person.phone_required", "Phone number is required.");
        PhoneNumber = phoneNumber.Trim();
        SubscriberCode = subscriberCode;
        IsActive = true;
        CreatedAtUtc = now;
    }

    public string FullName { get; private set; } = string.Empty;
    public string NormalizedFullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public SubscriberCode SubscriberCode { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void SetName(string fullName, DateTimeOffset now)
    {
        Guard.Against(string.IsNullOrWhiteSpace(fullName), "person.name_required", "Full name is required.");
        FullName = fullName.Trim();
        NormalizedFullName = NormalizeName(FullName);
        UpdatedAtUtc = now;
    }

    public void Deactivate(DateTimeOffset now) { IsActive = false; UpdatedAtUtc = now; }

    public static string NormalizeName(string name) => string.Join(' ', name.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
