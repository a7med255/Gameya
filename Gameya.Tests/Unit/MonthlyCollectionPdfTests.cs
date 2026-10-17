using System.Text;
using System.Text.RegularExpressions;
using Gameya.Application.DTOs;
using Gameya.Infrastructure.Reports;
using QuestPDF.Infrastructure;

namespace Gameya.Tests.Unit;

public sealed partial class MonthlyCollectionPdfTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 1)]
    [InlineData(40, 1)]
    [InlineData(41, 2)]
    public void Pdf_uses_forty_people_per_page_and_accepts_long_arabic_names(int peopleCount, int expectedPages)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var people = Enumerable.Range(1, peopleCount).Select(index => Person(index)).ToArray();
        var summary = new MonthlyFinanceDto(2026, 11, "نوفمبر 2026", people.Sum(x => x.Required), 0,
            people.Sum(x => x.Remaining), 0, 0, 0, 2, people.Length, people.Count(x => x.ScheduledPayouts > 0),
            people, []);

        var pdf = MonthlyCollectionReportGenerator.GeneratePdf(summary, "مسؤول الجمعية التجريبية");

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1_000);
        var source = Encoding.Latin1.GetString(pdf);
        Assert.Equal(expectedPages, PageObjectRegex().Matches(source).Count);
        if (peopleCount == 41 && Environment.GetEnvironmentVariable("GAMEYA_PDF_PREVIEW_PATH") is { Length: > 0 } path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, pdf);
        }
    }

    private static MonthlyCollectionPersonDto Person(int index)
    {
        var id = Guid.NewGuid();
        var required = index == 40 ? 9_999_999_999.99m : index * 1_000m;
        var payout = index % 7 == 0 ? required + 50_000m : 0;
        return new MonthlyCollectionPersonDto(id,
            index == 21 ? "عبد الرحمن محمد أحمد محمود مصطفى صاحب الاسم العربي الطويل جداً للاختبار" : $"مشترك رقم {index}",
            required, 0, required, 0, payout, Math.Max(0, payout - required), Math.Max(0, required - payout),
            [new MonthlyAssociationAmountDto(Guid.NewGuid(), "جمعية الاختبار", Guid.NewGuid(), 1,
                new DateOnly(2026, 11, 10), required, 0, required, 0)],
            payout > 0 ? [new MonthlyPayoutAmountDto(Guid.NewGuid(), "جمعية القبض", Guid.NewGuid(), 1, payout)] : []);
    }

    [GeneratedRegex(@"/Type\s*/Page(?!s)")]
    private static partial Regex PageObjectRegex();
}
