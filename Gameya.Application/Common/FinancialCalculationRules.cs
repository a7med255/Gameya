using System.Globalization;

namespace Gameya.Application.Common;

public static class FinancialCalculationRules
{
    private static readonly CultureInfo ArabicEgypt = CultureInfo.GetCultureInfo("ar-EG");

    public static decimal ObligationAmount(decimal monthlyContribution, decimal shareCount) =>
        decimal.Round(monthlyContribution * shareCount, 2, MidpointRounding.AwayFromZero);

    public static decimal Currency(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy", ArabicEgypt);

    public static string PaymentStatus(decimal required, decimal paid)
    {
        if (required <= 0 || paid >= required) return "Paid";
        return paid > 0 ? "PartiallyPaid" : "Unpaid";
    }
}
