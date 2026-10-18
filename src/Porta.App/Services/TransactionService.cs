using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;
using Porta.Repositories;

namespace Porta.Services;

public sealed record TransactionInput(
    int AssetId,
    TransactionType Type,
    DateTime Date,
    decimal Units,
    decimal PricePerUnit,
    decimal Fee);

/// <summary>
/// Records and deletes transactions atomically (NFR-03): the Transactions insert, the JournalEntries insert
/// and the Assets position update all run inside one SqlTransaction.
/// </summary>
public sealed class TransactionService(
    DatabaseHelper db,
    IAssetRepository assets,
    ITransactionRepository transactions,
    IJournalRepository journals,
    PortfolioCalculationService calc,
    SessionService session)
{
    /// <summary>FR-13 business validation; returns an error message or null.</summary>
    public static string? Validate(TransactionInput input)
    {
        if (input.AssetId <= 0) return "Aset wajib dipilih.";
        if (input.Units <= 0) return "Jumlah unit harus lebih besar dari 0.";
        if (input.PricePerUnit <= 0)
            return input.Type == TransactionType.DIVIDEND
                ? "Dividen per unit harus lebih besar dari 0."
                : "Harga per unit harus lebih besar dari 0.";
        if (input.Fee < 0) return "Biaya transaksi tidak boleh negatif.";
        if (input.Date > DateTime.Now.AddDays(1)) return "Tanggal transaksi tidak boleh di masa depan.";
        return null;
    }

    public async Task<int> RecordAsync(TransactionInput input, JournalEntry? journal)
    {
        session.EnsureCanEdit();
        if (Validate(input) is { } error) throw new ArgumentException(error);

        await using var conn = await db.OpenConnectionAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            var asset = await assets.GetForUpdateAsync(conn, tx, input.AssetId)
                        ?? throw new InvalidOperationException("Aset tidak ditemukan.");

            // Compute the new row using the current position (PRD section 8).
            var result = calc.Apply(input.Type, asset.TotalUnits, asset.AverageBuyPrice, input.Units, input.PricePerUnit, input.Fee);

            var newId = await transactions.InsertAsync(conn, tx, new Transaction
            {
                AssetId = input.AssetId,
                UserId = session.CurrentUser!.UserId,
                TransactionType = input.Type,
                Units = input.Units,
                PricePerUnit = input.PricePerUnit,
                Fee = input.Fee,
                TotalAmount = result.TotalAmount,
                RealizedPnL = result.RealizedPnL,
                TransactionDate = input.Date
            });

            if (journal is { IsEmpty: false })
            {
                journal.TransactionId = newId;
                await journals.InsertAsync(conn, tx, journal);
            }

            // Re-derive the position from full history so back-dated entries are also handled correctly.
            await RebuildPositionAsync(conn, tx, input.AssetId);

            await tx.CommitAsync();
            return newId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(int transactionId)
    {
        session.EnsureCanEdit();

        await using var conn = await db.OpenConnectionAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            var existing = await transactions.GetByIdAsync(conn, tx, transactionId)
                           ?? throw new InvalidOperationException("Transaksi tidak ditemukan.");
            await assets.GetForUpdateAsync(conn, tx, existing.AssetId);
            await transactions.DeleteAsync(conn, tx, transactionId);
            await RebuildPositionAsync(conn, tx, existing.AssetId);
            await tx.CommitAsync();
        }
        catch (InsufficientUnitsException)
        {
            await tx.RollbackAsync();
            throw new InvalidOperationException(
                "Transaksi tidak dapat dihapus karena akan membuat transaksi SELL setelahnya melebihi unit yang dimiliki.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task RebuildPositionAsync(SqlConnection conn, SqlTransaction tx, int assetId)
    {
        var history = await transactions.GetByAssetOrderedAsync(conn, tx, assetId);
        var (final, steps) = calc.Replay(history);

        for (var i = 0; i < history.Count; i++)
        {
            var t = history[i];
            var s = steps[i];
            if (t.TotalAmount != s.TotalAmount || t.RealizedPnL != s.RealizedPnL)
                await transactions.UpdateComputedAsync(conn, tx, t.TransactionId, s.TotalAmount, s.RealizedPnL);
        }

        await assets.UpdatePositionAsync(conn, tx, assetId, final.TotalUnits, final.AverageBuyPrice);
    }
}
