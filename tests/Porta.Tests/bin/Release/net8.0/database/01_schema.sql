-- =============================================================
-- PORTA_DB - Schema (PRD section 12)
-- Idempotent: safe to run more than once.
-- =============================================================
IF DB_ID(N'PORTA_DB') IS NULL
    CREATE DATABASE PORTA_DB;
GO

USE PORTA_DB;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username      NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash  NVARCHAR(100) NOT NULL,
    FullName      NVARCHAR(100) NOT NULL,
    Role          NVARCHAR(10)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN (N'Investor', N'Reviewer')),
    IsActive      BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.AssetCategories', N'U') IS NULL
CREATE TABLE dbo.AssetCategories (
    CategoryId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetCategories PRIMARY KEY,
    CategoryName      NVARCHAR(50)  NOT NULL CONSTRAINT UQ_AssetCategories_Name UNIQUE,
    TargetPercentage  DECIMAL(5,2)  NOT NULL CONSTRAINT DF_AssetCategories_Target DEFAULT (0)
                      CONSTRAINT CK_AssetCategories_Target CHECK (TargetPercentage BETWEEN 0 AND 100),
    RiskLevel         NVARCHAR(10)  NOT NULL CONSTRAINT DF_AssetCategories_Risk DEFAULT (N'Sedang')
                      CONSTRAINT CK_AssetCategories_Risk CHECK (RiskLevel IN (N'Rendah', N'Sedang', N'Tinggi')),
    Description       NVARCHAR(255) NULL
);
GO

IF OBJECT_ID(N'dbo.Assets', N'U') IS NULL
CREATE TABLE dbo.Assets (
    AssetId             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assets PRIMARY KEY,
    CategoryId          INT            NOT NULL CONSTRAINT FK_Assets_Categories REFERENCES dbo.AssetCategories(CategoryId),
    TickerSymbol        NVARCHAR(20)   NOT NULL CONSTRAINT UQ_Assets_Ticker UNIQUE,
    AssetName           NVARCHAR(100)  NOT NULL,
    UnitType            NVARCHAR(20)   NOT NULL,
    TotalUnits          DECIMAL(18,6)  NOT NULL CONSTRAINT DF_Assets_TotalUnits DEFAULT (0)
                        CONSTRAINT CK_Assets_TotalUnits CHECK (TotalUnits >= 0),
    AverageBuyPrice     DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Assets_Avg DEFAULT (0),
    CurrentMarketPrice  DECIMAL(18,2)  NOT NULL CONSTRAINT CK_Assets_Price CHECK (CurrentMarketPrice > 0),
    UpdatedAt           DATETIME2(0)   NOT NULL CONSTRAINT DF_Assets_UpdatedAt DEFAULT (SYSDATETIME())
);
GO

IF OBJECT_ID(N'dbo.Transactions', N'U') IS NULL
CREATE TABLE dbo.Transactions (
    TransactionId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY,
    AssetId          INT            NOT NULL CONSTRAINT FK_Transactions_Assets REFERENCES dbo.Assets(AssetId),
    UserId           INT            NOT NULL CONSTRAINT FK_Transactions_Users REFERENCES dbo.Users(UserId),
    TransactionType  NVARCHAR(10)   NOT NULL CONSTRAINT CK_Transactions_Type CHECK (TransactionType IN (N'BUY', N'SELL', N'DIVIDEND')),
    Units            DECIMAL(18,6)  NOT NULL CONSTRAINT CK_Transactions_Units CHECK (Units > 0),
    PricePerUnit     DECIMAL(18,2)  NOT NULL CONSTRAINT CK_Transactions_Price CHECK (PricePerUnit > 0),
    Fee              DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Transactions_Fee DEFAULT (0)
                     CONSTRAINT CK_Transactions_Fee CHECK (Fee >= 0),
    TotalAmount      DECIMAL(18,2)  NOT NULL,
    RealizedPnL      DECIMAL(18,2)  NULL,
    TransactionDate  DATETIME2(0)   NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Transactions_Asset_Date')
    CREATE INDEX IX_Transactions_Asset_Date ON dbo.Transactions(AssetId, TransactionDate, TransactionId);
GO

IF OBJECT_ID(N'dbo.JournalEntries', N'U') IS NULL
CREATE TABLE dbo.JournalEntries (
    JournalId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_JournalEntries PRIMARY KEY,
    TransactionId  INT            NOT NULL CONSTRAINT UQ_JournalEntries_Transaction UNIQUE
                   CONSTRAINT FK_JournalEntries_Transactions REFERENCES dbo.Transactions(TransactionId) ON DELETE CASCADE,
    ThesisNote     NVARCHAR(MAX)  NULL,
    TargetPrice    DECIMAL(18,2)  NULL,
    StopLossPrice  DECIMAL(18,2)  NULL,
    EmotionTag     NVARCHAR(30)   NULL,
    StrategyTag    NVARCHAR(50)   NULL
);
GO
