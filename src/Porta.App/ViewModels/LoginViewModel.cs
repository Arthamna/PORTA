using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Data;
using Porta.Services;

namespace Porta.ViewModels;

public sealed partial class LoginViewModel(AuthService auth, DatabaseHelper db, DbInitializer initializer, AppSettings settings)
    : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = "";

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _databaseError;
    [ObservableProperty] private bool _isDatabaseReady;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasDatabaseError => !string.IsNullOrEmpty(DatabaseError);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnDatabaseErrorChanged(string? value) => OnPropertyChanged(nameof(HasDatabaseError));
    partial void OnIsDatabaseReadyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    /// <summary>Checks the database on startup; never throws so the app cannot crash here (FR-15).</summary>
    [RelayCommand]
    public async Task CheckDatabaseAsync()
    {
        IsBusy = true;
        DatabaseError = null;
        try
        {
            if (settings.AutoInitializeDatabase)
                await initializer.EnsureDatabaseAsync();

            var (ok, message) = await db.TestConnectionAsync();
            IsDatabaseReady = ok;
            if (!ok) DatabaseError = message;
        }
        catch (Exception ex)
        {
            IsDatabaseReady = false;
            DatabaseError = "Tidak dapat terhubung ke SQL Server. Pastikan container database berjalan " +
                            $"(docker compose up -d) dan connection string benar.\n\nDetail: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => IsDatabaseReady && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var (success, error) = await auth.LoginAsync(Username, Password);
            if (success)
            {
                Password = "";
            }
            else
            {
                ErrorMessage = error;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = FriendlyMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
