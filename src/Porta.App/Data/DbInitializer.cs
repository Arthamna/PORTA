using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Porta.Data;

/// <summary>
/// First-run convenience: if PORTA_DB does not exist yet, runs database/01_schema.sql and 02_seed.sql
/// (copied next to the executable). Scripts are idempotent, so running them manually is also fine.
/// </summary>
public sealed partial class DbInitializer(AppSettings settings)
{
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    public async Task EnsureDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(settings.ConnectionString);
        var dbName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "PORTA_DB" : builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        await using (var check = new SqlCommand("SELECT DB_ID(@name)", conn))
        {
            check.Parameters.AddWithValue("@name", dbName);
            if (await check.ExecuteScalarAsync() is not DBNull and not null)
                return;
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "database");
        foreach (var file in Directory.GetFiles(dir, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
        {
            var script = await File.ReadAllTextAsync(file);
            foreach (var batch in GoSeparator().Split(script))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
