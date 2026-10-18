-- =============================================================
-- PORTA_DB - Seed data (PRD section 15, Fase 2)
-- Runs only on an empty database.
-- Demo accounts:  investor / investor123  (Investor)
--                 reviewer / reviewer123  (Reviewer)
-- Holdings in Assets are pre-computed from the transactions below
-- using the formulas in PRD section 8.
-- =============================================================
USE PORTA_DB;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Role, IsActive) VALUES
    (N'investor', N'$2a$11$NsNct.r2wjvrDaE2cOs7x.K8D6JsF5TGOcOXDj4rehtyEgBjvU4va', N'Investor Demo', N'Investor', 1),
    (N'reviewer', N'$2a$11$qyoBGQpVbV8ToB837U9Be.Ese8niYFysA9urSEOsLeq6wztdQ/bla', N'Mentor Reviewer', N'Reviewer', 1);

    INSERT INTO dbo.AssetCategories (CategoryName, TargetPercentage, RiskLevel, Description) VALUES
    (N'Saham',               40.00, N'Tinggi', N'Saham Bursa Efek Indonesia'),
    (N'Kripto',              15.00, N'Tinggi', N'Aset kripto (BTC, ETH, dll.)'),
    (N'Reksa Dana/Obligasi', 25.00, N'Sedang', N'Reksa dana dan obligasi'),
    (N'Emas',                10.00, N'Rendah', N'Logam mulia / emas batangan'),
    (N'Kas',                 10.00, N'Rendah', N'Kas, tabungan, dan deposito');

    DECLARE @Saham INT = (SELECT CategoryId FROM dbo.AssetCategories WHERE CategoryName = N'Saham');
    DECLARE @Kripto INT = (SELECT CategoryId FROM dbo.AssetCategories WHERE CategoryName = N'Kripto');
    DECLARE @RD INT = (SELECT CategoryId FROM dbo.AssetCategories WHERE CategoryName = N'Reksa Dana/Obligasi');
    DECLARE @Emas INT = (SELECT CategoryId FROM dbo.AssetCategories WHERE CategoryName = N'Emas');
    DECLARE @Kas INT = (SELECT CategoryId FROM dbo.AssetCategories WHERE CategoryName = N'Kas');

    INSERT INTO dbo.Assets (CategoryId, TickerSymbol, AssetName, UnitType, TotalUnits, AverageBuyPrice, CurrentMarketPrice) VALUES
    (@Saham,  N'BBCA',    N'Bank Central Asia Tbk',     N'Lembar', 1500,     9747.9333,  10250),
    (@Saham,  N'TLKM',    N'Telkom Indonesia Tbk',      N'Lembar', 1500,     3100,       3050),
    (@Kripto, N'BTC',     N'Bitcoin',                   N'Koin',   0.02,     1010000000, 1050000000),
    (@Kripto, N'ETH',     N'Ethereum',                  N'Koin',   0.3,      40000000,   38500000),
    (@RD,     N'RDPU',    N'Reksa Dana Pasar Uang',     N'Unit',   10000,    1500,       1525),
    (@Emas,   N'ANTAM',   N'Emas Antam',                N'Gram',   20,       1350000,    1420000),
    (@Kas,    N'KAS-BCA', N'Tabungan / Dana Darurat',   N'IDR',    15000000, 1,          1);

    DECLARE @User INT = (SELECT UserId FROM dbo.Users WHERE Username = N'investor');
    DECLARE @Tx INT;

    -- BBCA: BUY 1000 @ 9.800 + fee 14.700
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'BBCA'), @User, N'BUY', 1000, 9800, 14700, 9814700, NULL, '2026-06-02T09:15:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Fundamental kuat, ROE konsisten > 20%, valuasi wajar setelah koreksi.', 11000, 9200, N'Rasional', N'Value Investing');

    -- BBCA: BUY 500 @ 9.600 + fee 7.200 (DCA)
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'BBCA'), @User, N'BUY', 500, 9600, 7200, 4807200, NULL, '2026-07-15T10:00:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Dollar cost averaging sesuai rencana bulanan.', 11000, 9200, N'Riset Mendalam', N'DCA');

    -- TLKM: BUY 2000 @ 3.100
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'TLKM'), @User, N'BUY', 2000, 3100, 0, 6200000, NULL, '2026-06-10T09:30:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Dividen yield tinggi, bisnis data center bertumbuh.', 3500, 2900, N'Rasional', N'Dividend');

    -- BTC: BUY 0.02 @ 1.000.000.000 + fee 200.000
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'BTC'), @User, N'BUY', 0.02, 1000000000, 200000, 20200000, NULL, '2026-06-20T21:00:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Harga naik cepat dan ramai di media sosial, takut ketinggalan.', 1200000000, 900000000, N'FOMO', N'Momentum');

    -- RDPU: BUY 10.000 @ 1.500
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'RDPU'), @User, N'BUY', 10000, 1500, 0, 15000000, NULL, '2026-06-25T11:00:00');

    -- ANTAM: BUY 20 gram @ 1.350.000
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'ANTAM'), @User, N'BUY', 20, 1350000, 0, 27000000, NULL, '2026-07-01T13:00:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Lindung nilai terhadap inflasi dan ketidakpastian global.', 1600000, 1250000, N'Riset Mendalam', N'Hedging');

    -- KAS-BCA: dana darurat 15.000.000
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'KAS-BCA'), @User, N'BUY', 15000000, 1, 0, 15000000, NULL, '2026-07-02T08:00:00');

    -- ETH: BUY 0.3 @ 40.000.000
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'ETH'), @User, N'BUY', 0.3, 40000000, 0, 12000000, NULL, '2026-08-05T20:00:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Spekulasi menjelang upgrade jaringan.', 50000000, 35000000, N'Spekulasi', N'Event Driven');

    -- TLKM: SELL 500 @ 3.400 -> Realized P&L = 1.700.000 - (500 x 3.100) = 150.000
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'TLKM'), @User, N'SELL', 500, 3400, 0, 1700000, 150000, '2026-08-20T14:30:00');
    SET @Tx = SCOPE_IDENTITY();
    INSERT INTO dbo.JournalEntries (TransactionId, ThesisNote, TargetPrice, StopLossPrice, EmotionTag, StrategyTag)
    VALUES (@Tx, N'Ambil sebagian profit, harga mendekati resisten.', NULL, NULL, N'Rasional', N'Take Profit');

    -- BBCA: DIVIDEND 1.500 lembar x Rp 270
    INSERT INTO dbo.Transactions (AssetId, UserId, TransactionType, Units, PricePerUnit, Fee, TotalAmount, RealizedPnL, TransactionDate)
    VALUES ((SELECT AssetId FROM dbo.Assets WHERE TickerSymbol = N'BBCA'), @User, N'DIVIDEND', 1500, 270, 0, 405000, 405000, '2026-09-10T09:00:00');
END
GO
