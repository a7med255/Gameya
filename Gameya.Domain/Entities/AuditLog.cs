using Gameya.Domain.Common;

namespace Gameya.Domain.Entities;

public sealed class AuditLog : Entity
{
    private AuditLog() { }
    public AuditLog(string? userId, string action, string entityName, string entityId,
        string? oldValues, string? newValues, DateTimeOffset timestampUtc, string? ipAddress)
    {
        UserId = userId;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        TimestampUtc = timestampUtc;
        IpAddress = ipAddress;
    }
    public string? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? IpAddress { get; private set; }
}
