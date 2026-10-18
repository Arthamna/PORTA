using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;

namespace Porta.Repositories;

public sealed record TransactionFilter(DateTime? From = null, DateTime? To = null, TransactionType? Type = null, int? AssetId = null);

public interface ITransactionRepository
{
    Task<List<Transaction>> GetAsync(TransactionFilter filter);
    Task<List<Transaction>> GetRecentAsync(int count);
    Task<decimal> GetTotalRealizedPnLAsync();
    Task<Transaction?> GetByIdAsync(SqlConnection conn, SqlTransaction tx, int transactionId);
    Task<List<Transaction>> GetByAssetOrderedAsync(SqlConnection conn, SqlTransaction tx, int assetId);
    Task<int> InsertAsync(SqlConnection conn, SqlTransaction tx, Transaction t);
    Task UpdateComputedAsync(SqlConnection conn, SqlTransaction tx, int transactionId, decimal totalAmount, decimal? realizedPnL);
    Task DeleteAsync(SqlConnection conn, SqlTransaction tx, int transactionId);
}

public sealed class TransactionRepository(DatabaseHelper db) : ITransactionRepository
{
    private const string SelectColumns = """
        SELECT t.TransactionId, t.AssetId, t.UserId, t.TransactionType, t.Units, t.PricePerUnit, t.Fee,
               t.TotalAmount, t.RealizedPnL, t.TransactionDate, a.TickerSymbol, a.AssetName, u.Username,
               CASE WHEN j.JournalId IS NULL THEN 0 ELSE 1 END AS HasJournal
        FROM dbo.Transactions t
        INNER JOIN dbo.Assets a ON a.AssetId = t.AssetId
        INNER JOIN dbo.Users u ON u.UserId = t.UserId
        LEFT JOIN dbo.JournalEntries j ON j.TransactionId = t.TransactionId
        """;

    public async Task<List<Transaction>> GetAsync(TransactionFilter f)
    {
        var sql = SelectColumns + """

            WHERE (@from IS NULL OR t.TransactionDate >= @from)
              AND (@to IS NULL OR t.TransactionDate < @to)
              AND (@type IS NULL OR t.TransactionType = @type)
              AND (@assetId IS NULL OR t.AssetId = @assetId)
            ORDER BY t.TransactionDate DESC, t.TransactionId DESC
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@from", f.From?.Date);
        cmd.AddParam("@to", f.To?.Date.AddDays(1));
        cmd.AddParam("@type", f.Type?.ToString());
        cmd.AddParam("@assetId", f.AssetId);
        return await ReadAllAsync(cmd);
    }

    public async Task<List<Transaction>> GetRecentAsync(int count)
    {
        var sql = SelectColumns.Replace("SELECT t.", "SELECT TOP (@count) t.") +
                  " ORDER BY t.TransactionDate DESC, t.TransactionId DESC";
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@count", count);
        return await ReadAllAsync(cmd);
    }

    public async Task<decimal> GetTotalRealizedPnLAsync()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand("SELECT ISNULL(SUM(RealizedPnL), 0) FROM dbo.Transactions", conn);
        return (decimal)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<Transaction?> GetByIdAsync(SqlConnection conn, SqlTransaction tx, int transactionId)
    {
        await using var cmd = new SqlCommand(SelectColumns + " WHERE t.TransactionId = @id", conn, tx);
        cmd.AddParam("@id", transactionId);
        var list = await ReadAllAsync(cmd);
        return list.FirstOrDefault();
    }

    public async Task<List<Transaction>> GetByAssetOrderedAsync(SqlConnection conn, SqlTransaction tx, int assetId)
    {
        await using var cmd = new SqlCommand(
            SelectColumns + " WHERE t.AssetId = @assetId ORDER BY t.TransactionDate, t.TransactionId", conn, tx);
        cmd.AddParam("@assetId", assetId);
        return await ReadAllAsync(cmd);
    }

    public async Task<int> InsertAsync(SqlConnection conn, SqlTransaction tx, Transaction t)
    {
        const string sql = """
            INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
            OUTPUT INSERTED.TransactionId
            VALUES (@assetId, @userId, @type, @units, @price, @fee, @total, @realized, @date)
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.AddParam("@assetId", t.AssetId);
        cmd.AddParam("@userId", t.UserId);
        cmd.AddParam("@type", t.TransactionType.ToString());
        cmd.AddDecimal("@units", t.Units, 18, 6);
        cmd.AddDecimal("@price", t.PricePerUnit, 18, 2);
        cmd.AddDecimal("@fee", t.Fee, 18, 2);
        cmd.AddDecimal("@total", t.TotalAmount, 18, 2);
        cmd.AddDecimal("@realized", t.RealizedPnL, 18, 2);
        cmd.Parameters.Add("@date", System.Data.SqlDbType.DateTime2).Value = t.TransactionDate;
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateComputedAsync(SqlConnection conn, SqlTransaction tx, int transactionId, decimal totalAmount, decimal? realizedPnL)
    {
        await using var cmd = new SqlCommand(
            "UPDATE dbo.Transactions SET TotalAmount = @total, RealizedPnL = @realized WHERE TransactionId = @id", conn, tx);
        cmd.AddDecimal("@total", totalAmount, 18, 2);
        cmd.AddDecimal("@realized", realizedPnL, 18, 2);
        cmd.AddParam("@id", transactionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, int transactionId)
    {
        // JournalEntries rows are removed by ON DELETE CASCADE.
        await using var cmd = new SqlCommand("DELETE FROM dbo.Transactions WHERE TransactionId = @id", conn, tx);
        cmd.AddParam("@id", transactionId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<Transaction>> ReadAllAsync(SqlCommand cmd)
    {
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Transaction>();
        while (await r.ReadAsync())
        {
            list.Add(new Transaction
            {
                TransactionId = r.GetInt32(0),
                AssetId = r.GetInt32(1),
                UserId = r.GetInt32(2),
                TransactionType = Enum.Parse<TransactionType>(r.GetString(3)),
                Units = r.GetDecimal(4),
                PricePerUnit = r.GetDecimal(5),
                Fee = r.GetDecimal(6),
                TotalAmount = r.GetDecimal(7),
                RealizedPnL = r.GetNullableDecimal("RealizedPnL"),
                TransactionDate = r.GetDateTime(9),
                TickerSymbol = r.GetString(10),
                AssetName = r.GetString(11),
                Username = r.GetString(12),
                HasJournal = r.GetInt32(13) == 1
            });
        }
        return list;
    }
}
