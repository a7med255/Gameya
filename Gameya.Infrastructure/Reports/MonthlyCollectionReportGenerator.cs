using System.Globalization;
using Gameya.Application.Abstractions;
using Gameya.Application.DTOs;
using Gameya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gameya.Infrastructure.Reports;

/// <summary>Printable monthly collection sheet. All monetary values come from IMonthlyFinanceService.</summary>
public sealed class MonthlyCollectionReportGenerator(
    IMonthlyFinanceService finance,
    ApplicationDbContext db,
    MonthlyReportGenerator associationReports) : IReportGenerator
{
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-EG");

    public async Task<GeneratedReportDto> GenerateMonthlyAsync(string userId, bool includeAllOwners, int year,
        int month, CancellationToken cancellationToken)
    {
        var summary = await finance.GetAsync(userId, includeAllOwners, year, month, cancellationToken);
        var ownerName = includeAllOwners
            ? "كل الملاك"
            : await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DisplayName)
                .SingleOrDefaultAsync(cancellationToken) ?? "مالك الجمعية";
        var pdf = GeneratePdf(summary, ownerName);
        return new GeneratedReportDto(pdf, "application/pdf", $"كشف-التحصيل-{year:D4}-{month:D2}.pdf");
    }

    public static byte[] GeneratePdf(MonthlyFinanceDto summary, string ownerName)
    {
        var rows = summary.Collections.ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(rows.Length / 40m));
        return Document.Create(document =>
        {
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var pageRows = rows.Skip(pageIndex * 40).Take(40).ToArray();
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(x => x.FontFamily("Arial", "Noto Sans Arabic", "DejaVu Sans")
                        .FontSize(8.5f).FontColor(Colors.Black));
                    page.Content().Column(content =>
                    {
                        content.Item().AlignCenter().Text($"كشف تحصيل شهر {summary.MonthLabel}")
                            .FontSize(18).Bold().FontColor("#176B5B");
                        content.Item().PaddingTop(3).Row(info =>
                        {
                            info.RelativeItem().Text($"المسؤول: {ownerName}").Bold();
                            info.RelativeItem().AlignCenter().Text($"إجمالي المطلوب: {Money(summary.TotalRequired)}");
                            info.RelativeItem().AlignLeft().Text($"إجمالي المتبقي: {Money(summary.TotalRemaining)}");
                        });
                        content.Item().PaddingTop(5).LineHorizontal(1).LineColor("#176B5B");
                        content.Item().PaddingTop(9).Row(blocks =>
                        {
                            // RTL direction places the first block on the physical right.
                            blocks.RelativeItem().Element(container => CollectionBlock(container,
                                pageRows.Take(20).ToArray(), pageIndex * 40 + 1));
                            blocks.ConstantItem(8);
                            blocks.RelativeItem().Element(container => CollectionBlock(container,
                                pageRows.Skip(20).Take(20).ToArray(), pageIndex * 40 + 21));
                        });
                    });
                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text("توقيع مسؤول التحصيل: ____________________")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                        footer.ConstantItem(85).AlignLeft().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken2));
                            text.Span("صفحة "); text.CurrentPageNumber(); text.Span(" من "); text.TotalPages();
                        });
                    });
                });
            }
        }).GeneratePdf();
    }

    public Task<GeneratedReportDto> GenerateAssociationAsync(string userId, bool includeAllOwners,
        Guid associationId, CancellationToken cancellationToken) =>
        associationReports.GenerateAssociationAsync(userId, includeAllOwners, associationId, cancellationToken);

    private static void CollectionBlock(IContainer container, MonthlyCollectionPersonDto[] people,
        int startNumber)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);
                columns.RelativeColumn(2.1f);
                columns.RelativeColumn(1.65f);
                columns.RelativeColumn(1.25f);
            });
            Header(table.Cell()).Height(27).Text("م");
            Header(table.Cell()).Height(27).Text("الاسم بالكامل");
            Header(table.Cell()).Height(27).Text("المبلغ / الحالة");
            Header(table.Cell()).Height(27).Text("التوقيع");
            for (var index = 0; index < 20; index++)
            {
                var person = index < people.Length ? people[index] : null;
                Cell(table.Cell()).Height(27).Text(person is null ? string.Empty :
                    (startNumber + index).ToString(ArabicCulture));
                Cell(table.Cell()).Height(27).AlignRight().Text(person?.PersonName ?? string.Empty)
                    .FontSize(7.5f).SemiBold();
                var label = person is null
                    ? string.Empty
                    : person.ScheduledPayouts > 0
                        ? $"صافي القبض\n{Money(person.NetPayout)}"
                        : $"المطلوب تحصيله\n{Money(person.Remaining)}";
                Cell(table.Cell()).Height(27).Text(label).FontSize(6.7f).SemiBold();
                Cell(table.Cell()).Height(27).Text(string.Empty);
            }
        });
    }

    private static IContainer Header(IContainer container) => container.Border(1).BorderColor(Colors.Black)
        .Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().AlignMiddle()
        .DefaultTextStyle(x => x.Bold().FontSize(7.5f));

    private static IContainer Cell(IContainer container) => container.Border(1).BorderColor(Colors.Black)
        .PaddingHorizontal(3).PaddingVertical(2).AlignCenter().AlignMiddle();

    private static string Money(decimal amount) => $"{amount.ToString("N2", ArabicCulture)} ج.م";
}
