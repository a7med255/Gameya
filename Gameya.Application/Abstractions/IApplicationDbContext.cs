using Gameya.Domain.Entities;

namespace Gameya.Application.Abstractions;

public interface IApplicationDbContext
{
    IQueryable<Person> People { get; }
    IQueryable<Association> Associations { get; }
    IQueryable<AssociationMembership> Memberships { get; }
    IQueryable<AssociationPeriod> Periods { get; }
    IQueryable<PayoutAssignment> Payouts { get; }
    IQueryable<Payment> Payments { get; }
    IQueryable<PaymentAllocation> PaymentAllocations { get; }
    IQueryable<AssociationCredit> AssociationCredits { get; }
    IQueryable<AssociationCreditApplication> AssociationCreditApplications { get; }
    IQueryable<Replacement> Replacements { get; }
    IQueryable<Settlement> Settlements { get; }
    IQueryable<AuditLog> AuditLogs { get; }

    Task<Association?> FindAssociationForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken) where T : class;
    ValueTask AddEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class;
    void RemoveEntities<T>(IEnumerable<T> entities) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
