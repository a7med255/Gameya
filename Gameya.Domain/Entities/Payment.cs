using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Events;
using Gameya.Domain.Rules;

namespace Gameya.Domain.Entities;

public sealed class Payment : AggregateRoot
{
    private readonly List<PaymentAllocation> _allocations = [];
    private Payment() { }

    public Payment(Guid personId, string recordedByUserId, decimal amount, PaymentMethod method,
        DateTimeOffset paidAtUtc, DateTimeOffset createdAtUtc, PaymentEntryType entryType = PaymentEntryType.Payment,
        Guid? adjustsPaymentId = null)
    {
        Guard.Against(amount == 0, "payment.zero_amount", "Payment amount cannot be zero.");
        Guard.Against(entryType == PaymentEntryType.Payment && amount < 0, "payment.negative", "A payment must be positive.");
        Guard.Against(entryType == PaymentEntryType.Adjustment && adjustsPaymentId is null,
            "payment.adjustment_reference", "An adjustment must reference the corrected payment.");
        PersonId = personId;
        RecordedByUserId = recordedByUserId;
        Amount = amount;
        Method = method;
        PaidAtUtc = paidAtUtc;
        CreatedAtUtc = createdAtUtc;
        EntryType = entryType;
        AdjustsPaymentId = adjustsPaymentId;
        Raise(new PaymentRecorded(Id, personId, amount, createdAtUtc));
    }

    public Guid PersonId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public PaymentEntryType EntryType { get; private set; }
    public Guid? AdjustsPaymentId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations.AsReadOnly();

    public void Allocate(Guid associationId, Guid periodId, decimal amount, DateOnly dueDate, DateOnly paidDate)
    {
        Guard.Against(amount <= 0, "payment.invalid_allocation", "Allocated amount must be positive.");
        Guard.Against(_allocations.Sum(x => x.AllocatedAmount) + amount > Amount,
            "payment.over_allocated", "Allocations cannot exceed the payment amount.");
        _allocations.Add(new PaymentAllocation(Id, associationId, periodId, amount, dueDate,
            Math.Max(0, paidDate.DayNumber - dueDate.DayNumber)));
    }

    public void AllocateAdjustment(Guid associationId, Guid periodId, decimal amount, DateOnly dueDate)
    {
        Guard.Against(EntryType != PaymentEntryType.Adjustment, "payment.not_adjustment", "Only an adjustment can have signed allocations.");
        Guard.Against(amount == 0, "payment.invalid_allocation", "Adjustment allocation cannot be zero.");
        _allocations.Add(new PaymentAllocation(Id, associationId, periodId, amount, dueDate, 0));
    }
}

public sealed class PaymentAllocation : Entity
{
    private PaymentAllocation() { }
    internal PaymentAllocation(Guid paymentId, Guid associationId, Guid periodId, decimal amount, DateOnly dueDate, int lateDays)
    {
        PaymentId = paymentId;
        AssociationId = associationId;
        PeriodId = periodId;
        AllocatedAmount = amount;
        DueDate = dueDate;
        LateDays = lateDays;
    }
    public Guid PaymentId { get; private set; }
    public Guid AssociationId { get; private set; }
    public Guid PeriodId { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public DateOnly DueDate { get; private set; }
    public int LateDays { get; private set; }
}

public sealed class AssociationCredit : Entity
{
    private AssociationCredit() { }
    public AssociationCredit(Guid paymentId, Guid personId, Guid associationId, decimal amount, DateTimeOffset createdAtUtc)
    {
        Guard.Against(amount == 0, "credit.invalid_amount", "Credit amount cannot be zero.");
        PaymentId = paymentId;
        PersonId = personId;
        AssociationId = associationId;
        Amount = amount;
        CreatedAtUtc = createdAtUtc;
    }
    public Guid PaymentId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid AssociationId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class AssociationCreditApplication : Entity
{
    private AssociationCreditApplication() { }
    public AssociationCreditApplication(Guid personId, Guid associationId, Guid periodId, decimal amount, DateTimeOffset appliedAtUtc)
    {
        Guard.Against(amount <= 0, "credit.invalid_application", "Applied credit must be positive.");
        PersonId = personId; AssociationId = associationId; PeriodId = periodId; Amount = amount; AppliedAtUtc = appliedAtUtc;
    }
    public Guid PersonId { get; private set; }
    public Guid AssociationId { get; private set; }
    public Guid PeriodId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }
}
