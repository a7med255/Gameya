using Gameya.Domain.Common;
using Gameya.Domain.Enums;

namespace Gameya.Domain.Entities;

public sealed class AssociationPeriod : Entity
{
    private AssociationPeriod() { }

    internal AssociationPeriod(Guid associationId, int periodNumber, DateOnly periodDate, DateOnly dueDate)
    {
        AssociationId = associationId;
        PeriodNumber = periodNumber;
        PeriodDate = periodDate;
        DueDate = dueDate;
        Status = PeriodStatus.Upcoming;
    }

    public Guid AssociationId { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly PeriodDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public PeriodStatus Status { get; private set; }
    internal void Close() => Status = PeriodStatus.Closed;
}
