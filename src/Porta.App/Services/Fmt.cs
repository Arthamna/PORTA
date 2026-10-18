using System.Globalization;

namespace Porta.Services;

/// <summary>
/// Indonesian number formatting (PRD section 10: "Rp 15.250.000"), independent of OS locale/ICU.
/// </summary>
public static class Fmt
{
    public static readonly NumberFormatInfo Id = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        PercentGroupSeparator = ".",
        PercentDecimalSeparator = ",",
        NegativeSign = "-"
    };

    public static string Money(decimal v, bool signed = false)
    {
        var abs = Math.Abs(v).ToString("#,##0.##", Id);
        var sign = v < 0 ? "-" : signed && v > 0 ? "+" : "";
        return $"{sign}Rp {abs}";
    }

    public static string Units(decimal v) => v.ToString("#,##0.######", Id);

    public static string Percent(decimal v, bool signed = false)
    {
        var s = Math.Abs(v).ToString("#,##0.00", Id);
        var sign = v < 0 ? "-" : signed && v > 0 ? "+" : "";
        return $"{sign}{s}%";
    }

    private static readonly string[] Days = ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"];
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"];

    public static string Date(DateTime d) => $"{d.Day:00} {Months[d.Month - 1]} {d.Year}";
    public static string LongDate(DateTime d) => $"{Days[(int)d.DayOfWeek]}, {Date(d)}";

    /// <summary>Formats a number for an editable text box (no thousand separators).</summary>
    public static string Editable(decimal v) => v.ToString("0.######", Id);

    /// <summary>
    /// Parses user input. Accepts "9800", "9.800.000", "0,025", "0.025", "Rp 1.500,50".
    /// Comma is treated as the decimal separator; with no comma, a single dot is a decimal point
    /// and multiple dots are thousand separators.
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().Replace("Rp", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
        if (s.Contains(','))
            s = s.Replace(".", "").Replace(',', '.');
        else if (s.Count(c => c == '.') > 1)
            s = s.Replace(".", "");
        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }
}
