using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Enums;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Email;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gameya.Infrastructure.Reports;

public sealed class MonthlyReportGenerator(ApplicationDbContext db, IClock clock) : IReportGenerator
{
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-EG");

    public async Task<GeneratedReportDto> GenerateMonthlyAsync(string userId, bool includeAllOwners, int year, int month,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) throw new ConflictException("report.invalid_period", "Invalid report period.");
        var associations = await db.AssociationsSet.AsNoTracking()
            .Where(x => x.Status != AssociationStatus.Cancelled && (includeAllOwners || x.OwnerId == userId))
            .Select(x => new
            {
                x.Id, x.Name, x.Status, x.StartDate, x.EndDate, x.NumberOfMonths, x.MonthlyContribution,
                MemberCount = x.Memberships.Count(m => m.Kind == MembershipKind.Paying)
            }).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var ids = associations.Select(x => x.Id).ToArray();
        var periodIds = await db.PeriodsSet.AsNoTracking()
            .Where(x => ids.Contains(x.AssociationId) && x.PeriodDate.Year == year && x.PeriodDate.Month == month)
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        var due = await db.MembershipsSet.AsNoTracking()
            .Where(x => ids.Contains(x.AssociationId) && x.Kind == MembershipKind.Paying)
            .Join(db.AssociationsSet.AsNoTracking(), membership => membership.AssociationId, association => association.Id,
                (membership, association) => new { membership, association })
            .Join(db.PeriodsSet.AsNoTracking().Where(x => periodIds.Contains(x.Id)),
                row => row.association.Id, period => period.AssociationId,
                (row, period) => new { row.membership, row.association, period })
            .Where(x => x.membership.JoinedAtUtc < new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1)
                && (!x.membership.LeftAtUtc.HasValue
                    || x.membership.LeftAtUtc >= new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero)))
            .GroupBy(x => x.association.Id)
            .Select(x => new { Id = x.Key, Amount = x.Sum(v => v.membership.ShareCount * v.association.MonthlyContribution) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var paid = await db.PaymentAllocationsSet.AsNoTracking().Where(x => ids.Contains(x.AssociationId))
            .Join(db.PaymentsSet.AsNoTracking(), a => a.PaymentId, p => p.Id, (a, p) => new { a, p })
            .Where(x => x.p.PaidAtUtc.Year == year && x.p.PaidAtUtc.Month == month)
            .GroupBy(x => x.a.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(v => v.a.AllocatedAmount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var totalDue = associations.Sum(x => due.GetValueOrDefault(x.Id));
        var totalPaid = associations.Sum(x => paid.GetValueOrDefault(x.Id));
        var periodLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", ArabicCulture);
        var content = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(32);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontFamily("Arial", "Noto Sans Arabic", "DejaVu Sans")
                .FontSize(10).FontColor(Colors.Grey.Darken3));
            page.Header().Column(header =>
            {
                header.Item().Text("تقرير الجمعية الشهري").FontSize(22).Bold().FontColor("#176B5B");
                header.Item().PaddingTop(4).Text($"الفترة: {periodLabel}").FontSize(12);
                header.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Background("#EAF4F1").Padding(10).Text($"المستحق: {Money(totalDue)}").Bold();
                    row.RelativeItem().PaddingHorizontal(8).Background("#EAF4F1").Padding(10).Text($"المدفوع: {Money(totalPaid)}").Bold();
                    row.RelativeItem().Background("#EAF4F1").Padding(10)
                        .Text($"المتبقي: {Money(Math.Max(0, totalDue - totalPaid))}").Bold();
                });
            });
            page.Content().PaddingVertical(18).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.4f); c.RelativeColumn(1.1f); c.RelativeColumn(); c.RelativeColumn();
                    c.RelativeColumn(); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                });
                table.Header(h =>
                {
                    HeaderCell(h.Cell()).Text("الجمعية"); HeaderCell(h.Cell()).Text("الحالة");
                    HeaderCell(h.Cell()).Text("البداية"); HeaderCell(h.Cell()).Text("النهاية");
                    HeaderCell(h.Cell()).Text("الأعضاء"); HeaderCell(h.Cell()).Text("المستحق");
                    HeaderCell(h.Cell()).Text("المدفوع"); HeaderCell(h.Cell()).Text("المتبقي");
                });
                foreach (var item in associations)
                {
                    var associationDue = due.GetValueOrDefault(item.Id);
                    var associationPaid = paid.GetValueOrDefault(item.Id);
                    BodyCell(table.Cell()).Text(item.Name).Bold();
                    BodyCell(table.Cell()).Text(StatusLabel(item.Status));
                    BodyCell(table.Cell()).Text(Date(item.StartDate));
                    BodyCell(table.Cell()).Text(item.EndDate.HasValue ? Date(item.EndDate.Value) : "-");
                    BodyCell(table.Cell()).Text(item.MemberCount.ToString(ArabicCulture));
                    BodyCell(table.Cell()).Text(Money(associationDue));
                    BodyCell(table.Cell()).Text(Money(associationPaid));
                    BodyCell(table.Cell()).Text(Money(Math.Max(0, associationDue - associationPaid)));
                }
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("نظام الجمعية - تقرير مالي محفوظ للسجل").FontSize(8).FontColor(Colors.Grey.Medium);
                row.ConstantItem(90).AlignLeft()
                    .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).Text(text =>
                {
                    text.Span("صفحة "); text.CurrentPageNumber(); text.Span(" من "); text.TotalPages();
                });
            });
        })).GeneratePdf();
        return new GeneratedReportDto(content, "application/pdf", $"تقرير-الجمعية-{year:D4}-{month:D2}.pdf");
    }

    public async Task<GeneratedReportDto> GenerateAssociationAsync(string userId, bool includeAllOwners,
        Guid associationId, CancellationToken cancellationToken)
    {
        var association = await db.AssociationsSet.AsNoTracking()
            .Where(x => x.Id == associationId && (includeAllOwners || x.OwnerId == userId))
            .Select(x => new
            {
                x.Id, x.Name, x.StartDate, x.EndDate, x.NumberOfMonths, x.MonthlyContribution, x.Status
            }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");

        var periods = await db.PeriodsSet.AsNoTracking().Where(x => x.AssociationId == association.Id)
            .OrderBy(x => x.PeriodNumber).Select(x => new
            {
                x.Id, x.PeriodNumber, x.PeriodDate, x.DueDate
            }).ToListAsync(cancellationToken);
        var memberships = await db.MembershipsSet.AsNoTracking().Where(x => x.AssociationId == association.Id)
            .Join(db.PeopleSet.AsNoTracking(), membership => membership.PersonId, person => person.Id,
                (membership, person) => new
                {
                    membership.Id, membership.PersonId, person.FullName, membership.ShareCount, membership.Kind,
                    membership.JoinedAtUtc, membership.LeftAtUtc
                }).ToListAsync(cancellationToken);
        var payouts = await db.PayoutsSet.AsNoTracking().Where(x => x.AssociationId == association.Id)
            .OrderBy(x => x.PeriodId).ThenBy(x => x.CreatedAtUtc).Select(x => new
            {
                x.PeriodId, x.RecipientType, x.MembershipId, x.BeneficiaryPersonId, x.ShareCount
            }).ToListAsync(cancellationToken);
        var beneficiaryIds = payouts.Where(x => x.BeneficiaryPersonId.HasValue)
            .Select(x => x.BeneficiaryPersonId!.Value).Distinct().ToArray();
        var beneficiaryNames = await db.PeopleSet.AsNoTracking().Where(x => beneficiaryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var paidByPeriod = await db.PaymentAllocationsSet.AsNoTracking()
            .Where(x => x.AssociationId == association.Id)
            .GroupBy(x => x.PeriodId).Select(x => new { PeriodId = x.Key, Amount = x.Sum(y => y.AllocatedAmount) })
            .ToDictionaryAsync(x => x.PeriodId, x => x.Amount, cancellationToken);
        var creditsByPeriod = await db.AssociationCreditApplicationsSet.AsNoTracking()
            .Where(x => x.AssociationId == association.Id)
            .GroupBy(x => x.PeriodId).Select(x => new { PeriodId = x.Key, Amount = x.Sum(y => y.Amount) })
            .ToDictionaryAsync(x => x.PeriodId, x => x.Amount, cancellationToken);

        var rows = new List<AssociationReportRow>();
        foreach (var period in periods)
        {
            var eligibleShares = memberships.Where(x => x.Kind == MembershipKind.Paying
                    && period.PeriodDate >= clock.EgyptDate(x.JoinedAtUtc)
                    && (!x.LeftAtUtc.HasValue || period.PeriodDate <= clock.EgyptDate(x.LeftAtUtc.Value)))
                .Sum(x => x.ShareCount);
            var due = eligibleShares * association.MonthlyContribution;
            var paid = paidByPeriod.GetValueOrDefault(period.Id) + creditsByPeriod.GetValueOrDefault(period.Id);
            var periodPayouts = payouts.Where(x => x.PeriodId == period.Id).ToArray();
            if (periodPayouts.Length == 0)
            {
                rows.Add(new(period.PeriodNumber, "—", period.PeriodDate, association.MonthlyContribution,
                    0, 0, paid, Math.Max(0, due - paid), true));
                continue;
            }
            for (var index = 0; index < periodPayouts.Length; index++)
            {
                var payout = periodPayouts[index];
                var recipient = payout.RecipientType switch
                {
                    PayoutRecipientType.ReserveSlot => payout.BeneficiaryPersonId.HasValue
                        ? beneficiaryNames.GetValueOrDefault(payout.BeneficiaryPersonId.Value, "بدون اسم")
                        : "بدون اسم",
                    PayoutRecipientType.AdvanceSlot => "سلف",
                    _ => memberships.FirstOrDefault(x => x.Id == payout.MembershipId)?.FullName ?? "—"
                };
                rows.Add(new(period.PeriodNumber, recipient, period.PeriodDate, association.MonthlyContribution,
                    payout.ShareCount, payout.ShareCount * association.MonthlyContribution * association.NumberOfMonths,
                    index == 0 ? paid : null, index == 0 ? Math.Max(0, due - paid) : null, index == 0));
            }
        }

        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(22);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontFamily("Arial", "Noto Sans Arabic", "DejaVu Sans")
                .FontSize(9).FontColor(Colors.Black));
            page.Header().Column(header =>
            {
                header.Item().Border(1).BorderColor(Colors.Black).Padding(8).AlignCenter()
                    .Text($"جمعية رقم / {association.Name}").FontSize(18).Bold();
                header.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"ابتداءً من {Date(association.StartDate)}").Bold();
                    row.RelativeItem().AlignCenter().Text($"إلى {Date(association.EndDate ?? association.StartDate.AddMonths(association.NumberOfMonths - 1))}").Bold();
                    row.RelativeItem().AlignLeft().Text($"عدد الفترات: {association.NumberOfMonths}").Bold();
                });
            });
            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(38); columns.RelativeColumn(2.2f); columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.15f); columns.RelativeColumn(.8f); columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.15f); columns.RelativeColumn(1.15f);
                });
                table.Header(header =>
                {
                    PaperHeader(header.Cell()).Text("م"); PaperHeader(header.Cell()).Text("الاسم");
                    PaperHeader(header.Cell()).Text("التاريخ"); PaperHeader(header.Cell()).Text("المبلغ الشهري");
                    PaperHeader(header.Cell()).Text("عدد الأسهم"); PaperHeader(header.Cell()).Text("قيمة القبض");
                    PaperHeader(header.Cell()).Text("الدفع"); PaperHeader(header.Cell()).Text("المتبقي");
                });
                foreach (var row in rows)
                {
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.FirstInPeriod ? row.PeriodNumber.ToString(ArabicCulture) : "");
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.Recipient).Bold();
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.FirstInPeriod ? Date(row.Date) : "");
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(Money(row.MonthlyAmount));
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.Shares.ToString("0.##", ArabicCulture));
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(Money(row.PayoutValue));
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.Paid.HasValue ? Money(row.Paid.Value) : "");
                    PaperCell(table.Cell(), row.FirstInPeriod).Text(row.Remaining.HasValue ? Money(row.Remaining.Value) : "");
                }
            });
            page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(8)).Text(text =>
            {
                text.Span("صفحة "); text.CurrentPageNumber(); text.Span(" من "); text.TotalPages();
            });
        })).GeneratePdf();
        var safeName = string.Concat(association.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return new GeneratedReportDto(pdf, "application/pdf", $"تقرير-جمعية-{safeName}.pdf");
    }

    private static IContainer HeaderCell(IContainer container) => container.Background("#176B5B")
        .PaddingVertical(8).PaddingHorizontal(6).AlignMiddle().AlignCenter()
        .DefaultTextStyle(x => x.FontColor(Colors.White).Bold());
    private static IContainer BodyCell(IContainer container) => container.BorderBottom(1)
        .BorderColor("#DCE8E5").PaddingVertical(8).PaddingHorizontal(6).AlignMiddle();
    private static string Money(decimal amount) => $"{amount.ToString("N2", ArabicCulture)} جنيه";
    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", ArabicCulture);
    private static string StatusLabel(AssociationStatus status) => status switch
    {
        AssociationStatus.Draft => "مسودة",
        AssociationStatus.Active => "نشطة",
        AssociationStatus.Paused => "متوقفة مؤقتًا",
        AssociationStatus.Completed => "مكتملة",
        AssociationStatus.Cancelled => "ملغاة",
        _ => "غير محددة"
    };

    private static IContainer PaperHeader(IContainer container) => container.Border(1)
        .BorderColor(Colors.Black).Background(Colors.Grey.Lighten2).Padding(6).AlignCenter().AlignMiddle()
        .DefaultTextStyle(x => x.Bold());
    private static IContainer PaperCell(IContainer container, bool emphasized) => container.Border(1)
        .BorderColor(Colors.Black).Background(emphasized ? Colors.White : Colors.Grey.Lighten4)
        .PaddingVertical(6).PaddingHorizontal(4).AlignCenter().AlignMiddle();

    private sealed record AssociationReportRow(int PeriodNumber, string Recipient, DateOnly Date,
        decimal MonthlyAmount, decimal Shares, decimal PayoutValue, decimal? Paid, decimal? Remaining,
        bool FirstInPeriod);
}

public sealed class ReportDeliveryService(IReportGenerator reports, IEmailService email,
    UserManager<ApplicationUser> users, ApplicationDbContext db) : IReportDeliveryService
{
    public async Task SendMonthlyAsync(string userId, bool includeAllOwners, int year, int month,
        CancellationToken cancellationToken)
    {
        var recipient = await users.FindByIdAsync(userId) ?? throw new NotFoundException("user.not_found", "User was not found.");
        var report = await reports.GenerateMonthlyAsync(userId, includeAllOwners, year, month, cancellationToken);
        await email.SendAsync(recipient.Email!, $"كشف تحصيل جمعية — {new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("ar-EG"))}",
            ArabicEmailTemplate.Report(year, month),
            report.Content, report.FileName, cancellationToken);
    }

    public async Task SendAssociationAsync(string userId, bool includeAllOwners, Guid associationId,
        CancellationToken cancellationToken)
    {
        var recipient = await users.FindByIdAsync(userId)
            ?? throw new NotFoundException("user.not_found", "User was not found.");
        var report = await reports.GenerateAssociationAsync(userId, includeAllOwners, associationId, cancellationToken);
        var associationName = await db.AssociationsSet.AsNoTracking()
            .Where(x => x.Id == associationId && (includeAllOwners || x.OwnerId == userId))
            .Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        await email.SendAsync(recipient.Email!, $"تقرير جمعية - {associationName}",
            ArabicEmailTemplate.AssociationReport(associationName),
            report.Content, report.FileName, cancellationToken);
    }
}
