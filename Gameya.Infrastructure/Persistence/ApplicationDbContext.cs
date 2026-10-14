using System.Data;
using Gameya.Application.Abstractions;
using Gameya.Domain.Common;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gameya.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
    ILogger<ApplicationDbContext> logger)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<Person> PeopleSet => Set<Person>();
    public DbSet<Association> AssociationsSet => Set<Association>();
    public DbSet<AssociationMembership> MembershipsSet => Set<AssociationMembership>();
    public DbSet<AssociationPeriod> PeriodsSet => Set<AssociationPeriod>();
    public DbSet<PayoutAssignment> PayoutsSet => Set<PayoutAssignment>();
    public DbSet<Payment> PaymentsSet => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocationsSet => Set<PaymentAllocation>();
    public DbSet<AssociationCredit> AssociationCreditsSet => Set<AssociationCredit>();
    public DbSet<AssociationCreditApplication> AssociationCreditApplicationsSet => Set<AssociationCreditApplication>();
    public DbSet<Replacement> ReplacementsSet => Set<Replacement>();
    public DbSet<ReplacementShare> ReplacementSharesSet => Set<ReplacementShare>();
    public DbSet<Settlement> SettlementsSet => Set<Settlement>();
    public DbSet<AuditLog> AuditLogsSet => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PaymentUserInvitation> PaymentUserInvitations => Set<PaymentUserInvitation>();
    public DbSet<InvitationAssociation> InvitationAssociations => Set<InvitationAssociation>();
    public DbSet<UserAssociationAccess> UserAssociationAccesses => Set<UserAssociationAccess>();
    public DbSet<OwnerPersonAccess> OwnerPersonAccesses => Set<OwnerPersonAccess>();
    public DbSet<BackgroundJobExecution> BackgroundJobExecutions => Set<BackgroundJobExecution>();

    IQueryable<Person> IApplicationDbContext.People => PeopleSet;
    IQueryable<Association> IApplicationDbContext.Associations => AssociationsSet;
    IQueryable<AssociationMembership> IApplicationDbContext.Memberships => MembershipsSet;
    IQueryable<AssociationPeriod> IApplicationDbContext.Periods => PeriodsSet;
    IQueryable<PayoutAssignment> IApplicationDbContext.Payouts => PayoutsSet;
    IQueryable<Payment> IApplicationDbContext.Payments => PaymentsSet;
    IQueryable<PaymentAllocation> IApplicationDbContext.PaymentAllocations => PaymentAllocationsSet;
    IQueryable<AssociationCredit> IApplicationDbContext.AssociationCredits => AssociationCreditsSet;
    IQueryable<AssociationCreditApplication> IApplicationDbContext.AssociationCreditApplications => AssociationCreditApplicationsSet;
    IQueryable<Replacement> IApplicationDbContext.Replacements => ReplacementsSet;
    IQueryable<Settlement> IApplicationDbContext.Settlements => SettlementsSet;
    IQueryable<AuditLog> IApplicationDbContext.AuditLogs => AuditLogsSet;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public async Task<Association?> FindAssociationForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await AssociationsSet.Include(x => x.Memberships).Include(x => x.Periods).Include(x => x.Payouts)
            .Include(x => x.Replacements).ThenInclude(x => x.Shares).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken) where T : class =>
        await Set<T>().FindAsync([id], cancellationToken);

    public async ValueTask AddEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class =>
        await Set<T>().AddAsync(entity, cancellationToken);

    public void RemoveEntities<T>(IEnumerable<T> entities) where T : class => Set<T>().RemoveRange(entities);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceImmutableHistory();
        var aggregateRoots = ChangeTracker.Entries<AggregateRoot>().Select(x => x.Entity).Where(x => x.DomainEvents.Count != 0).ToList();
        int result;
        try
        {
            result = await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Persistence concurrency conflict for entries {Entries}",
                string.Join(", ", exception.Entries.Select(entry =>
                    $"{entry.Metadata.ClrType.Name}:{entry.State}")));
            throw;
        }
        foreach (var aggregate in aggregateRoots)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                logger.LogInformation("Domain event {EventName} occurred at {OccurredAt}", domainEvent.GetType().Name, domainEvent.OccurredAtUtc);
            aggregate.ClearDomainEvents();
        }
        return result;
    }

    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!Database.IsRelational()) return await operation(cancellationToken);
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private void EnforceImmutableHistory()
    {
        var immutableTypes = new[] { typeof(Payment), typeof(PaymentAllocation), typeof(AssociationCredit), typeof(AssociationCreditApplication),
            typeof(Settlement), typeof(Replacement), typeof(ReplacementShare), typeof(AuditLog) };
        if (ChangeTracker.Entries().Any(x => immutableTypes.Contains(x.Entity.GetType()) && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Historical financial records are immutable; use a compensating transaction.");
        if (ChangeTracker.Entries<PayoutAssignment>().Any(x =>
                x.State == EntityState.Deleted && x.OriginalValues.GetValue<PayoutStatus>(nameof(PayoutAssignment.Status)) == PayoutStatus.Completed ||
                x.State == EntityState.Modified && x.OriginalValues.GetValue<PayoutStatus>(nameof(PayoutAssignment.Status)) == PayoutStatus.Completed))
            throw new InvalidOperationException("Completed payout history is immutable.");
    }
}
