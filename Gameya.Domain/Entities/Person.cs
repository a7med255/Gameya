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
        PhoneNumber = NormalizePhone(phoneNumber);
        NormalizedPhoneNumber = PhoneNumber;
        Guard.Against(!IsValidEgyptianMobile(NormalizedPhoneNumber), "person.invalid_phone",
            "A valid Egyptian mobile number is required.");
        SubscriberCode = subscriberCode;
        IsActive = true;
        CreatedAtUtc = now;
    }

    public string FullName { get; private set; } = string.Empty;
    public string NormalizedFullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string NormalizedPhoneNumber { get; private set; } = string.Empty;
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

    public void UpdateDetails(string fullName, string phoneNumber, DateTimeOffset now)
    {
        SetName(fullName, now);
        Guard.Against(string.IsNullOrWhiteSpace(phoneNumber), "person.phone_required", "Phone number is required.");
        PhoneNumber = NormalizePhone(phoneNumber);
        NormalizedPhoneNumber = PhoneNumber;
        Guard.Against(!IsValidEgyptianMobile(NormalizedPhoneNumber), "person.invalid_phone",
            "A valid Egyptian mobile number is required.");
        UpdatedAtUtc = now;
    }

    public void Deactivate(DateTimeOffset now) { IsActive = false; UpdatedAtUtc = now; }

    public static string NormalizeName(string name) => string.Join(' ', name.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    public static string NormalizePhone(string phoneNumber)
    {
        var digits = string.Concat(phoneNumber.Where(char.IsAsciiDigit));
        if (digits.StartsWith("0020", StringComparison.Ordinal) && digits.Length == 14) digits = digits[4..];
        else if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        if (digits.Length == 10 && digits.StartsWith('1')) digits = $"0{digits}";
        return digits;
    }

    public static bool IsValidEgyptianMobile(string phoneNumber)
    {
        var normalized = NormalizePhone(phoneNumber);
        return normalized.Length == 11 && normalized.StartsWith("01", StringComparison.Ordinal)
            && normalized[2] is '0' or '1' or '2' or '5' && normalized.All(char.IsAsciiDigit);
    }

    public static string[] PhoneVariants(string phoneNumber)
    {
        var normalized = NormalizePhone(phoneNumber);
        if (!IsValidEgyptianMobile(normalized)) return [normalized];
        var international = $"20{normalized[1..]}";
        return [normalized, international, $"+{international}", $"00{international}"];
    }
}
