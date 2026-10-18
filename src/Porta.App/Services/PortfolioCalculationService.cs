using Porta.Models;

namespace Porta.Services;

public sealed class InsufficientUnitsException(decimal requested, decimal available)
    : InvalidOperationException($"Jumlah unit tidak mencukupi. Unit dijual: {requested:N6}, unit dimiliki: {available:N6}.")
{
    public decimal Requested { get; } = requested;
    public decimal Available { get; } = available;
}

/// <summary>
/// All financial formulas from PRD section 8, isolated from UI and database (NFR-05).
/// Pure and deterministic; every number is <see cref="decimal"/> (NFR-02).
/// </summary>
public sealed class PortfolioCalculationService
{
    public const decimal RebalanceThreshold = 5m;

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundPrice(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    private static decimal RoundUnits(decimal v) => Math.Round(v, 6, MidpointRounding.AwayFromZero);

    /// <summary>PRD 8.1 — weighted average buy price including fee.</summary>
    public PositionResult ApplyBuy(decimal unitsHeld, decimal avgBuyPrice, decimal buyUnits, decimal pricePerUnit, decimal fee)
    {
        ValidatePositive(buyUnits, pricePerUnit, fee);

        var totalCostOld = unitsHeld * avgBuyPrice;
        var totalCostNew = buyUnits * pricePerUnit + fee;
        var unitsFinal = RoundUnits(unitsHeld + buyUnits);
        var avgNew = RoundPrice((totalCostOld + totalCostNew) / unitsFinal);

        return new PositionResult(unitsFinal, avgNew, null, RoundMoney(totalCostNew));
    }

    /// <summary>PRD 8.2 — realized P&amp;L on sale; average price unchanged (0 when fully sold).</summary>
    public PositionResult ApplySell(decimal unitsHeld, decimal avgBuyPrice, decimal sellUnits, decimal pricePerUnit, decimal fee)
    {
        ValidatePositive(sellUnits, pricePerUnit, fee);
        if (sellUnits > unitsHeld)
            throw new InsufficientUnitsException(sellUnits, unitsHeld);

        var netProceeds = sellUnits * pricePerUnit - fee;
        var costOfSoldUnits = sellUnits * avgBuyPrice;
        var realized = RoundMoney(netProceeds - costOfSoldUnits);
        var unitsFinal = RoundUnits(unitsHeld - sellUnits);
        var avgFinal = unitsFinal == 0 ? 0m : avgBuyPrice;

        return new PositionResult(unitsFinal, avgFinal, realized, RoundMoney(netProceeds));
    }

    /// <summary>FR-10 — dividend/yield adds realized return without changing units.</summary>
    public PositionResult ApplyDividend(decimal unitsHeld, decimal avgBuyPrice, decimal eligibleUnits, decimal dividendPerUnit, decimal fee)
    {
        ValidatePositive(eligibleUnits, dividendPerUnit, fee);
        var amount = eligibleUnits * dividendPerUnit;
        var realized = RoundMoney(amount - fee);
        return new PositionResult(unitsHeld, avgBuyPrice, realized, RoundMoney(amount));
    }

    public PositionResult Apply(TransactionType type, decimal unitsHeld, decimal avgBuyPrice, decimal units, decimal pricePerUnit, decimal fee) =>
        type switch
        {
            TransactionType.BUY => ApplyBuy(unitsHeld, avgBuyPrice, units, pricePerUnit, fee),
            TransactionType.SELL => ApplySell(unitsHeld, avgBuyPrice, units, pricePerUnit, fee),
            TransactionType.DIVIDEND => ApplyDividend(unitsHeld, avgBuyPrice, units, pricePerUnit, fee),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    /// <summary>
    /// Rebuilds a position from scratch by replaying transactions in chronological order.
    /// Used after deleting a transaction so Assets, Transactions.RealizedPnL and TotalAmount stay consistent.
    /// Throws <see cref="InsufficientUnitsException"/> if the remaining history becomes invalid.
    /// </summary>
    public (PositionResult Final, IReadOnlyList<PositionResult> Steps) Replay(IEnumerable<Transaction> orderedTransactions)
    {
        decimal units = 0, avg = 0;
        var steps = new List<PositionResult>();
        var final = new PositionResult(0, 0, null, 0);
        foreach (var t in orderedTransactions)
        {
            final = Apply(t.TransactionType, units, avg, t.Units, t.PricePerUnit, t.Fee);
            units = final.TotalUnits;
            avg = final.AverageBuyPrice;
            steps.Add(final);
        }
        return (final with { RealizedPnL = null, TotalAmount = 0 }, steps);
    }

    /// <summary>PRD 8.3 — unrealized P&amp;L for an open position.</summary>
    public HoldingMetrics CalculateHolding(decimal totalUnits, decimal avgBuyPrice, decimal currentMarketPrice)
    {
        var marketValue = RoundMoney(totalUnits * currentMarketPrice);
        var invested = RoundMoney(totalUnits * avgBuyPrice);
        var unrealized = marketValue - invested;
        var pct = invested == 0 ? 0m : Math.Round(unrealized / invested * 100m, 2, MidpointRounding.AwayFromZero);
        return new HoldingMetrics(marketValue, invested, unrealized, pct);
    }

    public PortfolioSummaryDto CalculateSummary(IEnumerable<Asset> assets, decimal totalRealizedPnL)
    {
        decimal marketValue = 0, invested = 0;
        foreach (var a in assets)
        {
            var m = CalculateHolding(a.TotalUnits, a.AverageBuyPrice, a.CurrentMarketPrice);
            marketValue += m.MarketValue;
            invested += m.InvestedCapital;
        }
        var unrealized = marketValue - invested;
        return new PortfolioSummaryDto
        {
            TotalNetWorth = marketValue,
            TotalInvestedCapital = invested,
            TotalUnrealizedPnL = unrealized,
            TotalUnrealizedPnLPercentage = invested == 0 ? 0 : Math.Round(unrealized / invested * 100m, 2, MidpointRounding.AwayFromZero),
            TotalRealizedPnL = totalRealizedPnL
        };
    }

    /// <summary>PRD 8.4 — status from deviation between actual and target allocation.</summary>
    public RebalanceStatus GetRebalanceStatus(decimal deviation) =>
        deviation > RebalanceThreshold ? RebalanceStatus.Overweight
        : deviation < -RebalanceThreshold ? RebalanceStatus.Underweight
        : RebalanceStatus.Ideal;

    /// <summary>PRD 8.4 — actual allocation per category vs. target.</summary>
    public IReadOnlyList<AllocationItemDto> CalculateAllocation(IEnumerable<AssetCategory> categories, IEnumerable<Asset> assets)
    {
        var valueByCategory = assets
            .GroupBy(a => a.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(a => CalculateHolding(a.TotalUnits, a.AverageBuyPrice, a.CurrentMarketPrice).MarketValue));
        var netWorth = valueByCategory.Values.Sum();

        return categories.Select(c =>
        {
            var value = valueByCategory.GetValueOrDefault(c.CategoryId);
            var actual = netWorth == 0 ? 0m : Math.Round(value / netWorth * 100m, 2, MidpointRounding.AwayFromZero);
            var deviation = actual - c.TargetPercentage;
            return new AllocationItemDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                MarketValue = value,
                TargetPercentage = c.TargetPercentage,
                ActualPercentage = actual,
                Deviation = deviation,
                Status = GetRebalanceStatus(deviation)
            };
        }).ToList();
    }

    private static void ValidatePositive(decimal units, decimal price, decimal fee)
    {
        if (units <= 0) throw new ArgumentException("Jumlah unit harus lebih besar dari 0.");
        if (price <= 0) throw new ArgumentException("Harga per unit harus lebih besar dari 0.");
        if (fee < 0) throw new ArgumentException("Biaya transaksi tidak boleh negatif.");
    }
}
