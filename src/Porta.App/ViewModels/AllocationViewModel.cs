using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Models;
using Porta.Services;

namespace Porta.ViewModels;

/// <summary>One editable row in the Allocation view.</summary>
public sealed partial class AllocationRowViewModel : ObservableObject
{
    private readonly Action _onTargetChanged;

    public AllocationRowViewModel(AssetCategory category, AllocationItemDto allocation, Action onTargetChanged)
    {
        _onTargetChanged = onTargetChanged;
        Category = category;
        Allocation = allocation;
        TargetText = Fmt.Editable(category.TargetPercentage);
    }

    public AssetCategory Category { get; }
    public AllocationItemDto Allocation { get; }

    public string CategoryName => Category.CategoryName;
    public string RiskLevel => Category.RiskLevel;
    public string? Description => Category.Description;
    public decimal MarketValue => Allocation.MarketValue;
    public decimal ActualPercentage => Allocation.ActualPercentage;
    public decimal Deviation => Allocation.Deviation;
    public RebalanceStatus Status => Allocation.Status;
    public string StatusLabel => Allocation.StatusLabel;
    public double ActualBar => (double)Math.Min(100m, Allocation.ActualPercentage);

    [ObservableProperty] private string _targetText = "";

    public decimal? ParsedTarget => Fmt.TryParse(TargetText, out var v) ? v : null;

    partial void OnTargetTextChanged(string value) => _onTargetChanged();
}

/// <summary>Category management and target allocation with 100% validation (FR-12, PRD 8.4).</summary>
public sealed partial class AllocationViewModel(
    PortfolioService portfolio,
    DialogService dialogs,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    public override string Title => "Kategori & Rebalancing";

    public ObservableCollection<AllocationRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCategoryCommand), nameof(DeleteCategoryCommand))]
    private AllocationRowViewModel? _selectedRow;

    [ObservableProperty] private decimal _totalTarget;
    [ObservableProperty] private bool _isTotalValid;
    [ObservableProperty] private decimal _netWorth;

    public string TotalTargetText => $"Total target: {Fmt.Percent(TotalTarget)}";
    public bool IsTotalInvalid => !IsTotalValid;

    partial void OnTotalTargetChanged(decimal value) => OnPropertyChanged(nameof(TotalTargetText));
    partial void OnIsTotalValidChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTotalInvalid));
        SaveTargetsCommand.NotifyCanExecuteChanged();
    }

    protected override async Task LoadAsync()
    {
        var categories = await portfolio.GetCategoriesAsync();
        var (summary, allocation) = await portfolio.GetOverviewAsync();
        NetWorth = summary.TotalNetWorth;

        Rows.Clear();
        foreach (var c in categories)
        {
            var a = allocation.First(x => x.CategoryId == c.CategoryId);
            Rows.Add(new AllocationRowViewModel(c, a, RecalculateTotal));
        }
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        var parsed = Rows.Select(r => r.ParsedTarget).ToList();
        TotalTarget = parsed.Sum(p => p ?? 0);
        IsTotalValid = parsed.All(p => p is >= 0 and <= 100) && TotalTarget == 100m;
    }

    private bool CanSaveTargets() => IsTotalValid && CanEdit;

    [RelayCommand(CanExecute = nameof(CanSaveTargets))]
    private Task SaveTargetsAsync() => TryRunAsync(async () =>
    {
        var targets = Rows.Select(r => (r.Category.CategoryId, r.ParsedTarget ?? 0)).ToList();
        await portfolio.SaveTargetsAsync(targets);
        await LoadAsync();
    }, "Target alokasi berhasil disimpan. Status rebalancing diperbarui.");

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var form = new CategoryFormViewModel(portfolio, null);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Kategori {form.CategoryName.Trim()} berhasil ditambahkan.");
            await TryRunAsync(LoadAsync);
        }
    }

    private bool HasSelection() => SelectedRow is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditCategoryAsync()
    {
        if (SelectedRow is null) return;
        var form = new CategoryFormViewModel(portfolio, SelectedRow.Category);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Kategori {form.CategoryName.Trim()} berhasil diperbarui.");
            await TryRunAsync(LoadAsync);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedRow is not { } row) return;
        if (!await dialogs.ConfirmAsync("Hapus Kategori", $"Hapus kategori {row.CategoryName}?")) return;
        await TryRunAsync(async () =>
        {
            await portfolio.DeleteCategoryAsync(row.Category.CategoryId);
            await LoadAsync();
        }, $"Kategori {row.CategoryName} berhasil dihapus.");
    }
}

public sealed partial class CategoryFormViewModel : DialogViewModelBase
{
    private readonly PortfolioService _portfolio;
    private readonly AssetCategory? _existing;

    public CategoryFormViewModel(PortfolioService portfolio, AssetCategory? existing)
    {
        _portfolio = portfolio;
        _existing = existing;
        if (existing is not null)
        {
            CategoryName = existing.CategoryName;
            TargetText = Fmt.Editable(existing.TargetPercentage);
            RiskLevel = existing.RiskLevel;
            Description = existing.Description ?? "";
        }
    }

    public override string DialogTitle => _existing is null ? "Tambah Kategori Aset" : $"Edit Kategori {_existing.CategoryName}";
    public string[] RiskLevels { get; } = ["Rendah", "Sedang", "Tinggi"];

    [ObservableProperty] private string _categoryName = "";
    [ObservableProperty] private string _targetText = "0";
    [ObservableProperty] private string _riskLevel = "Sedang";
    [ObservableProperty] private string _description = "";

    [RelayCommand]
    private Task SaveAsync() => SubmitAsync(async () =>
    {
        if (!Fmt.TryParse(TargetText, out var target))
            throw new ArgumentException("Target persentase harus berupa angka.");
        await _portfolio.SaveCategoryAsync(new AssetCategory
        {
            CategoryId = _existing?.CategoryId ?? 0,
            CategoryName = CategoryName,
            TargetPercentage = target,
            RiskLevel = RiskLevel,
            Description = Description
        });
    });
}
