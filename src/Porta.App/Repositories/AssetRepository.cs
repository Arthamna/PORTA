using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;

namespace Porta.Repositories;

public interface IAssetRepository
{
    Task<List<Asset>> GetAllAsync(string? search = null, int? categoryId = null);
    Task<Asset?> GetByIdAsync(int assetId);
    Task<Asset?> GetForUpdateAsync(SqlConnection conn, SqlTransaction tx, int assetId);
    Task<bool> TickerExistsAsync(string ticker, int? excludeId = null);
    Task<int> AddAsync(Asset asset);
    Task UpdateAsync(Asset asset);
    Task UpdatePriceAsync(int assetId, decimal price);
    Task UpdatePositionAsync(SqlConnection conn, SqlTransaction tx, int assetId, decimal totalUnits, decimal avgBuyPrice);
    Task<bool> HasTransactionsAsync(int assetId);
    Task DeleteAsync(int assetId);
}

public sealed class AssetRepository(DatabaseHelper db) : IAssetRepository
{
    private const string SelectColumns = """
        SELECT a.AssetId, a.CategoryId, c.CategoryName, a.TickerSymbol, a.AssetName, a.UnitType,
               a.TotalUnits, a.AverageBuyPrice, a.CurrentMarketPrice, a.UpdatedAt
        FROM dbo.Assets a
        INNER JOIN dbo.AssetCategories c ON c.CategoryId = a.CategoryId
        """;

    public async Task<List<Asset>> GetAllAsync(string? search = null, int? categoryId = null)
    {
        // FR-07: case-insensitive search on ticker or name, optional category filter.
        var sql = SelectColumns + """

            WHERE (@search IS NULL
                   OR LOWER(a.TickerSymbol) LIKE LOWER(@search) ESCAPE '\'
                   OR LOWER(a.AssetName) LIKE LOWER(@search) ESCAPE '\')
              AND (@categoryId IS NULL OR a.CategoryId = @categoryId)
            ORDER BY c.CategoryId, a.TickerSymbol
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@search", string.IsNullOrWhiteSpace(search) ? null : $"%{EscapeLike(search.Trim())}%");
        cmd.AddParam("@categoryId", categoryId);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Asset>();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<Asset?> GetByIdAsync(int assetId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(SelectColumns + " WHERE a.AssetId = @id", conn);
        cmd.AddParam("@id", assetId);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<Asset?> GetForUpdateAsync(SqlConnection conn, SqlTransaction tx, int assetId)
    {
        const string sql = """
            SELECT a.AssetId, a.CategoryId, c.CategoryName, a.TickerSymbol, a.AssetName, a.UnitType,
                   a.TotalUnits, a.AverageBuyPrice, a.CurrentMarketPrice, a.UpdatedAt
            FROM dbo.Assets a WITH (UPDLOCK, ROWLOCK)
            INNER JOIN dbo.AssetCategories c ON c.CategoryId = a.CategoryId
            WHERE a.AssetId = @id
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.AddParam("@id", assetId);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<bool> TickerExistsAsync(string ticker, int? excludeId = null)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.Assets
            WHERE UPPER(TickerSymbol) = UPPER(@ticker) AND (@excludeId IS NULL OR AssetId <> @excludeId)
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@ticker", ticker.Trim());
        cmd.AddParam("@excludeId", excludeId);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    public async Task<int> AddAsync(Asset a)
    {
        const string sql = """
            INSERT INTO dbo.Assets (CategoryId, TickerSymbol, AssetName, UnitType, TotalUnits, AverageBuyPrice, CurrentMarketPrice, UpdatedAt)
            OUTPUT INSERTED.AssetId
            VALUES (@categoryId, @ticker, @name, @unitType, 0, 0, @price, SYSDATETIME())
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@categoryId", a.CategoryId);
        cmd.AddParam("@ticker", a.TickerSymbol.Trim().ToUpperInvariant());
        cmd.AddParam("@name", a.AssetName.Trim());
        cmd.AddParam("@unitType", a.UnitType.Trim());
        cmd.AddDecimal("@price", a.CurrentMarketPrice, 18, 2);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(Asset a)
    {
        // Units and average price are owned by the transaction engine and are never edited directly.
        const string sql = """
            UPDATE dbo.Assets
            SET CategoryId = @categoryId, TickerSymbol = @ticker, AssetName = @name, UnitType = @unitType,
                CurrentMarketPrice = @price, UpdatedAt = SYSDATETIME()
            WHERE AssetId = @id
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@categoryId", a.CategoryId);
        cmd.AddParam("@ticker", a.TickerSymbol.Trim().ToUpperInvariant());
        cmd.AddParam("@name", a.AssetName.Trim());
        cmd.AddParam("@unitType", a.UnitType.Trim());
        cmd.AddDecimal("@price", a.CurrentMarketPrice, 18, 2);
        cmd.AddParam("@id", a.AssetId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePriceAsync(int assetId, decimal price)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(
            "UPDATE dbo.Assets SET CurrentMarketPrice = @price, UpdatedAt = SYSDATETIME() WHERE AssetId = @id", conn);
        cmd.AddDecimal("@price", price, 18, 2);
        cmd.AddParam("@id", assetId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePositionAsync(SqlConnection conn, SqlTransaction tx, int assetId, decimal totalUnits, decimal avgBuyPrice)
    {
        await using var cmd = new SqlCommand(
            "UPDATE dbo.Assets SET TotalUnits = @units, AverageBuyPrice = @avg, UpdatedAt = SYSDATETIME() WHERE AssetId = @id",
            conn, tx);
        cmd.AddDecimal("@units", totalUnits, 18, 6);
        cmd.AddDecimal("@avg", avgBuyPrice, 18, 4);
        cmd.AddParam("@id", assetId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasTransactionsAsync(int assetId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Transactions WHERE AssetId = @id", conn);
        cmd.AddParam("@id", assetId);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    public async Task DeleteAsync(int assetId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.Assets WHERE AssetId = @id", conn);
        cmd.AddParam("@id", assetId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string EscapeLike(string s) =>
        s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");

    private static Asset Map(SqlDataReader r) => new()
    {
        AssetId = r.GetInt32(0),
        CategoryId = r.GetInt32(1),
        CategoryName = r.GetString(2),
        TickerSymbol = r.GetString(3),
        AssetName = r.GetString(4),
        UnitType = r.GetString(5),
        TotalUnits = r.GetDecimal(6),
        AverageBuyPrice = r.GetDecimal(7),
        CurrentMarketPrice = r.GetDecimal(8),
        UpdatedAt = r.GetDateTime(9)
    };
}
