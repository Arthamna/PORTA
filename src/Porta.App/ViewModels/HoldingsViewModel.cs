using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Models;
using Porta.Services;

namespace Porta.ViewModels;

public sealed record FilterOption<T>(string Label, T? Value)
{
    public override string ToString() => Label;
}

/// <summary>Holdings: search, category filter, DataGrid, add/edit/delete asset, quick price update (FR-05..07).</summary>
public sealed partial class HoldingsViewModel(
    PortfolioService portfolio,
    DialogService dialogs,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    private CancellationTokenSource? _searchDebounce;
    private bool _loadingFilters;

    public override string Title => "Aset & Portofolio";

    public ObservableCollection<HoldingDto> Holdings { get; } = [];
    public ObservableCollection<FilterOption<int?>> CategoryOptions { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private FilterOption<int?>? _selectedCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(UpdatePriceCommand), nameof(DeleteCommand))]
    private HoldingDto? _selectedHolding;

    [ObservableProperty] private decimal _totalMarketValue;
    [ObservableProperty] private decimal _totalInvested;
    [ObservableProperty] private decimal _totalUnrealized;

    public bool HasSelection => SelectedHolding is not null;
    public bool IsEmpty => Holdings.Count == 0;

    partial void OnSearchTextChanged(string value) => _ = DebouncedReloadAsync();

    partial void OnSelectedCategoryChanged(FilterOption<int?>? value)
    {
        if (!_loadingFilters) _ = TryRunAsync(ReloadHoldingsAsync);
    }

    private async Task DebouncedReloadAsync()
    {
        _searchDebounce?.Cancel();
        var cts = _searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(250, cts.Token);
            await TryRunAsync(ReloadHoldingsAsync);
        }
        catch (TaskCanceledException)
        {
        }
    }

    protected override async Task LoadAsync()
    {
        var categories = await portfolio.GetCategoriesAsync();
        var selectedId = SelectedCategory?.Value;

        _loadingFilters = true;
        CategoryOptions.Clear();
        CategoryOptions.Add(new FilterOption<int?>("Semua Kategori", null));
        foreach (var c in categories) CategoryOptions.Add(new FilterOption<int?>(c.CategoryName, c.CategoryId));
        SelectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == selectedId) ?? CategoryOptions[0];
        _loadingFilters = false;

        await ReloadHoldingsAsync();
    }

    private async Task ReloadHoldingsAsync()
    {
        var selectedId = SelectedHolding?.AssetId;
        var list = await portfolio.GetHoldingsAsync(SearchText, SelectedCategory?.Value);

        Holdings.Clear();
        foreach (var h in list) Holdings.Add(h);
        SelectedHolding = Holdings.FirstOrDefault(h => h.AssetId == selectedId);

        TotalMarketValue = list.Sum(h => h.MarketValue);
        TotalInvested = list.Sum(h => h.InvestedCapital);
        TotalUnrealized = list.Sum(h => h.UnrealizedPnL);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var categories = await portfolio.GetCategoriesAsync();
        var form = new AssetFormViewModel(portfolio, categories, null);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Aset {form.Ticker.ToUpperInvariant()} berhasil ditambahkan.");
            await TryRunAsync(ReloadHoldingsAsync);
        }
    }

    private bool CanActOnSelection() => SelectedHolding is not null;

    [RelayCommand(CanExecute = nameof(CanActOnSelection))]
    private async Task EditAsync()
    {
        if (SelectedHolding is null) return;
        var categories = await portfolio.GetCategoriesAsync();
        var form = new AssetFormViewModel(portfolio, categories, SelectedHolding.Asset);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Aset {form.Ticker.ToUpperInvariant()} berhasil diperbarui.");
            await TryRunAsync(ReloadHoldingsAsync);
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnSelection))]
    private async Task UpdatePriceAsync()
    {
        if (SelectedHolding is null) return;
        var form = new UpdatePriceDialogViewModel(portfolio, SelectedHolding.Asset);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Harga pasar {SelectedHolding.TickerSymbol} diperbarui. Market value & P&L dihitung ulang.");
            await TryRunAsync(ReloadHoldingsAsync);
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedHolding is not { } h) return;
        var ok = await dialogs.ConfirmAsync("Hapus Aset",
            $"Hapus aset {h.TickerSymbol} — {h.AssetName}? Tindakan ini tidak dapat dibatalkan.");
        if (!ok) return;

        await TryRunAsync(async () =>
        {
            await portfolio.DeleteAssetAsync(h.AssetId);
            await ReloadHoldingsAsync();
        }, $"Aset {h.TickerSymbol} berhasil dihapus.");
    }
}

public sealed partial class AssetFormViewModel : DialogViewModelBase
{
    private readonly PortfolioService _portfolio;
    private readonly Asset? _existing;

    public AssetFormViewModel(PortfolioService portfolio, IReadOnlyList<AssetCategory> categories, Asset? existing)
    {
        _portfolio = portfolio;
        _existing = existing;
        Categories = categories;
        if (existing is not null)
        {
            Ticker = existing.TickerSymbol;
            AssetName = existing.AssetName;
            UnitType = existing.UnitType;
            PriceText = Fmt.Editable(existing.CurrentMarketPrice);
            SelectedCategory = categories.FirstOrDefault(c => c.CategoryId == existing.CategoryId);
        }
        else
        {
            SelectedCategory = categories.FirstOrDefault();
        }
    }

    public override string DialogTitle => _existing is null ? "Tambah Aset Baru" : $"Edit Aset {_existing.TickerSymbol}";
    public IReadOnlyList<AssetCategory> Categories { get; }
    public string[] UnitTypeSuggestions { get; } = ["Lembar", "Lot", "Koin", "Unit", "Gram", "IDR"];
    public bool IsEdit => _existing is not null;

    [ObservableProperty] private string _ticker = "";
    [ObservableProperty] private string _assetName = "";
    [ObservableProperty] private string _unitType = "Lembar";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private AssetCategory? _selectedCategory;

    [RelayCommand]
    private Task SaveAsync() => SubmitAsync(async () =>
    {
        if (!Fmt.TryParse(PriceText, out var price) || price <= 0)
            throw new ArgumentException("Harga pasar harus berupa angka lebih besar dari 0.");

        var asset = new Asset
        {
            AssetId = _existing?.AssetId ?? 0,
            TickerSymbol = Ticker,
            AssetName = AssetName,
            UnitType = UnitType,
            CategoryId = SelectedCategory?.CategoryId ?? 0,
            CurrentMarketPrice = price
        };
        await _portfolio.SaveAssetAsync(asset);
    });
}

public sealed partial class UpdatePriceDialogViewModel(PortfolioService portfolio, Asset asset) : DialogViewModelBase
{
    public override string DialogTitle => $"Update Harga Pasar — {asset.TickerSymbol}";
    public string AssetName => asset.AssetName;
    public string CurrentPriceText => Fmt.Money(asset.CurrentMarketPrice);
    public string LastUpdatedText => $"{Fmt.Date(asset.UpdatedAt)} {asset.UpdatedAt:HH:mm}";

    [ObservableProperty] private string _priceText = Fmt.Editable(asset.CurrentMarketPrice);

    [RelayCommand]
    private Task SaveAsync() => SubmitAsync(async () =>
    {
        if (!Fmt.TryParse(PriceText, out var price) || price <= 0)
            throw new ArgumentException("Harga pasar harus berupa angka lebih besar dari 0.");
        await portfolio.UpdateMarketPriceAsync(asset.AssetId, price);
    });
}
