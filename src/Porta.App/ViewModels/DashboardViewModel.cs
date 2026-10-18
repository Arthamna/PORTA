using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porta.Controls;
using Porta.Models;
using Porta.Repositories;
using Porta.Services;

namespace Porta.ViewModels;

public sealed class AllocationLegendItem
{
    public required AllocationItemDto Item { get; init; }
    public required Avalonia.Media.IBrush Brush { get; init; }

    public string CategoryName => Item.CategoryName;
    public decimal MarketValue => Item.MarketValue;
    public decimal ActualPercentage => Item.ActualPercentage;
    public decimal TargetPercentage => Item.TargetPercentage;
    public decimal Deviation => Item.Deviation;
    public RebalanceStatus Status => Item.Status;
    public string StatusShort => Item.StatusShort;
    public double ActualBar => (double)Math.Min(100m, Item.ActualPercentage);
    public double TargetBar => (double)Math.Min(100m, Item.TargetPercentage);
}

/// <summary>Dashboard: KPI cards, allocation donut, rebalancing panel, last 5 transactions (FR-03, FR-04, US-01).</summary>
public sealed partial class DashboardViewModel(
    PortfolioService portfolio,
    ITransactionRepository transactions,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    public override string Title => "Dashboard";

    [ObservableProperty] private PortfolioSummaryDto _summary = new();
    [ObservableProperty] private IReadOnlyList<ChartSlice> _slices = [];
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private int _alertCount;

    public ObservableCollection<AllocationLegendItem> Allocation { get; } = [];
    public ObservableCollection<Transaction> RecentTransactions { get; } = [];

    public bool HasNoData => !HasData;
    public bool HasRecentTransactions => RecentTransactions.Count > 0;
    public bool HasAlerts => AlertCount > 0;

    partial void OnHasDataChanged(bool value) => OnPropertyChanged(nameof(HasNoData));
    partial void OnAlertCountChanged(int value) => OnPropertyChanged(nameof(HasAlerts));

    protected override async Task LoadAsync()
    {
        var (summary, allocation) = await portfolio.GetOverviewAsync();
        var recent = await transactions.GetRecentAsync(5);

        Summary = summary;

        Allocation.Clear();
        var slices = new List<ChartSlice>();
        for (var i = 0; i < allocation.Count; i++)
        {
            var brush = ChartPalette.Get(i);
            Allocation.Add(new AllocationLegendItem { Item = allocation[i], Brush = brush });
            if (allocation[i].MarketValue > 0)
                slices.Add(new ChartSlice(allocation[i].CategoryName, (double)allocation[i].MarketValue, brush));
        }
        Slices = slices;
        HasData = summary.TotalNetWorth > 0;
        AlertCount = HasData ? allocation.Count(a => a.Status != RebalanceStatus.Ideal) : 0;

        RecentTransactions.Clear();
        foreach (var t in recent) RecentTransactions.Add(t);
        OnPropertyChanged(nameof(HasRecentTransactions));
    }
}
