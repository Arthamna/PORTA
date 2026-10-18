using Porta.Models;
using Porta.Services;
using Xunit;

namespace Porta.Tests;

/// <summary>PRD section 16 testing scenarios and section 8 calculation rules.</summary>
public class PortfolioCalculationServiceTests
{
    private readonly PortfolioCalculationService _calc = new();

    [Fact]
    public void Buy_TwoLots_AveragePriceIsWeighted()
    {
        // Beli 100 @ 1.000 lalu 100 @ 2.000 (tanpa fee) -> 200 unit, avg 1.500
        var first = _calc.ApplyBuy(0, 0, 100, 1000, 0);
        var second = _calc.ApplyBuy(first.TotalUnits, first.AverageBuyPrice, 100, 2000, 0);

        Assert.Equal(200m, second.TotalUnits);
        Assert.Equal(1500m, second.AverageBuyPrice);
        Assert.Null(second.RealizedPnL);
    }

    [Fact]
    public void Buy_FeeIsIncludedInAveragePrice()
    {
        // (1000 x 9.800 + 14.700) / 1000 = 9.814,70
        var r = _calc.ApplyBuy(0, 0, 1000, 9800, 14700);

        Assert.Equal(9814.70m, r.AverageBuyPrice);
        Assert.Equal(9_814_700m, r.TotalAmount);
    }

    [Fact]
    public void Sell_MoreThanHeld_IsRejected()
    {
        // Menjual 250 unit saat kepemilikan hanya 200
        var ex = Assert.Throws<InsufficientUnitsException>(() => _calc.ApplySell(200, 1500, 250, 2000, 0));
        Assert.Equal(250m, ex.Requested);
        Assert.Equal(200m, ex.Available);
    }

    [Fact]
    public void Sell_PartialPosition_RealizedPnLAndAverageUnchanged()
    {
        // Menjual 50 @ 2.000 dari avg 1.500 -> sisa 150, avg tetap 1.500, realized 25.000
        var r = _calc.ApplySell(200, 1500, 50, 2000, 0);

        Assert.Equal(150m, r.TotalUnits);
        Assert.Equal(1500m, r.AverageBuyPrice);
        Assert.Equal(25_000m, r.RealizedPnL);
    }

    [Fact]
    public void Sell_WithFee_ReducesNetProceeds()
    {
        var r = _calc.ApplySell(100, 1000, 100, 1200, 5000);

        Assert.Equal(115_000m, r.TotalAmount);
        Assert.Equal(15_000m, r.RealizedPnL);
    }

    [Fact]
    public void Sell_AllUnits_AveragePriceResetsToZero()
    {
        var r = _calc.ApplySell(100, 1500, 100, 1400, 0);

        Assert.Equal(0m, r.TotalUnits);
        Assert.Equal(0m, r.AverageBuyPrice);
        Assert.Equal(-10_000m, r.RealizedPnL);
    }

    [Fact]
    public void Dividend_AddsRealizedReturn_WithoutChangingUnits()
    {
        var r = _calc.ApplyDividend(1500, 9747.9333m, 1500, 270, 0);

        Assert.Equal(1500m, r.TotalUnits);
        Assert.Equal(9747.9333m, r.AverageBuyPrice);
        Assert.Equal(405_000m, r.RealizedPnL);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(-5, 1000, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 1000, -1)]
    public void InvalidInput_IsRejected(decimal units, decimal price, decimal fee)
    {
        Assert.Throws<ArgumentException>(() => _calc.ApplyBuy(0, 0, units, price, fee));
    }

    [Fact]
    public void CryptoFractionalUnits_KeepPrecision()
    {
        var r = _calc.ApplyBuy(0, 0, 0.02m, 1_000_000_000m, 200_000m);

        Assert.Equal(0.02m, r.TotalUnits);
        Assert.Equal(1_010_000_000m, r.AverageBuyPrice);
    }

    [Fact]
    public void Holding_UnrealizedPnL()
    {
        var m = _calc.CalculateHolding(200, 1500, 1800);

        Assert.Equal(360_000m, m.MarketValue);
        Assert.Equal(300_000m, m.InvestedCapital);
        Assert.Equal(60_000m, m.UnrealizedPnL);
        Assert.Equal(20m, m.UnrealizedPnLPercentage);
    }

    [Fact]
    public void Holding_ZeroUnits_NoDivideByZero()
    {
        var m = _calc.CalculateHolding(0, 0, 1000);

        Assert.Equal(0m, m.UnrealizedPnLPercentage);
    }

    [Theory]
    [InlineData(5.01, RebalanceStatus.Overweight)]
    [InlineData(5.00, RebalanceStatus.Ideal)]
    [InlineData(0, RebalanceStatus.Ideal)]
    [InlineData(-5.00, RebalanceStatus.Ideal)]
    [InlineData(-5.01, RebalanceStatus.Underweight)]
    public void RebalanceStatus_UsesFivePercentBand(decimal deviation, RebalanceStatus expected)
    {
        Assert.Equal(expected, _calc.GetRebalanceStatus(deviation));
    }

    [Fact]
    public void Allocation_PriceIncreasePushesCategoryOverweight()
    {
        var categories = new List<AssetCategory>
        {
            new() { CategoryId = 1, CategoryName = "Saham", TargetPercentage = 50 },
            new() { CategoryId = 2, CategoryName = "Kas", TargetPercentage = 50 }
        };
        var stock = new Asset { CategoryId = 1, TotalUnits = 100, AverageBuyPrice = 1000, CurrentMarketPrice = 1000 };
        var cash = new Asset { CategoryId = 2, TotalUnits = 100_000, AverageBuyPrice = 1, CurrentMarketPrice = 1 };

        var before = _calc.CalculateAllocation(categories, [stock, cash]);
        Assert.All(before, a => Assert.Equal(RebalanceStatus.Ideal, a.Status));

        // Mengubah harga pasar hingga porsi kategori melampaui target > 5%
        stock.CurrentMarketPrice = 1500;
        var after = _calc.CalculateAllocation(categories, [stock, cash]);

        Assert.Equal(60m, after[0].ActualPercentage);
        Assert.Equal(RebalanceStatus.Overweight, after[0].Status);
        Assert.Equal(RebalanceStatus.Underweight, after[1].Status);
    }

    [Fact]
    public void Allocation_EmptyPortfolio_IsZero()
    {
        var categories = new List<AssetCategory> { new() { CategoryId = 1, TargetPercentage = 100 } };
        var result = _calc.CalculateAllocation(categories, []);

        Assert.Equal(0m, result[0].ActualPercentage);
    }

    [Fact]
    public void Replay_RebuildsPositionAndRealizedPnL()
    {
        var history = new List<Transaction>
        {
            new() { TransactionType = TransactionType.BUY, Units = 100, PricePerUnit = 1000 },
            new() { TransactionType = TransactionType.BUY, Units = 100, PricePerUnit = 2000 },
            new() { TransactionType = TransactionType.SELL, Units = 50, PricePerUnit = 2000 },
            new() { TransactionType = TransactionType.DIVIDEND, Units = 150, PricePerUnit = 10 }
        };

        var (final, steps) = _calc.Replay(history);

        Assert.Equal(150m, final.TotalUnits);
        Assert.Equal(1500m, final.AverageBuyPrice);
        Assert.Equal(25_000m, steps[2].RealizedPnL);
        Assert.Equal(1_500m, steps[3].RealizedPnL);
    }

    [Fact]
    public void Replay_DeletingABuyThatFundsALaterSell_Throws()
    {
        // History after deleting the first BUY: BUY 100, SELL 150 -> invalid
        var history = new List<Transaction>
        {
            new() { TransactionType = TransactionType.BUY, Units = 100, PricePerUnit = 2000 },
            new() { TransactionType = TransactionType.SELL, Units = 150, PricePerUnit = 2000 }
        };

        Assert.Throws<InsufficientUnitsException>(() => _calc.Replay(history));
    }

    [Fact]
    public void SeedData_BbcaAveragePrice_MatchesFormula()
    {
        // database/02_seed.sql: BUY 1000 @ 9.800 + 14.700, BUY 500 @ 9.600 + 7.200
        var a = _calc.ApplyBuy(0, 0, 1000, 9800, 14700);
        var b = _calc.ApplyBuy(a.TotalUnits, a.AverageBuyPrice, 500, 9600, 7200);

        Assert.Equal(1500m, b.TotalUnits);
        Assert.Equal(9747.9333m, b.AverageBuyPrice);
    }
}
