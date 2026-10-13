using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Enums;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gameya.Infrastructure.Reports;

public sealed class MonthlyReportGenerator(ApplicationDbContext db) : IReportGenerator
{
    public async Task<GeneratedReportDto> GenerateMonthlyAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) throw new ConflictException("report.invalid_period", "Invalid report period.");
        var associations = await db.AssociationsSet.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .Select(x => new { x.Id, x.Name, x.Status, x.MonthlyContribution }).ToListAsync(cancellationToken);
        var ids = associations.Select(x => x.Id).ToArray();
        var paid = await db.PaymentAllocationsSet.AsNoTracking().Where(x => ids.Contains(x.AssociationId))
            .Join(db.PaymentsSet, a => a.PaymentId, p => p.Id, (a, p) => new { a, p })
            .Where(x => x.p.PaidAtUtc.Year == year && x.p.PaidAtUtc.Month == month)
            .GroupBy(x => x.a.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(v => v.a.AllocatedAmount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var content = Document.Create(document => document.Page(page =>
        {
            page.Margin(36); page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"Gameya monthly report - {year:D4}-{month:D2}").FontSize(18).SemiBold();
            page.Content().PaddingVertical(20).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                table.Header(h =>
                {
                    h.Cell().Text("Association").SemiBold(); h.Cell().Text("Status").SemiBold();
                    h.Cell().AlignRight().Text("Monthly").SemiBold(); h.Cell().AlignRight().Text("Paid").SemiBold();
                });
                foreach (var item in associations)
                {
                    table.Cell().Text(item.Name); table.Cell().Text(item.Status.ToString());
                    table.Cell().AlignRight().Text(item.MonthlyContribution.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().AlignRight().Text(paid.GetValueOrDefault(item.Id).ToString("N2", CultureInfo.InvariantCulture));
                }
            });
            page.Footer().AlignCenter().Text("Report layout is versioned and intentionally extensible pending the approved reference format.").FontSize(8).FontColor(Colors.Grey.Medium);
        })).GeneratePdf();
        return new GeneratedReportDto(content, "application/pdf", $"gameya-report-{year:D4}-{month:D2}.pdf");
    }
}

public sealed class ReportDeliveryService(IReportGenerator reports, IEmailService email,
    UserManager<ApplicationUser> users) : IReportDeliveryService
{
    public async Task SendMonthlyAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        var owner = await users.FindByIdAsync(ownerId) ?? throw new NotFoundException("user.not_found", "User was not found.");
        var report = await reports.GenerateMonthlyAsync(ownerId, year, month, cancellationToken);
        await email.SendAsync(owner.Email!, $"Gameya report {year:D4}-{month:D2}", "<p>Your monthly Gameya report is attached.</p>",
            report.Content, report.FileName, cancellationToken);
    }
}
