namespace Porta.Models;

public enum UserRole
{
    Investor,
    Reviewer
}

public enum TransactionType
{
    BUY,
    SELL,
    DIVIDEND
}

public enum RebalanceStatus
{
    Ideal,
    Overweight,
    Underweight
}

public sealed class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal TargetPercentage { get; set; }
    public string RiskLevel { get; set; } = "Sedang";
    public string? Description { get; set; }

    public override string ToString() => CategoryName;
}

public sealed class Asset
{
    public int AssetId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string TickerSymbol { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string UnitType { get; set; } = "";
    public decimal TotalUnits { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentMarketPrice { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string Display => $"{TickerSymbol} — {AssetName}";
    public override string ToString() => Display;
}

public sealed class Transaction
{
    public int TransactionId { get; set; }
    public int AssetId { get; set; }
    public int UserId { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Units { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? RealizedPnL { get; set; }
    public DateTime TransactionDate { get; set; }

    // Joined, read-only display fields
    public string TickerSymbol { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string Username { get; set; } = "";
    public bool HasJournal { get; set; }
}

public sealed class JournalEntry
{
    public int JournalId { get; set; }
    public int TransactionId { get; set; }
    public string? ThesisNote { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public string? EmotionTag { get; set; }
    public string? StrategyTag { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(ThesisNote) && TargetPrice is null && StopLossPrice is null &&
        string.IsNullOrWhiteSpace(EmotionTag) && string.IsNullOrWhiteSpace(StrategyTag);
}

public static class EmotionTags
{
    public static readonly string[] All = ["Rasional", "Riset Mendalam", "FOMO", "Panik", "Spekulasi"];
}
