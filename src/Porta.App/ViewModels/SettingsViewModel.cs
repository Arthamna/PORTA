using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Data;
using Porta.Services;

namespace Porta.ViewModels;

/// <summary>Theme toggle, profile, database status and logout (FR-14).</summary>
public sealed partial class SettingsViewModel(
    ThemeService theme,
    DatabaseHelper db,
    AuthService auth,
    DialogService dialogs,
    NotificationService notifications,
    SessionService session) : PageViewModelBase(notifications, session)
{
    public override string Title => "Pengaturan";

    public ThemeService Theme { get; } = theme;

    public string FullName => Session.CurrentUser?.FullName ?? "";
    public string Username => Session.CurrentUser?.Username ?? "";
    public string Role => Session.CurrentUser?.Role.ToString() ?? "";
    public string AccessDescription => Session.CanEdit
        ? "Akses penuh (CRUD) ke seluruh modul, pengaturan target alokasi, dan manajemen data."
        : "Akses baca (read-only): Dashboard, Aset, Riwayat Transaksi, Jurnal, dan Alokasi.";

    [ObservableProperty] private string _databaseStatus = "Memeriksa...";
    [ObservableProperty] private bool _isDatabaseOk;

    protected override async Task LoadAsync()
    {
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(AccessDescription));
        await TestConnectionAsync();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        DatabaseStatus = "Memeriksa...";
        var (ok, message) = await db.TestConnectionAsync();
        IsDatabaseOk = ok;
        DatabaseStatus = message;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (await dialogs.ConfirmAsync("Logout", "Apakah Anda yakin ingin keluar dari sesi ini?", "Logout", danger: false))
            auth.Logout();
    }
}
