using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Services;
using Porta.Views;

namespace Porta.ViewModels;

public sealed class NavItem(string title, Geometry icon, PageViewModelBase page)
{
    public string Title { get; } = title;
    public Geometry Icon { get; } = icon;
    public PageViewModelBase Page { get; } = page;
}

/// <summary>Shell: sidebar navigation, header bar, login gate, toasts and dialog overlay.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly AuthService _auth;
    private readonly PortfolioService _portfolio;

    public LoginViewModel Login { get; }
    public ThemeService Theme { get; }
    public NotificationService Notifications { get; }
    public DialogService Dialogs { get; }
    public IReadOnlyList<NavItem> NavItems { get; }

    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private NavItem? _selectedNav;
    [ObservableProperty] private PageViewModelBase? _currentPage;
    [ObservableProperty] private string _headerNetWorth = Fmt.Money(0);
    [ObservableProperty] private string _headerPnL = Fmt.Money(0);
    [ObservableProperty] private decimal _headerPnLValue;

    public string UserFullName => _session.CurrentUser?.FullName ?? "";
    public string UserRole => _session.CurrentUser?.Role.ToString() ?? "";
    public string UserInitial => string.IsNullOrEmpty(UserFullName) ? "?" : UserFullName[..1].ToUpperInvariant();
    public bool IsReviewer => _session.IsAuthenticated && !_session.CanEdit;
    public string TodayText => Fmt.LongDate(DateTime.Now);

    public MainViewModel(
        SessionService session,
        AuthService auth,
        PortfolioService portfolio,
        LoginViewModel login,
        ThemeService theme,
        NotificationService notifications,
        DialogService dialogs,
        DashboardViewModel dashboard,
        HoldingsViewModel holdings,
        TransactionsViewModel transactions,
        JournalViewModel journal,
        AllocationViewModel allocation,
        SettingsViewModel settings)
    {
        _session = session;
        _auth = auth;
        _portfolio = portfolio;
        Login = login;
        Theme = theme;
        Notifications = notifications;
        Dialogs = dialogs;
        NavItems =
        [
            new NavItem("Dashboard", Icons.Dashboard, dashboard),
            new NavItem("Aset & Portofolio", Icons.Holdings, holdings),
            new NavItem("Transaksi", Icons.Transactions, transactions),
            new NavItem("Jurnal Investasi", Icons.Journal, journal),
            new NavItem("Kategori & Rebalancing", Icons.Allocation, allocation),
            new NavItem("Pengaturan", Icons.Settings, settings)
        ];

        _session.SessionChanged += (_, _) => OnSessionChanged();
    }

    public Task InitializeAsync() => Login.CheckDatabaseAsync();

    private void OnSessionChanged()
    {
        IsLoggedIn = _session.IsAuthenticated;
        OnPropertyChanged(nameof(UserFullName));
        OnPropertyChanged(nameof(UserRole));
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(IsReviewer));

        if (IsLoggedIn)
        {
            SelectedNav = NavItems[0];
            Notifications.Success($"Selamat datang, {UserFullName}.");
        }
        else
        {
            SelectedNav = null;
            CurrentPage = null;
        }
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null) return;
        CurrentPage = value.Page;
        _ = ActivateCurrentAsync();
    }

    private async Task ActivateCurrentAsync()
    {
        if (CurrentPage is null) return;
        await CurrentPage.ActivateAsync();
        await RefreshHeaderAsync();
    }

    /// <summary>Quick status summary in the header bar.</summary>
    public async Task RefreshHeaderAsync()
    {
        try
        {
            var (summary, _) = await _portfolio.GetOverviewAsync();
            HeaderNetWorth = Fmt.Money(summary.TotalNetWorth);
            HeaderPnLValue = summary.TotalUnrealizedPnL;
            HeaderPnL = $"{Fmt.Money(summary.TotalUnrealizedPnL, true)} ({Fmt.Percent(summary.TotalUnrealizedPnLPercentage, true)})";
        }
        catch
        {
            // Header is informational; page-level errors are already reported.
        }
    }

    [RelayCommand]
    private void ToggleTheme() => Theme.Toggle();

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (await Dialogs.ConfirmAsync("Logout", "Apakah Anda yakin ingin keluar dari sesi ini?", "Logout", danger: false))
            _auth.Logout();
    }
}
