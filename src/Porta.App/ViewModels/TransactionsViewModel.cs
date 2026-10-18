using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Models;
using Porta.Repositories;
using Porta.Services;

namespace Porta.ViewModels;

/// <summary>Transaction history with date/type filters, new transaction form and delete (FR-08..FR-10).</summary>
public sealed partial class TransactionsViewModel(
    ITransactionRepository transactions,
    TransactionService transactionService,
    PortfolioService portfolio,
    PortfolioCalculationService calc,
    DialogService dialogs,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    public override string Title => "Riwayat Transaksi";

    public ObservableCollection<Transaction> Transactions { get; } = [];

    public IReadOnlyList<FilterOption<TransactionType?>> TypeOptions { get; } =
    [
        new("Semua Tipe", null),
        new("BUY", TransactionType.BUY),
        new("SELL", TransactionType.SELL),
        new("DIVIDEND", TransactionType.DIVIDEND)
    ];

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private FilterOption<TransactionType?>? _selectedType;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Transaction? _selectedTransaction;

    public bool IsEmpty => Transactions.Count == 0;

    partial void OnFromDateChanged(DateTime? value) => _ = TryRunAsync(LoadAsync);
    partial void OnToDateChanged(DateTime? value) => _ = TryRunAsync(LoadAsync);
    partial void OnSelectedTypeChanged(FilterOption<TransactionType?>? value) => _ = TryRunAsync(LoadAsync);

    protected override async Task LoadAsync()
    {
        var list = await transactions.GetAsync(new TransactionFilter(FromDate, ToDate, SelectedType?.Value));
        Transactions.Clear();
        foreach (var t in list) Transactions.Add(t);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FromDate = null;
        ToDate = null;
        SelectedType = TypeOptions[0];
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        List<Asset> assets;
        try
        {
            assets = await portfolio.GetAssetsAsync();
        }
        catch (Exception ex)
        {
            Notifications.Error(FriendlyMessage(ex));
            return;
        }
        if (assets.Count == 0)
        {
            Notifications.Info("Belum ada aset. Tambahkan aset terlebih dahulu di menu Aset & Portofolio.");
            return;
        }

        var form = new TransactionFormViewModel(transactionService, calc, assets);
        if (await dialogs.ShowAsync(form))
        {
            Notifications.Success($"Transaksi {form.SelectedType} {form.SelectedAsset?.TickerSymbol} berhasil disimpan.");
            await TryRunAsync(LoadAsync);
        }
    }

    private bool CanDelete() => SelectedTransaction is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedTransaction is not { } t) return;
        var ok = await dialogs.ConfirmAsync("Hapus Transaksi",
            $"Hapus transaksi {t.TransactionType} {t.TickerSymbol} ({Fmt.Units(t.Units)} unit, {Fmt.Date(t.TransactionDate)})?\n" +
            "Saldo unit, Average Price dan Realized P&L aset akan dihitung ulang.");
        if (!ok) return;

        await TryRunAsync(async () =>
        {
            await transactionService.DeleteAsync(t.TransactionId);
            await LoadAsync();
        }, "Transaksi berhasil dihapus dan portofolio dihitung ulang.");
    }
}

/// <summary>Form Transaksi &amp; Jurnal (modal) with live calculation preview.</summary>
public sealed partial class TransactionFormViewModel : DialogViewModelBase
{
    private readonly TransactionService _service;
    private readonly PortfolioCalculationService _calc;

    public TransactionFormViewModel(TransactionService service, PortfolioCalculationService calc, IReadOnlyList<Asset> assets)
    {
        _service = service;
        _calc = calc;
        Assets = assets;
        SelectedAsset = assets.FirstOrDefault();
        UpdatePreview();
    }

    public override string DialogTitle => "Catat Transaksi Baru";
    public IReadOnlyList<Asset> Assets { get; }
    public TransactionType[] Types { get; } = [TransactionType.BUY, TransactionType.SELL, TransactionType.DIVIDEND];
    public string[] EmotionOptions { get; } = EmotionTags.All;

    [ObservableProperty] private Asset? _selectedAsset;
    [ObservableProperty] private TransactionType _selectedType = TransactionType.BUY;
    [ObservableProperty] private DateTime? _date = DateTime.Today;
    [ObservableProperty] private string _unitsText = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _feeText = "0";

    // Journal section (FR-11)
    [ObservableProperty] private string _thesisNote = "";
    [ObservableProperty] private string _targetPriceText = "";
    [ObservableProperty] private string _stopLossText = "";
    [ObservableProperty] private string? _emotionTag;
    [ObservableProperty] private string _strategyTag = "";

    // Live preview
    [ObservableProperty] private string _holdingInfo = "";
    [ObservableProperty] private string _totalPreview = "—";
    [ObservableProperty] private string _resultPreview = "";
    [ObservableProperty] private string? _inlineWarning;

    public bool IsDividend => SelectedType == TransactionType.DIVIDEND;
    public bool IsBuy => SelectedType == TransactionType.BUY;
    public string UnitsLabel => IsDividend ? "Unit yang Berhak Dividen" : "Jumlah Unit";
    public string PriceLabel => IsDividend ? "Dividen / Bunga per Unit (Rp)" : "Harga per Unit (Rp)";
    public bool HasInlineWarning => !string.IsNullOrEmpty(InlineWarning);

    partial void OnSelectedAssetChanged(Asset? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(PriceText) && !IsDividend)
            PriceText = Fmt.Editable(value.CurrentMarketPrice);
        if (IsDividend && value is not null) UnitsText = Fmt.Editable(value.TotalUnits);
        UpdatePreview();
    }

    partial void OnSelectedTypeChanged(TransactionType value)
    {
        OnPropertyChanged(nameof(IsDividend));
        OnPropertyChanged(nameof(IsBuy));
        OnPropertyChanged(nameof(UnitsLabel));
        OnPropertyChanged(nameof(PriceLabel));
        if (value == TransactionType.DIVIDEND && SelectedAsset is not null)
        {
            UnitsText = Fmt.Editable(SelectedAsset.TotalUnits);
            PriceText = "";
        }
        else if (SelectedAsset is not null && string.IsNullOrWhiteSpace(PriceText))
        {
            PriceText = Fmt.Editable(SelectedAsset.CurrentMarketPrice);
        }
        UpdatePreview();
    }

    partial void OnUnitsTextChanged(string value) => UpdatePreview();
    partial void OnPriceTextChanged(string value) => UpdatePreview();
    partial void OnFeeTextChanged(string value) => UpdatePreview();
    partial void OnInlineWarningChanged(string? value) => OnPropertyChanged(nameof(HasInlineWarning));

    private void UpdatePreview()
    {
        InlineWarning = null;
        ResultPreview = "";
        TotalPreview = "—";

        if (SelectedAsset is not { } a)
        {
            HoldingInfo = "";
            return;
        }
        HoldingInfo = $"Dimiliki: {Fmt.Units(a.TotalUnits)} {a.UnitType} · Avg {Fmt.Money(a.AverageBuyPrice)} · Harga pasar {Fmt.Money(a.CurrentMarketPrice)}";

        if (!Fmt.TryParse(UnitsText, out var units) || !Fmt.TryParse(PriceText, out var price)) return;
        var fee = Fmt.TryParse(FeeText, out var f) ? f : 0;
        if (units <= 0 || price <= 0 || fee < 0) return;

        if (SelectedType == TransactionType.SELL && units > a.TotalUnits)
        {
            InlineWarning = $"Unit dijual ({Fmt.Units(units)}) melebihi unit yang dimiliki ({Fmt.Units(a.TotalUnits)}).";
            return;
        }

        try
        {
            var r = _calc.Apply(SelectedType, a.TotalUnits, a.AverageBuyPrice, units, price, fee);
            TotalPreview = Fmt.Money(r.TotalAmount);
            ResultPreview = SelectedType switch
            {
                TransactionType.BUY => $"Unit akhir {Fmt.Units(r.TotalUnits)} · Average Buy Price baru {Fmt.Money(r.AverageBuyPrice)}",
                TransactionType.SELL => $"Unit akhir {Fmt.Units(r.TotalUnits)} · Realized P&L {Fmt.Money(r.RealizedPnL ?? 0, true)}",
                _ => $"Realized Return {Fmt.Money(r.RealizedPnL ?? 0, true)} · unit tidak berubah"
            };
        }
        catch (Exception ex)
        {
            InlineWarning = ex.Message;
        }
    }

    private static decimal? ParseOptional(string text, string field)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!Fmt.TryParse(text, out var v) || v <= 0)
            throw new ArgumentException($"{field} harus berupa angka lebih besar dari 0.");
        return v;
    }

    [RelayCommand]
    private Task SaveAsync() => SubmitAsync(async () =>
    {
        if (SelectedAsset is null) throw new ArgumentException("Aset wajib dipilih.");
        if (Date is null) throw new ArgumentException("Tanggal transaksi wajib diisi.");
        if (!Fmt.TryParse(UnitsText, out var units)) throw new ArgumentException($"{UnitsLabel} wajib diisi dengan angka.");
        if (!Fmt.TryParse(PriceText, out var price)) throw new ArgumentException($"{PriceLabel} wajib diisi dengan angka.");
        decimal fee = 0;
        if (!string.IsNullOrWhiteSpace(FeeText) && !Fmt.TryParse(FeeText, out fee))
            throw new ArgumentException("Biaya transaksi harus berupa angka.");

        // Keep the time of day so same-day transactions stay in entry order.
        var date = Date.Value.Date == DateTime.Today ? DateTime.Now : Date.Value.Date.AddHours(12);
        date = date.AddTicks(-(date.Ticks % TimeSpan.TicksPerSecond));

        var input = new TransactionInput(SelectedAsset.AssetId, SelectedType, date, units, price, fee);
        if (TransactionService.Validate(input) is { } error) throw new ArgumentException(error);

        var journal = new JournalEntry
        {
            ThesisNote = ThesisNote,
            TargetPrice = ParseOptional(TargetPriceText, "Target Price"),
            StopLossPrice = ParseOptional(StopLossText, "Stop Loss"),
            EmotionTag = EmotionTag,
            StrategyTag = StrategyTag
        };

        await _service.RecordAsync(input, journal);
    });

    [RelayCommand]
    private void ClearEmotion() => EmotionTag = null;
}
