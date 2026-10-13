using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gameya.Tests.Integration;

public sealed class PersistenceInvariantTests
{
    [Fact]
    public async Task Historical_payment_cannot_be_deleted()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"immutability-{Guid.NewGuid():N}").Options;
        await using var db = new ApplicationDbContext(options, NullLogger<ApplicationDbContext>.Instance);
        var payment = new Payment(Guid.NewGuid(), "owner", 100m, PaymentMethod.Cash,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        db.PaymentsSet.Add(payment); await db.SaveChangesAsync();
        db.PaymentsSet.Remove(payment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Financial_rows_use_optimistic_concurrency_tokens()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"model-{Guid.NewGuid():N}").Options;
        using var db = new ApplicationDbContext(options, NullLogger<ApplicationDbContext>.Instance);
        Assert.True(db.Model.FindEntityType(typeof(Payment))!.FindProperty(nameof(Payment.RowVersion))!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(Association))!.FindProperty(nameof(Association.RowVersion))!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(PayoutAssignment))!.FindProperty(nameof(PayoutAssignment.RowVersion))!.IsConcurrencyToken);
    }
}
