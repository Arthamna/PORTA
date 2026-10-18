using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Porta.Models;
using Porta.Services;

namespace Porta.Converters;

internal static class Semantic
{
    public static readonly IBrush Profit = new SolidColorBrush(Color.Parse("#10B981"));
    public static readonly IBrush Loss = new SolidColorBrush(Color.Parse("#F43F5E"));
    public static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
    public static readonly IBrush Primary = new SolidColorBrush(Color.Parse("#2563EB"));
    public static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#64748B"));
    public static readonly IBrush Violet = new SolidColorBrush(Color.Parse("#8B5CF6"));

    public static decimal ToDecimal(object? value) => value switch
    {
        decimal d => d,
        double d => (decimal)d,
        int i => i,
        null => 0,
        _ => 0
    };
}

/// <summary>decimal → "Rp 15.250.000". ConverterParameter "signed" adds +/−.</summary>
public sealed class MoneyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? "—" : Fmt.Money(Semantic.ToDecimal(value), parameter as string == "signed");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class UnitsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? "—" : Fmt.Units(Semantic.ToDecimal(value));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>decimal → "+12,34%". ConverterParameter "signed" adds +/−.</summary>
public sealed class PercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? "—" : Fmt.Percent(Semantic.ToDecimal(value), parameter as string == "signed");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime d ? Fmt.Date(d) : "—";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Positive → emerald, negative → rose, zero/null → neutral.</summary>
public sealed class PnLBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Semantic.Neutral;
        var d = Semantic.ToDecimal(value);
        return d > 0 ? Semantic.Profit : d < 0 ? Semantic.Loss : Semantic.Neutral;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Ideal → emerald, Overweight → rose, Underweight → amber.</summary>
public sealed class RebalanceBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RebalanceStatus.Overweight => Semantic.Loss,
        RebalanceStatus.Underweight => Semantic.Warning,
        _ => Semantic.Profit
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>BUY → emerald, SELL → rose, DIVIDEND → royal blue.</summary>
public sealed class TransactionTypeBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TransactionType.BUY => Semantic.Profit,
        TransactionType.SELL => Semantic.Loss,
        _ => Semantic.Primary
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Emotion tag → badge colour.</summary>
public sealed class EmotionBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "Rasional" => Semantic.Profit,
        "Riset Mendalam" => Semantic.Primary,
        "FOMO" => Semantic.Warning,
        "Panik" => Semantic.Loss,
        "Spekulasi" => Semantic.Violet,
        _ => Semantic.Neutral
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Toast kind → background brush.</summary>
public sealed class ToastBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ToastKind.Success => Semantic.Profit,
        ToastKind.Error => Semantic.Loss,
        _ => Semantic.Primary
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
