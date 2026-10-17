using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.Features.Payments;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Domain.ValueObjects;
using Gameya.Infrastructure.Persistence;
using Gameya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gameya.Tests.Unit;

public sealed class PaymentSmartAllocationScenariosTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; } = Guid.NewGuid().ToString("N");
        public Guid? PersonId { get; init; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => true;
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class TestClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(3));
        public DateOnly Today => today;
        public DateTimeOffset EgyptStartOfDayUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        public DateOnly EgyptDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"GameyaAllocationTests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, NullLogger<ApplicationDbContext>.Instance);
    }

    private static (Person Person, Association AssocA, Association AssocB, Association AssocC) SetupThreeAssociations(
        ApplicationDbContext db,
        DateOnly startMonth,
        string ownerId,
        int months = 4,
        decimal amountA = 1_000m,
        decimal amountB = 2_000m,
        decimal amountC = 2_000m,
        decimal sharesA = 1m,
        decimal sharesB = 1m,
        decimal sharesC = 1m)
    {
        var person = new Person("Ahmed Mohamed", "+201012345678", SubscriberCode.Create("AHMD1234"), DateTimeOffset.UtcNow.AddMonths(-months));
        db.PeopleSet.Add(person);

        Association CreateAssoc(string name, decimal monthlyContribution, decimal shares)
        {
            var assoc = new Association(name, startMonth, months, monthlyContribution, ownerId, DateTimeOffset.UtcNow.AddMonths(-months));
            var dates = new List<(DateOnly PeriodDate, DateOnly DueDate)>();
            for (int i = 0; i < months; i++)
            {
                var pDate = startMonth.AddMonths(i);
                dates.Add((pDate, new DateOnly(pDate.Year, pDate.Month, 10)));
            }
            assoc.SetPeriods(dates);
            var mem = assoc.AddMember(person.Id, shares, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-months));
            assoc.AddPayout(assoc.Periods.First().Id, PayoutRecipientType.Membership, mem.Id, shares, DateTimeOffset.UtcNow.AddMonths(-months));
            foreach (var p in assoc.Periods.Skip(1))
            {
                assoc.AddPayout(p.Id, PayoutRecipientType.ReserveSlot, null, 1m, DateTimeOffset.UtcNow.AddMonths(-months));
            }
            assoc.Start(DateTimeOffset.UtcNow.AddMonths(-months + 1));
            db.AssociationsSet.Add(assoc);
            return assoc;
        }

        var a = CreateAssoc("Association A", amountA, sharesA);
        var b = CreateAssoc("Association B", amountB, sharesB);
        var c = CreateAssoc("Association C", amountC, sharesC);

        db.SaveChanges();
        return (person, a, b, c);
    }

    private static PaymentAllocationService CreateService(ApplicationDbContext db, ICurrentUser user, IClock clock)
    {
        var access = new AssociationAccessService(db, user);
        return new PaymentAllocationService(db, user, access, clock);
    }

    [Fact]
    public async Task Scenario1_And_Scenario2_FullCurrentMonthPayment_AllocatesProportionallyAndSettlesAll()
    {
        // Scenario 1: Person belongs to three associations with different monthly contributions: A: 1,000, B: 2,000, C: 2,000
        // Scenario 2: Full current-month payment of 5,000 EGP
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);
        var page = await service.GetPageAsync(person.Id, CancellationToken.None);

        Assert.Equal(5_000m, page.CurrentDue);
        Assert.Equal(0m, page.PreviousOverdue);
        Assert.Equal(5_000m, page.AmountDueNow);
        Assert.Equal(3, page.Associations.Count);

        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 5_000m), CancellationToken.None);

        Assert.Equal(3, plan.Allocations.Count);
        var allocA = plan.Allocations.Single(x => x.AssociationId == a.Id);
        var allocB = plan.Allocations.Single(x => x.AssociationId == b.Id);
        var allocC = plan.Allocations.Single(x => x.AssociationId == c.Id);

        Assert.Equal(1_000m, allocA.Amount);
        Assert.Equal(2_000m, allocB.Amount);
        Assert.Equal(2_000m, allocC.Amount);
        Assert.Equal(5_000m, plan.Allocations.Sum(x => x.Amount));
        Assert.Equal(0m, plan.CreditApplications.Sum(x => x.Amount));

        // Persist payment and verify invariants
        var payment = new Payment(person.Id, user.UserId!, 5_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in plan.Allocations)
            payment.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(payment);
        await db.SaveChangesAsync();

        // Recalculate page: current month should be 0 remaining (fully paid)
        var updatedPage = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(0m, updatedPage.CurrentDue);
        Assert.Equal(0m, updatedPage.AmountDueNow);
        Assert.All(updatedPage.Associations, assoc => Assert.Equal(0m, assoc.CurrentRemaining));
    }

    [Fact]
    public async Task Scenario3_PartialCurrentMonthPayment_PreservesRemainingBalances()
    {
        // Scenario 3: Partial payment of 2,500 EGP against 5,000 EGP dues
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 2_500m), CancellationToken.None);

        Assert.Equal(3, plan.Allocations.Count);
        var allocA = plan.Allocations.Single(x => x.AssociationId == a.Id);
        var allocB = plan.Allocations.Single(x => x.AssociationId == b.Id);
        var allocC = plan.Allocations.Single(x => x.AssociationId == c.Id);

        // Proportional split: 1000/5000 * 2500 = 500, 2000/5000 * 2500 = 1000, 2000/5000 * 2500 = 1000
        Assert.Equal(500m, allocA.Amount);
        Assert.Equal(1_000m, allocB.Amount);
        Assert.Equal(1_000m, allocC.Amount);
        Assert.Equal(2_500m, plan.Allocations.Sum(x => x.Amount));

        // Persist and verify remaining dues
        var payment = new Payment(person.Id, user.UserId!, 2_500m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in plan.Allocations)
            payment.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(payment);
        await db.SaveChangesAsync();

        var page = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(2_500m, page.CurrentDue);
        Assert.Equal(500m, page.Associations.Single(x => x.AssociationId == a.Id).CurrentRemaining);
        Assert.Equal(1_000m, page.Associations.Single(x => x.AssociationId == b.Id).CurrentRemaining);
        Assert.Equal(1_000m, page.Associations.Single(x => x.AssociationId == c.Id).CurrentRemaining);
    }

    [Fact]
    public async Task Scenario4_And_Scenario5_PreviousArrearsPriority_AllocatesOldestFirst()
    {
        // Scenario 4: September arrears (A: 1,000, B: 2,000, C: 2,000) + October current
        // Scenario 5: Payment of 1,200 is insufficient to clear older arrears -> 0 allocated to October
        await using var db = CreateDbContext();
        var prevMonth = new DateOnly(2026, 9, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();

        var (person, a, b, c) = SetupThreeAssociations(db, prevMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);
        var page = await service.GetPageAsync(person.Id, CancellationToken.None);

        Assert.True(page.PreviousOverdue > 0, "Should have previous overdue from previousMonth.");

        // Payment of 1,200 EGP
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 1_200m), CancellationToken.None);

        // All 1,200 must be allocated to previousMonth periods only; 0 to currentMonth
        Assert.All(plan.Allocations, al =>
        {
            Assert.Equal(prevMonth, new DateOnly(al.DueDate.Year, al.DueDate.Month, 1));
        });
        Assert.Equal(1_200m, plan.Allocations.Sum(x => x.Amount));
    }

    [Fact]
    public async Task Scenario6_ClearsArrearsAndPartiallyPaysCurrentMonth()
    {
        // Scenario 6: Older arrears 1,000 on A. Current month dues 5,000 across A, B, C.
        // Payment of 3,500 clears 1,000 arrears on A, and remaining 2,500 splits proportionally across October
        await using var db = CreateDbContext();
        var prevMonth = new DateOnly(2026, 9, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, prevMonth, user.UserId!, months: 3);

        // Pre-settle previousMonth for B and C so only A has 1,000 arrear in prevMonth
        var periodBPrev = b.Periods.First();
        var periodCPrev = c.Periods.First();
        var prePayment = new Payment(person.Id, user.UserId!, 4_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddMonths(-1));
        prePayment.Allocate(b.Id, periodBPrev.Id, 2_000m, periodBPrev.DueDate, periodBPrev.DueDate);
        prePayment.Allocate(c.Id, periodCPrev.Id, 2_000m, periodCPrev.DueDate, periodCPrev.DueDate);
        db.PaymentsSet.Add(prePayment);
        await db.SaveChangesAsync();

        var service = CreateService(db, user, clock);
        var page = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(1_000m, page.PreviousOverdue);
        Assert.Equal(5_000m, page.CurrentDue);

        // Now pay 3,500
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 3_500m), CancellationToken.None);

        Assert.Equal(3_500m, plan.Allocations.Sum(x => x.Amount));

        // 1,000 to previousMonth of A
        var prevAllocA = plan.Allocations.Single(x => x.AssociationId == a.Id && x.PeriodId == a.Periods.First().Id);
        Assert.Equal(1_000m, prevAllocA.Amount);

        // Remaining 2,500 to currentMonth across A(500), B(1000), C(1000)
        var curAllocA = plan.Allocations.Single(x => x.AssociationId == a.Id && x.PeriodId == a.Periods.ElementAt(1).Id);
        var curAllocB = plan.Allocations.Single(x => x.AssociationId == b.Id && x.PeriodId == b.Periods.ElementAt(1).Id);
        var curAllocC = plan.Allocations.Single(x => x.AssociationId == c.Id && x.PeriodId == c.Periods.ElementAt(1).Id);

        Assert.Equal(500m, curAllocA.Amount);
        Assert.Equal(1_000m, curAllocB.Amount);
        Assert.Equal(1_000m, curAllocC.Amount);
    }

    [Fact]
    public async Task Scenario7_ClearsArrearsCurrentMonthAndPartiallyPaysAdvanceMonth()
    {
        // Scenario 7: Arrears 1,000 on A, Current 5,000, Advance 5,000.
        // Pays 8,000 with AdvanceMonth requested -> 1,000 arrears + 5,000 current + 2,000 advance
        await using var db = CreateDbContext();
        var prevMonth = new DateOnly(2026, 9, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, prevMonth, user.UserId!, months: 4);

        // Pre-settle B and C for prevMonth
        var prePayment = new Payment(person.Id, user.UserId!, 4_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddMonths(-1));
        prePayment.Allocate(b.Id, b.Periods.First().Id, 2_000m, b.Periods.First().DueDate, b.Periods.First().DueDate);
        prePayment.Allocate(c.Id, c.Periods.First().Id, 2_000m, c.Periods.First().DueDate, c.Periods.First().DueDate);
        db.PaymentsSet.Add(prePayment);
        await db.SaveChangesAsync();

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 8_000m, AdvanceYear: 2026, AdvanceMonth: 11), CancellationToken.None);

        Assert.Equal(8_000m, plan.Allocations.Sum(x => x.Amount));

        // 1,000 to prev
        Assert.Equal(1_000m, plan.Allocations.Where(x => x.PeriodId == a.Periods.First().Id).Sum(x => x.Amount));
        // 5,000 to current (period index 1)
        var curAllocations = plan.Allocations.Where(x => x.PeriodId == a.Periods.ElementAt(1).Id || x.PeriodId == b.Periods.ElementAt(1).Id || x.PeriodId == c.Periods.ElementAt(1).Id);
        Assert.Equal(5_000m, curAllocations.Sum(x => x.Amount));
        // 2,000 to advance (period index 2)
        var advAllocations = plan.Allocations.Where(x => x.PeriodId == a.Periods.ElementAt(2).Id || x.PeriodId == b.Periods.ElementAt(2).Id || x.PeriodId == c.Periods.ElementAt(2).Id).ToList();
        Assert.Equal(2_000m, advAllocations.Sum(x => x.Amount));

        // Verify proportional split in advance: 1000 : 2000 : 2000 -> 400 : 800 : 800
        Assert.Equal(400m, advAllocations.Single(x => x.AssociationId == a.Id).Amount);
        Assert.Equal(800m, advAllocations.Single(x => x.AssociationId == b.Id).Amount);
        Assert.Equal(800m, advAllocations.Single(x => x.AssociationId == c.Id).Amount);
    }

    [Fact]
    public async Task Scenario8_AdvancePaymentWhenPreviousAndCurrentAreSettled()
    {
        // Scenario 8: Advance payment of 5,000 when arrears & current are 0
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        // Pre-settle current month completely
        var prePayment = new Payment(person.Id, user.UserId!, 5_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        prePayment.Allocate(a.Id, a.Periods.First().Id, 1_000m, a.Periods.First().DueDate, a.Periods.First().DueDate);
        prePayment.Allocate(b.Id, b.Periods.First().Id, 2_000m, b.Periods.First().DueDate, b.Periods.First().DueDate);
        prePayment.Allocate(c.Id, c.Periods.First().Id, 2_000m, c.Periods.First().DueDate, c.Periods.First().DueDate);
        db.PaymentsSet.Add(prePayment);
        await db.SaveChangesAsync();

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 5_000m, AdvanceYear: 2026, AdvanceMonth: 11), CancellationToken.None);

        Assert.Equal(3, plan.Allocations.Count);
        Assert.Equal(5_000m, plan.Allocations.Sum(x => x.Amount));
        Assert.Equal(1_000m, plan.Allocations.Single(x => x.AssociationId == a.Id).Amount);
        Assert.Equal(2_000m, plan.Allocations.Single(x => x.AssociationId == b.Id).Amount);
        Assert.Equal(2_000m, plan.Allocations.Single(x => x.AssociationId == c.Id).Amount);
    }

    [Fact]
    public async Task Scenario9_PartialAdvancePayment_WithSpecifiedAdvanceAmount()
    {
        // Scenario 9: Current is 5,000. Advance requested is 3,000. Total payment = 8,000.
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 8_000m, AdvanceYear: 2026, AdvanceMonth: 11, AdvanceAmount: 3_000m), CancellationToken.None);

        Assert.Equal(8_000m, plan.Allocations.Sum(x => x.Amount));
        var advAllocations = plan.Allocations.Where(x => x.PeriodId == a.Periods.ElementAt(1).Id || x.PeriodId == b.Periods.ElementAt(1).Id || x.PeriodId == c.Periods.ElementAt(1).Id).ToList();
        Assert.Equal(3_000m, advAllocations.Sum(x => x.Amount));

        // Proportional: 1000/5000*3000 = 600, 2000/5000*3000 = 1200, 2000/5000*3000 = 1200
        Assert.Equal(600m, advAllocations.Single(x => x.AssociationId == a.Id).Amount);
        Assert.Equal(1_200m, advAllocations.Single(x => x.AssociationId == b.Id).Amount);
        Assert.Equal(1_200m, advAllocations.Single(x => x.AssociationId == c.Id).Amount);
    }

    [Fact]
    public async Task Scenario10_SecondPaymentOnPartiallyPaidObligations_UsesRemainingBalancesAsWeights()
    {
        // Scenario 10: After paying 2,500 leaving A: 500, B: 1,000, C: 1,000,
        // a second payment of 1,500 must use remaining balances as weights:
        // A: 500/2500 * 1500 = 300, B: 1000/2500 * 1500 = 600, C: 1000/2500 * 1500 = 600
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);

        // Payment 1: 2,500
        var plan1 = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 2_500m), CancellationToken.None);
        var payment1 = new Payment(person.Id, user.UserId!, 2_500m, PaymentMethod.Cash, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2));
        foreach (var al in plan1.Allocations)
            payment1.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(payment1);
        await db.SaveChangesAsync();

        // Payment 2: 1,500
        var plan2 = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 1_500m), CancellationToken.None);

        var allocA = plan2.Allocations.Single(x => x.AssociationId == a.Id);
        var allocB = plan2.Allocations.Single(x => x.AssociationId == b.Id);
        var allocC = plan2.Allocations.Single(x => x.AssociationId == c.Id);

        Assert.Equal(300m, allocA.Amount);
        Assert.Equal(600m, allocB.Amount);
        Assert.Equal(600m, allocC.Amount);
        Assert.Equal(1_500m, plan2.Allocations.Sum(x => x.Amount));

        // Persist payment 2 and verify remaining
        var payment2 = new Payment(person.Id, user.UserId!, 1_500m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in plan2.Allocations)
            payment2.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(payment2);
        await db.SaveChangesAsync();

        var page = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(1_000m, page.CurrentDue);
        Assert.Equal(200m, page.Associations.Single(x => x.AssociationId == a.Id).CurrentRemaining);
        Assert.Equal(400m, page.Associations.Single(x => x.AssociationId == b.Id).CurrentRemaining);
        Assert.Equal(400m, page.Associations.Single(x => x.AssociationId == c.Id).CurrentRemaining);
    }

    [Fact]
    public async Task Scenario11_CancelledAssociation_DoesNotGenerateActiveOrPayableObligations()
    {
        // Scenario 11: Association C is cancelled
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, c) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        c.Cancel(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var service = CreateService(db, user, clock);
        var page = await service.GetPageAsync(person.Id, CancellationToken.None);

        // Cancelled association should have 0 CurrentRequired and 0 CurrentRemaining
        var cDue = page.Associations.Single(x => x.AssociationId == c.Id);
        Assert.Equal(0m, cDue.CurrentRequired);
        Assert.Equal(0m, cDue.CurrentRemaining);

        // Payment of 3,000 allocates only to A (1,000) and B (2,000)
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 3_000m), CancellationToken.None);
        Assert.DoesNotContain(plan.Allocations, x => x.AssociationId == c.Id);
        Assert.Equal(1_000m, plan.Allocations.Single(x => x.AssociationId == a.Id).Amount);
        Assert.Equal(2_000m, plan.Allocations.Single(x => x.AssociationId == b.Id).Amount);
    }

    [Fact]
    public async Task Scenario12_DifferentShareAmountsAcrossAssociations()
    {
        // Scenario 12: A: monthly 1,000 with 1 share = 1,000. B: monthly 1,000 with 2.5 shares = 2,500. Total = 3,500.
        // Partial payment of 1,400: A gets 400, B gets 1,000.
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, a, b, _) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3,
            amountA: 1_000m, amountB: 1_000m, amountC: 1_000m,
            sharesA: 1m, sharesB: 2.5m, sharesC: 1m);

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 1_400m, AssociationIds: [a.Id, b.Id]), CancellationToken.None);

        Assert.Equal(1_400m, plan.Allocations.Sum(x => x.Amount));
        Assert.Equal(400m, plan.Allocations.Single(x => x.AssociationId == a.Id).Amount);
        Assert.Equal(1_000m, plan.Allocations.Single(x => x.AssociationId == b.Id).Amount);
    }

    [Fact]
    public async Task Scenario13_PersonNotObligatedForParticularMonth()
    {
        // Scenario 13: Association A started in prevMonth. Association B started in currentMonth.
        // Person pays for prevMonth: only Association A has an obligation and receives allocation.
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var prevMonth = currentMonth.AddMonths(-1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();

        var person = new Person("Subscriber", "+201099998888", SubscriberCode.Create("NOTO1234"), DateTimeOffset.UtcNow.AddMonths(-3));
        db.PeopleSet.Add(person);

        var assocA = new Association("Starts Early", prevMonth, 2, 1_000m, user.UserId!, DateTimeOffset.UtcNow.AddMonths(-3));
        assocA.SetPeriods([(prevMonth, new DateOnly(prevMonth.Year, prevMonth.Month, 10)), (currentMonth, new DateOnly(currentMonth.Year, currentMonth.Month, 10))]);
        var memA = assocA.AddMember(person.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-3));
        assocA.AddPayout(assocA.Periods.First().Id, PayoutRecipientType.Membership, memA.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-3));
        assocA.AddPayout(assocA.Periods.Last().Id, PayoutRecipientType.ReserveSlot, null, 1m, DateTimeOffset.UtcNow.AddMonths(-3));
        assocA.Start(DateTimeOffset.UtcNow.AddMonths(-2));

        var assocB = new Association("Starts Current", currentMonth, 2, 2_000m, user.UserId!, DateTimeOffset.UtcNow.AddMonths(-2));
        assocB.SetPeriods([(currentMonth, new DateOnly(currentMonth.Year, currentMonth.Month, 10)), (currentMonth.AddMonths(1), new DateOnly(currentMonth.AddMonths(1).Year, currentMonth.AddMonths(1).Month, 10))]);
        var memB = assocB.AddMember(person.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-2));
        assocB.AddPayout(assocB.Periods.First().Id, PayoutRecipientType.Membership, memB.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
        assocB.AddPayout(assocB.Periods.Last().Id, PayoutRecipientType.ReserveSlot, null, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
        assocB.Start(DateTimeOffset.UtcNow.AddMonths(-1));

        db.AssociationsSet.AddRange(assocA, assocB);
        await db.SaveChangesAsync();

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 1_000m), CancellationToken.None);

        // Should settle AssocA prevMonth obligation only
        var alloc = Assert.Single(plan.Allocations);
        Assert.Equal(assocA.Id, alloc.AssociationId);
        Assert.Equal(assocA.Periods.First().Id, alloc.PeriodId);
        Assert.Equal(1_000m, alloc.Amount);
    }

    [Fact]
    public void Scenario14_PaymentRecordedInLaterMonthForOlderObligation()
    {
        // Scenario 14: Payment entered in November 2026 for October 2026 obligation retains actual transaction date
        // and records correct DueDate and LateDays on the allocation.
        var paidAt = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var dueDate = new DateOnly(2026, 10, 10);
        var actualDate = DateOnly.FromDateTime(paidAt.UtcDateTime);

        var payment = new Payment(Guid.NewGuid(), "payment_user", 1_000m, PaymentMethod.InstaPay, paidAt, paidAt);
        payment.Allocate(Guid.NewGuid(), Guid.NewGuid(), 1_000m, dueDate, actualDate);

        var allocation = Assert.Single(payment.Allocations);
        Assert.Equal(paidAt, payment.PaidAtUtc);
        Assert.Equal(dueDate, allocation.DueDate);
        Assert.True(allocation.LateDays > 0, "Late days should be positive since payment was in November for October due date.");
    }

    [Fact]
    public async Task Scenario16_DecimalRoundingAndCurrencyReconciliation()
    {
        // Scenario 16: 100.00 EGP split among 3 equal shares: 33.33, 33.33, 33.34: reconciles exactly to 100.00.
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, _, _, _) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3,
            amountA: 100m, amountB: 100m, amountC: 100m);

        var service = CreateService(db, user, clock);
        var plan = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 100.00m), CancellationToken.None);

        Assert.Equal(3, plan.Allocations.Count);
        Assert.Equal(100.00m, plan.Allocations.Sum(x => x.Amount));
    }

    [Fact]
    public async Task Scenario17_SavedTransactionAllocationsMatchThePreview()
    {
        // Scenario 17: Verify preview plan and persisted allocations match penny-for-penny
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, _, _, _) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);
        var req = new PaymentPlanningRequest(person.Id, 3_750m);

        var preview = await service.BuildPlanAsync(req, CancellationToken.None);

        // Save payment using preview plan
        var payment = new Payment(person.Id, user.UserId!, 3_750m, PaymentMethod.VodafoneCash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in preview.Allocations)
            payment.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(payment);
        await db.SaveChangesAsync();

        // Load saved payment from db and compare
        var saved = await db.PaymentsSet.Include(x => x.Allocations).SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(preview.Allocations.Count, saved.Allocations.Count);
        foreach (var pa in preview.Allocations)
        {
            var matched = saved.Allocations.Single(x => x.AssociationId == pa.AssociationId && x.PeriodId == pa.PeriodId);
            Assert.Equal(pa.Amount, matched.AllocatedAmount);
            Assert.Equal(pa.DueDate, matched.DueDate);
        }
    }

    [Fact]
    public async Task Scenario18_Invariants_SumOfAllocationsEqualsAmountAndOutstandingReconciles()
    {
        // Scenario 18:
        // Sum of persisted payment allocations = Amount actually allocated
        // Outstanding obligation = Original obligation - valid allocations
        await using var db = CreateDbContext();
        var currentMonth = new DateOnly(2026, 10, 1);
        var clock = new TestClock(new DateOnly(2026, 10, 15));
        var user = new TestCurrentUser();
        var (person, _, _, _) = SetupThreeAssociations(db, currentMonth, user.UserId!, months: 3);

        var service = CreateService(db, user, clock);

        // Step 1: Pay 2,000
        var plan1 = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 2_000m), CancellationToken.None);
        var pay1 = new Payment(person.Id, user.UserId!, 2_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in plan1.Allocations)
            pay1.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(pay1);
        await db.SaveChangesAsync();

        Assert.Equal(2_000m, pay1.Allocations.Sum(x => x.AllocatedAmount));

        var page1 = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(3_000m, page1.CurrentDue);
        Assert.Equal(5_000m - 2_000m, page1.AmountDueNow);

        // Step 2: Pay 3,000 to fully settle
        var plan2 = await service.BuildPlanAsync(new PaymentPlanningRequest(person.Id, 3_000m), CancellationToken.None);
        var pay2 = new Payment(person.Id, user.UserId!, 3_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        foreach (var al in plan2.Allocations)
            pay2.Allocate(al.AssociationId, al.PeriodId, al.Amount, al.DueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.PaymentsSet.Add(pay2);
        await db.SaveChangesAsync();

        Assert.Equal(3_000m, pay2.Allocations.Sum(x => x.AllocatedAmount));

        var page2 = await service.GetPageAsync(person.Id, CancellationToken.None);
        Assert.Equal(0m, page2.CurrentDue);
        Assert.Equal(0m, page2.AmountDueNow);
        Assert.All(page2.Associations, assoc => Assert.Equal(0m, assoc.CurrentRemaining));
    }
}
