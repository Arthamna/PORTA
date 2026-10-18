using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;

namespace Porta.Repositories;

public interface IJournalRepository
{
    Task<int> InsertAsync(SqlConnection conn, SqlTransaction tx, JournalEntry entry);
    Task<List<JournalViewDto>> GetAllAsync(string? emotionTag = null, int? assetId = null);
}

public sealed class JournalRepository(DatabaseHelper db) : IJournalRepository
{
    public async Task<int> InsertAsync(SqlConnection conn, SqlTransaction tx, JournalEntry e)
    {
        const string sql = """
            INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
            OUTPUT INSERTED.JournalId
            VALUES (@txId, @thesis, @tp, @sl, @emotion, @strategy)
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.AddParam("@txId", e.TransactionId);
        cmd.AddParam("@thesis", string.IsNullOrWhiteSpace(e.ThesisNote) ? null : e.ThesisNote.Trim());
        cmd.AddDecimal("@tp", e.TargetPrice, 18, 2);
        cmd.AddDecimal("@sl", e.StopLossPrice, 18, 2);
        cmd.AddParam("@emotion", string.IsNullOrWhiteSpace(e.EmotionTag) ? null : e.EmotionTag);
        cmd.AddParam("@strategy", string.IsNullOrWhiteSpace(e.StrategyTag) ? null : e.StrategyTag.Trim());
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<JournalViewDto>> GetAllAsync(string? emotionTag = null, int? assetId = null)
    {
        const string sql = """
            SELECT j.JournalId, j.TransactionId, t.TransactionDate, t.TransactionType, a.TickerSymbol, a.AssetName,
                   t.Units, t.PricePerUnit, a.CurrentMarketPrice, j.ThesisNote, j.TargetPrice, j.StopLossPrice,
                   j.EmotionTag, j.StrategyTag
            FROM dbo.JournalEntries j
            INNER JOIN dbo.Transactions t ON t.TransactionId = j.TransactionId
            INNER JOIN dbo.Assets a ON a.AssetId = t.AssetId
            WHERE (@emotion IS NULL OR j.EmotionTag = @emotion)
              AND (@assetId IS NULL OR t.AssetId = @assetId)
            ORDER BY t.TransactionDate DESC, t.TransactionId DESC
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.AddParam("@emotion", emotionTag);
        cmd.AddParam("@assetId", assetId);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<JournalViewDto>();
        while (await r.ReadAsync())
        {
            list.Add(new JournalViewDto
            {
                JournalId = r.GetInt32(0),
                TransactionId = r.GetInt32(1),
                TransactionDate = r.GetDateTime(2),
                TransactionType = Enum.Parse<TransactionType>(r.GetString(3)),
                TickerSymbol = r.GetString(4),
                AssetName = r.GetString(5),
                Units = r.GetDecimal(6),
                EntryPrice = r.GetDecimal(7),
                CurrentMarketPrice = r.GetDecimal(8),
                ThesisNote = r.GetNullableString("ThesisNote"),
                TargetPrice = r.GetNullableDecimal("TargetPrice"),
                StopLossPrice = r.GetNullableDecimal("StopLossPrice"),
                EmotionTag = r.GetNullableString("EmotionTag"),
                StrategyTag = r.GetNullableString("StrategyTag")
            });
        }
        return list;
    }
}
