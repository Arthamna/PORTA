using Microsoft.Data.SqlClient;

namespace Porta.Data;

/// <summary>Creates and opens SQL Server connections for the repositories.</summary>
public sealed class DatabaseHelper(AppSettings settings)
{
    public string ConnectionString { get; } = settings.ConnectionString;

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>Returns (ok, message) — used by Settings view and startup checks.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("SELECT @@VERSION", conn);
            var version = (string?)await cmd.ExecuteScalarAsync() ?? "";
            var firstLine = version.Split('\n')[0].Trim();
            return (true, $"Terhubung ke {conn.DataSource} / {conn.Database} — {firstLine}");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal terhubung: {ex.Message}");
        }
    }
}

/// <summary>Small helpers for reading nullable columns and building parameters.</summary>
internal static class SqlExtensions
{
    public static SqlParameter AddParam(this SqlCommand cmd, string name, object? value)
        => cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

    public static SqlParameter AddDecimal(this SqlCommand cmd, string name, decimal? value, byte precision, byte scale)
    {
        var p = cmd.Parameters.Add(name, System.Data.SqlDbType.Decimal);
        p.Precision = precision;
        p.Scale = scale;
        p.Value = value.HasValue ? value.Value : DBNull.Value;
        return p;
    }

    public static string? GetNullableString(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static decimal? GetNullableDecimal(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDecimal(i);
    }
}
