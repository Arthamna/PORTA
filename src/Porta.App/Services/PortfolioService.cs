using Porta.Models;
using Porta.Repositories;

namespace Porta.Services;

/// <summary>
/// Asset and category use-cases with validation (FR-05, FR-06, FR-12, FR-13) and authorization (FR-02),
/// plus read models for the Dashboard, Holdings and Allocation views.
/// </summary>
public sealed class PortfolioService(
    IAssetRepository assets,
    ICategoryRepository categories,
    ITransactionRepository transactions,
    PortfolioCalculationService calc,
    SessionService session)
{
    // ---------- Read models ----------

    public Task<List<AssetCategory>> GetCategoriesAsync() => categories.GetAllAsync();

    public Task<List<Asset>> GetAssetsAsync(string? search = null, int? categoryId = null) =>
        assets.GetAllAsync(search, categoryId);

    public async Task<List<HoldingDto>> GetHoldingsAsync(string? search = null, int? categoryId = null)
    {
        var list = await assets.GetAllAsync(search, categoryId);
        return list.Select(a => new HoldingDto
        {
            Asset = a,
            Metrics = calc.CalculateHolding(a.TotalUnits, a.AverageBuyPrice, a.CurrentMarketPrice)
        }).ToList();
    }

    public async Task<(PortfolioSummaryDto Summary, IReadOnlyList<AllocationItemDto> Allocation)> GetOverviewAsync()
    {
        var allAssets = await assets.GetAllAsync();
        var allCategories = await categories.GetAllAsync();
        var realized = await transactions.GetTotalRealizedPnLAsync();
        return (calc.CalculateSummary(allAssets, realized), calc.CalculateAllocation(allCategories, allAssets));
    }

    // ---------- Assets ----------

    public async Task SaveAssetAsync(Asset asset)
    {
        session.EnsureCanEdit();

        if (string.IsNullOrWhiteSpace(asset.TickerSymbol)) throw new ArgumentException("Kode ticker wajib diisi.");
        if (asset.TickerSymbol.Trim().Length > 20) throw new ArgumentException("Kode ticker maksimal 20 karakter.");
        if (string.IsNullOrWhiteSpace(asset.AssetName)) throw new ArgumentException("Nama aset wajib diisi.");
        if (string.IsNullOrWhiteSpace(asset.UnitType)) throw new ArgumentException("Satuan unit wajib diisi.");
        if (asset.CategoryId <= 0) throw new ArgumentException("Kategori aset wajib dipilih.");
        if (asset.CurrentMarketPrice <= 0) throw new ArgumentException("Harga pasar harus lebih besar dari 0.");

        var isNew = asset.AssetId == 0;
        if (await assets.TickerExistsAsync(asset.TickerSymbol, isNew ? null : asset.AssetId))
            throw new ArgumentException($"Ticker '{asset.TickerSymbol.Trim().ToUpperInvariant()}' sudah terdaftar.");

        if (isNew) asset.AssetId = await assets.AddAsync(asset);
        else await assets.UpdateAsync(asset);
    }

    public async Task UpdateMarketPriceAsync(int assetId, decimal price)
    {
        session.EnsureCanEdit();
        if (price <= 0) throw new ArgumentException("Harga pasar harus lebih besar dari 0.");
        await assets.UpdatePriceAsync(assetId, price);
    }

    public async Task DeleteAssetAsync(int assetId)
    {
        session.EnsureCanEdit();
        if (await assets.HasTransactionsAsync(assetId))
            throw new InvalidOperationException(
                "Aset tidak dapat dihapus karena masih memiliki riwayat transaksi. Hapus transaksinya terlebih dahulu.");
        await assets.DeleteAsync(assetId);
    }

    // ---------- Categories ----------

    public async Task SaveCategoryAsync(AssetCategory category)
    {
        session.EnsureCanEdit();
        if (string.IsNullOrWhiteSpace(category.CategoryName)) throw new ArgumentException("Nama kategori wajib diisi.");
        if (category.TargetPercentage is < 0 or > 100) throw new ArgumentException("Target persentase harus 0 - 100.");

        var isNew = category.CategoryId == 0;
        if (await categories.NameExistsAsync(category.CategoryName.Trim(), isNew ? null : category.CategoryId))
            throw new ArgumentException($"Kategori '{category.CategoryName.Trim()}' sudah ada.");

        if (isNew) category.CategoryId = await categories.AddAsync(category);
        else await categories.UpdateAsync(category);
    }

    public async Task DeleteCategoryAsync(int categoryId)
    {
        session.EnsureCanEdit();
        if (await categories.HasAssetsAsync(categoryId))
            throw new InvalidOperationException("Kategori tidak dapat dihapus karena masih memiliki aset.");
        await categories.DeleteAsync(categoryId);
    }

    /// <summary>FR-12 — targets must add up to exactly 100%.</summary>
    public async Task SaveTargetsAsync(IReadOnlyCollection<(int CategoryId, decimal Target)> targets)
    {
        session.EnsureCanEdit();
        if (targets.Any(t => t.Target is < 0 or > 100))
            throw new ArgumentException("Setiap target harus berada di antara 0 dan 100.");
        var total = targets.Sum(t => t.Target);
        if (total != 100m)
            throw new ArgumentException($"Total target alokasi harus 100%. Saat ini: {total:N2}%.");
        await categories.UpdateTargetsAsync(targets);
    }
}
