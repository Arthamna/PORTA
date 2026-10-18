using System.Text.Json;

namespace Porta.Data;

/// <summary>
/// Loads appsettings.json from the output directory.
/// The connection string can be overridden with the PORTA_CONNECTION_STRING environment variable.
/// </summary>
public sealed class AppSettings
{
    public string ConnectionString { get; init; } = "";
    public bool AutoInitializeDatabase { get; init; } = true;

    public static AppSettings Load()
    {
        string connectionString = "";
        bool autoInit = true;

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("PortaDb", out var db))
                connectionString = db.GetString() ?? "";
            if (root.TryGetProperty("Database", out var dbSection) && dbSection.TryGetProperty("AutoInitialize", out var ai))
                autoInit = ai.GetBoolean();
        }

        var env = Environment.GetEnvironmentVariable("PORTA_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(env))
            connectionString = env;

        return new AppSettings { ConnectionString = connectionString, AutoInitializeDatabase = autoInit };
    }
}
