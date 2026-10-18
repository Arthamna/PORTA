using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using Porta.Services;

namespace Porta.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;

    /// <summary>Converts exceptions into user-friendly Indonesian messages (FR-15).</summary>
    public static string FriendlyMessage(Exception ex) => ex switch
    {
        SqlException { Number: 2627 or 2601 } => "Data duplikat: nilai yang sama sudah tersimpan di database.",
        SqlException { Number: 547 } => "Operasi dibatalkan karena data masih direferensikan oleh data lain.",
        SqlException sql => $"Terjadi masalah koneksi/query database: {sql.Message}",
        InvalidOperationException { InnerException: SqlException inner } => FriendlyMessage(inner),
        _ => ex.Message
    };
}

/// <summary>A page shown in the main content area, reached from the sidebar.</summary>
public abstract partial class PageViewModelBase(NotificationService notifications, SessionService session) : ViewModelBase
{
    protected NotificationService Notifications { get; } = notifications;
    protected SessionService Session { get; } = session;

    public abstract string Title { get; }

    public bool CanEdit => Session.CanEdit;
    public bool IsReadOnly => !Session.CanEdit;

    public async Task ActivateAsync()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsReadOnly));
        await TryRunAsync(LoadAsync);
    }

    protected abstract Task LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => TryRunAsync(LoadAsync);

    /// <summary>Runs an async action with a busy indicator; failures become error toasts instead of crashes.</summary>
    protected async Task<bool> TryRunAsync(Func<Task> action, string? successMessage = null)
    {
        try
        {
            IsBusy = true;
            await action();
            if (successMessage is not null) Notifications.Success(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            Notifications.Error(FriendlyMessage(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Base class for overlay dialogs shown via <see cref="DialogService"/>.</summary>
public abstract partial class DialogViewModelBase : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> Completion => _tcs.Task;

    public abstract string DialogTitle { get; }

    [ObservableProperty] private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    protected void Close(bool result) => _tcs.TrySetResult(result);

    [RelayCommand]
    private void Cancel() => Close(false);

    /// <summary>Runs a save action; validation/database errors are shown inline in the dialog.</summary>
    protected async Task SubmitAsync(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            IsBusy = true;
            await action();
            Close(true);
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

public sealed partial class ConfirmDialogViewModel(string title, string message, string confirmText, bool danger)
    : DialogViewModelBase
{
    public override string DialogTitle { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public bool IsDanger { get; } = danger;

    [RelayCommand]
    private void Confirm() => Close(true);
}
