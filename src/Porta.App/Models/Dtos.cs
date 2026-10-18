namespace Porta.Models;

/// <summary>Result of applying a BUY/SELL/DIVIDEND to a position.</summary>
public readonly record struct PositionResult(decimal TotalUnits, decimal AverageBuyPrice, decimal? RealizedPnL, decimal TotalAmount);

/// <summary>Running-position metrics for one asset (PRD 8.3).</summary>
public readonly record struct HoldingMetrics(decimal MarketValue, decimal InvestedCapital, decimal UnrealizedPnL, decimal UnrealizedPnLPercentage);

/// <summary>Row shown in the Holdings DataGrid.</summary>
public sealed class HoldingDto
{
    public required Asset Asset { get; init; }
    public required HoldingMetrics Metrics { get; init; }

    public int AssetId => Asset.AssetId;
    public string TickerSymbol => Asset.TickerSymbol;
    public string AssetName => Asset.AssetName;
    public string CategoryName => Asset.CategoryName;
    public string UnitType => Asset.UnitType;
    public decimal TotalUnits => Asset.TotalUnits;
    public decimal AverageBuyPrice => Asset.AverageBuyPrice;
    public decimal CurrentMarketPrice => Asset.CurrentMarketPrice;
    public decimal MarketValue => Metrics.MarketValue;
    public decimal InvestedCapital => Metrics.InvestedCapital;
    public decimal UnrealizedPnL => Metrics.UnrealizedPnL;
    public decimal UnrealizedPnLPercentage => Metrics.UnrealizedPnLPercentage;
}

/// <summary>Allocation / rebalancing row per category (PRD 8.4).</summary>
public sealed class AllocationItemDto
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public decimal MarketValue { get; init; }
    public decimal TargetPercentage { get; init; }
    public decimal ActualPercentage { get; init; }
    public decimal Deviation { get; init; }
    public RebalanceStatus Status { get; init; }

    public string StatusLabel => Status switch
    {
        RebalanceStatus.Overweight => "Overweight (Kurangi / Tahan Beli)",
        RebalanceStatus.Underweight => "Underweight (Tambah Alokasi)",
        _ => "Ideal (On-Track)"
    };

    public string StatusShort => Status.ToString();
}

/// <summary>Dashboard KPI summary (FR-03).</summary>
public sealed class PortfolioSummaryDto
{
    public decimal TotalNetWorth { get; init; }
    public decimal TotalInvestedCapital { get; init; }
    public decimal TotalUnrealizedPnL { get; init; }
    public decimal TotalUnrealizedPnLPercentage { get; init; }
    public decimal TotalRealizedPnL { get; init; }
}

/// <summary>Journal entry joined with its transaction and asset (Journal view).</summary>
public sealed class JournalViewDto
{
    public int JournalId { get; init; }
    public int TransactionId { get; init; }
    public DateTime TransactionDate { get; init; }
    public TransactionType TransactionType { get; init; }
    public string TickerSymbol { get; init; } = "";
    public string AssetName { get; init; } = "";
    public decimal Units { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal CurrentMarketPrice { get; init; }
    public string? ThesisNote { get; init; }
    public decimal? TargetPrice { get; init; }
    public decimal? StopLossPrice { get; init; }
    public string? EmotionTag { get; init; }
    public string? StrategyTag { get; init; }

    public decimal PriceChangePercentage =>
        EntryPrice == 0 ? 0 : Math.Round((CurrentMarketPrice - EntryPrice) / EntryPrice * 100m, 2);

    /// <summary>Plan (TP/SL) vs. realisation (current price).</summary>
    public string PlanStatus
    {
        get
        {
            if (TransactionType != TransactionType.BUY) return "—";
            if (TargetPrice is { } tp && CurrentMarketPrice >= tp) return "Target Price tercapai";
            if (StopLossPrice is { } sl && CurrentMarketPrice <= sl) return "Stop Loss tersentuh";
            if (TargetPrice is null && StopLossPrice is null) return "Tanpa rencana TP/SL";
            return "Dalam rentang rencana";
        }
    }
}
