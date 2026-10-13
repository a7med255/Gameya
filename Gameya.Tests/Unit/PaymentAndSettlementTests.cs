using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Domain.Exceptions;

namespace Gameya.Tests.Unit;

public sealed class PaymentAndSettlementTests
{
    [Fact]
    public void Allocation_retains_historical_due_date_and_late_days()
    {
        var paidAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var payment = new Payment(Guid.NewGuid(), "user", 5_000m, PaymentMethod.Cash, paidAt, paidAt);
        payment.Allocate(Guid.NewGuid(), Guid.NewGuid(), 5_000m, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15));
        var allocation = Assert.Single(payment.Allocations);
        Assert.Equal(5, allocation.LateDays);
        Assert.Equal(new DateOnly(2026, 1, 10), allocation.DueDate);
    }

    [Fact]
    public void Payment_cannot_be_overallocated()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = new Payment(Guid.NewGuid(), "user", 5_000m, PaymentMethod.InstaPay, now, now);
        payment.Allocate(Guid.NewGuid(), Guid.NewGuid(), 4_000m, DateOnly.FromDateTime(now.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime));
        var error = Assert.Throws<DomainException>(() =>
            payment.Allocate(Guid.NewGuid(), Guid.NewGuid(), 2_000m, DateOnly.FromDateTime(now.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime)));
        Assert.Equal("payment.over_allocated", error.Code);
    }

    [Fact]
    public void Correction_is_a_separate_compensating_transaction()
    {
        var originalId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var adjustment = new Payment(Guid.NewGuid(), "owner", -1_000m, PaymentMethod.Cash, now, now,
            PaymentEntryType.Adjustment, originalId);
        Assert.Equal(PaymentEntryType.Adjustment, adjustment.EntryType);
        Assert.Equal(originalId, adjustment.AdjustsPaymentId);
        Assert.Equal(-1_000m, adjustment.Amount);
    }

    [Fact]
    public void Settlement_balance_is_paid_minus_required()
    {
        var settlement = new Settlement(Guid.NewGuid(), Guid.NewGuid(), 20_000m, 30_000m, DateTimeOffset.UtcNow);
        Assert.Equal(10_000m, settlement.Balance);
        Assert.Equal(SettlementStatus.Pending, settlement.Status);
    }
}
