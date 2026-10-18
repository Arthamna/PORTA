using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;

namespace Porta.Repositories;

public interface ICategoryRepository
{
    Task<List<AssetCategory>> GetAllAsync();
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<int> AddAsync(AssetCategory category);
    Task UpdateAsync(AssetCategory category);
    Task<bool> HasAssetsAsync(int categoryId);
    Task DeleteAsync(int categoryId);
    Task UpdateTargetsAsync(IEnumerable<(int CategoryId, decimal Target)> targets);
}

public sealed class CategoryRepository(DatabaseHelper db) : ICategoryRepository
{
    public async Task<List<AssetCategory>> GetAllAsync()
    {
        const string sql = """
            SELECT CategoryId, CategoryName, TargetPercentage, RiskLevel, Description
            FROM dbo.AssetCategories ORDER BY CategoryId
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<AssetCategory>();
        while (await r.ReadAsync())
        {
            list.Add(new AssetCategory
            {
                CategoryId = r.GetInt32(0),
                CategoryName = r.GetString(1),
                TargetPercentage = r.GetDecimal(2),
                RiskLevel = r.GetString(3),
                Description = r.GetNullableString("Description")
            });
        }
        return list;
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.AssetCategories
            WHERE LOWER(CategoryName) = LOWER(@name) AND (@excludeId IS NULL OR CategoryId <> @excludeId)
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@name", name);
        cmd.AddParam("@excludeId", excludeId);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    public async Task<int> AddAsync(AssetCategory c)
    {
        const string sql = """
            INSERT INTO dbo.AssetCategories (CategoryName, TargetPercentage, RiskLevel, Description)
            OUTPUT INSERTED.CategoryId
            VALUES (@name, @target, @risk, @desc)
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddCategoryParams(cmd, c);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(AssetCategory c)
    {
        const string sql = """
            UPDATE dbo.AssetCategories
            SET CategoryName = @name, TargetPercentage = @target, RiskLevel = @risk, Description = @desc
            WHERE CategoryId = @id
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddCategoryParams(cmd, c);
        cmd.AddParam("@id", c.CategoryId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasAssetsAsync(int categoryId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Assets WHERE CategoryId = @id", conn);
        cmd.AddParam("@id", categoryId);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    public async Task DeleteAsync(int categoryId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.AssetCategories WHERE CategoryId = @id", conn);
        cmd.AddParam("@id", categoryId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateTargetsAsync(IEnumerable<(int CategoryId, decimal Target)> targets)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            foreach (var (id, target) in targets)
            {
                await using var cmd = new SqlCommand(
                    "UPDATE dbo.AssetCategories SET TargetPercentage = @target WHERE CategoryId = @id", conn, tx);
                cmd.AddDecimal("@target", target, 5, 2);
                cmd.AddParam("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static void AddCategoryParams(SqlCommand cmd, AssetCategory c)
    {
        cmd.AddParam("@name", c.CategoryName.Trim());
        cmd.AddDecimal("@target", c.TargetPercentage, 5, 2);
        cmd.AddParam("@risk", c.RiskLevel);
        cmd.AddParam("@desc", string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim());
    }
}
