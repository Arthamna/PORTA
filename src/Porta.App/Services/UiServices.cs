using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Porta.ViewModels;

namespace Porta.Services;

public enum ToastKind { Success, Error, Info }

public sealed record Toast(ToastKind Kind, string Message)
{
    public string Icon => Kind switch { ToastKind.Success => "✓", ToastKind.Error => "✕", _ => "i" };
}

/// <summary>Success / failure toasts shown at the top-right of the shell (FR-15).</summary>
public sealed class NotificationService
{
    public ObservableCollection<Toast> Toasts { get; } = [];

    public void Success(string message) => Show(ToastKind.Success, message);
    public void Error(string message) => Show(ToastKind.Error, message);
    public void Info(string message) => Show(ToastKind.Info, message);

    private void Show(ToastKind kind, string message)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var toast = new Toast(kind, message);
            Toasts.Add(toast);
            await Task.Delay(kind == ToastKind.Error ? 6000 : 3500);
            Toasts.Remove(toast);
        });
    }
}

/// <summary>Shows modal dialogs as an overlay inside the main window and awaits their result.</summary>
public sealed partial class DialogService : ObservableObject
{
    [ObservableProperty] private DialogViewModelBase? _currentDialog;

    public async Task<bool> ShowAsync(DialogViewModelBase dialog)
    {
        CurrentDialog = dialog;
        try
        {
            return await dialog.Completion;
        }
        finally
        {
            if (ReferenceEquals(CurrentDialog, dialog)) CurrentDialog = null;
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Hapus", bool danger = true) =>
        ShowAsync(new ConfirmDialogViewModel(title, message, confirmText, danger));
}

/// <summary>Dark / Light mode switch (FR-14).</summary>
public sealed partial class ThemeService : ObservableObject
{
    [ObservableProperty] private bool _isDark;

    partial void OnIsDarkChanged(bool value)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void Toggle() => IsDark = !IsDark;
}
