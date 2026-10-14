using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Domain.Entities;
using Gameya.Infrastructure.Authentication;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gameya.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo EgyptTimeZone = ResolveEgyptTimeZone();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, EgyptTimeZone).DateTime);
    private static TimeZoneInfo ResolveEgyptTimeZone()
    {
        foreach (var id in new[] { "Egypt Standard Time", "Africa/Cairo" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        throw new InvalidOperationException("The Egypt time zone is unavailable on this host.");
    }
}

public sealed class SubscriberCodeGenerator : ISubscriberCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public string Generate() => string.Create(8, 0, static (span, _) =>
    {
        Span<byte> bytes = stackalloc byte[span.Length];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[bytes[i] % Alphabet.Length];
    });
}

public sealed class AssociationCalendar(IOptions<AssociationCalendarOptions> options) : IAssociationCalendar
{
    public IReadOnlyCollection<(DateOnly PeriodDate, DateOnly DueDate)> CreatePeriods(DateOnly startDate, int numberOfMonths)
    {
        var dueDay = Math.Clamp(options.Value.DueDay, 1, 28);
        return Enumerable.Range(0, numberOfMonths).Select(index =>
        {
            var periodDate = startDate.AddMonths(index);
            var dueDate = new DateOnly(periodDate.Year, periodDate.Month, dueDay);
            return (periodDate, dueDate);
        }).ToArray();
    }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    public Guid? PersonId => Guid.TryParse(User.FindFirstValue("person_id"), out var id) ? id : null;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public bool IsInRole(string role) => User.IsInRole(role);
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public sealed class AssociationAccessService(ApplicationDbContext db, ICurrentUser current) : IAssociationAccessService
{
    public IQueryable<Guid> GetAccessibleAssociationIds(string userId, bool isAdmin, bool isOwner)
    {
        if (isAdmin) return db.AssociationsSet.Select(x => x.Id);
        if (isOwner) return db.AssociationsSet.Where(x => x.OwnerId == userId).Select(x => x.Id);
        return db.UserAssociationAccesses.Where(x => x.UserId == userId).Select(x => x.AssociationId);
    }

    public async Task EnsureAccessAsync(Guid associationId, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var ids = GetAccessibleAssociationIds(userId, current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        if (!await ids.AnyAsync(x => x == associationId, cancellationToken))
            throw new NotFoundException("association.not_found", "Association was not found.");
    }
}

public sealed class PersonAccessService(ApplicationDbContext db, ICurrentUser current, IClock clock) : IPersonAccessService
{
    public IQueryable<Guid> GetAccessiblePersonIds(string userId, bool isAdmin, bool isOwner)
    {
        if (isAdmin) return db.PeopleSet.Select(x => x.Id);
        if (isOwner)
        {
            var ownedAssociationIds = db.AssociationsSet.Where(x => x.OwnerId == userId).Select(x => x.Id);
            var memberIds = db.MembershipsSet.Where(x => ownedAssociationIds.Contains(x.AssociationId)).Select(x => x.PersonId);
            var directlyManagedIds = db.OwnerPersonAccesses.Where(x => x.OwnerId == userId).Select(x => x.PersonId);
            return memberIds.Concat(directlyManagedIds).Distinct();
        }
        var assignedAssociationIds = db.UserAssociationAccesses.Where(x => x.UserId == userId).Select(x => x.AssociationId);
        return db.MembershipsSet.Where(x => assignedAssociationIds.Contains(x.AssociationId)).Select(x => x.PersonId).Distinct();
    }

    public async Task EnsureAccessAsync(Guid personId, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new NotFoundException("person.not_found", "Person was not found.");
        var ids = GetAccessiblePersonIds(userId, current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        if (!await ids.AnyAsync(x => x == personId, cancellationToken))
            throw new NotFoundException("person.not_found", "Person was not found.");
    }

    public async Task GrantOwnerAccessAsync(string ownerId, Guid personId, string normalizedName,
        string normalizedPhone, CancellationToken cancellationToken)
    {
        if (await db.OwnerPersonAccesses.AnyAsync(x => x.OwnerId == ownerId && x.PersonId == personId, cancellationToken)) return;
        await db.OwnerPersonAccesses.AddAsync(new OwnerPersonAccess
        {
            OwnerId = ownerId, PersonId = personId, NormalizedName = normalizedName,
            NormalizedPhone = normalizedPhone, GrantedAtUtc = clock.UtcNow
        }, cancellationToken);
    }
}

public sealed class AuditService(ApplicationDbContext db, ICurrentUser current, IClock clock) : IAuditService
{
    public async Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues,
        CancellationToken cancellationToken)
    {
        static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value);
        await db.AuditLogsSet.AddAsync(new AuditLog(current.UserId, action, entityName, entityId,
            Serialize(oldValues), Serialize(newValues), clock.UtcNow, current.IpAddress), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
