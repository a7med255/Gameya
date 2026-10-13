namespace Gameya.Application.Common;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Owner = "Owner";
    public const string PaymentUser = "PaymentUser";
    public const string Participant = "Participant";
    public const string Staff = Admin + "," + Owner + "," + PaymentUser;
}

public static class AppPolicies
{
    public const string OwnerOrAdmin = nameof(OwnerOrAdmin);
    public const string CanRecordPayments = nameof(CanRecordPayments);
    public const string ParticipantOnly = nameof(ParticipantOnly);
}
