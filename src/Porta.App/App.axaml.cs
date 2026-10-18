using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Porta.Data;
using Porta.Repositories;
using Porta.Services;
using Porta.ViewModels;
using Porta.Views;

namespace Porta;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = main };
            _ = main.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Data
        services.AddSingleton(AppSettings.Load());
        services.AddSingleton<DatabaseHelper>();
        services.AddSingleton<DbInitializer>();

        // Repositories
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IAssetRepository, AssetRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IJournalRepository, JournalRepository>();

        // Services
        services.AddSingleton<PortfolioCalculationService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<PortfolioService>();
        services.AddSingleton<TransactionService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<HoldingsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<JournalViewModel>();
        services.AddSingleton<AllocationViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
