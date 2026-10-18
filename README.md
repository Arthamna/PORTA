# PORTA — Portfolio & Multi-Asset Investment Journal System

PORTA is a Rich UI desktop app for managing a multi-asset investment portfolio (Stocks, Crypto, Mutual Funds/Bonds, Gold, Cash) and keeping an investment journal. It is built with **C# / .NET 8**, **Avalonia UI 11** (cross-platform, XAML), **SQL Server**, and **Microsoft.Data.SqlClient (ADO.NET)** using the **MVVM + Repository Pattern** architecture, following the PRD in `PRD_PORTA_Sistem_Manajemen_Portofolio_Multi_Aset.pdf`.

> The PRD allows WPF or Avalonia. Avalonia was chosen so the app runs on Windows, Linux, and macOS.

---

## Features (PRD §5.1 & §7)

| Module | What it does | FR |
|---|---|---|
| Login & roles | BCrypt login, logout clears the session, **Investor** (full CRUD) vs **Reviewer** (read-only) | FR-01, FR-02 |
| Dashboard | 4 KPI cards (Net Worth, Invested Capital, Unrealized P&L Rp & %, Realized P&L), allocation donut chart, rebalancing panel, last 5 transactions, empty state | FR-03, FR-04 |
| Assets & Portfolio | Asset CRUD (unique ticker), quick market-price update, case-insensitive ticker/name search, category filter, DataGrid with green/red P&L | FR-05 – FR-07 |
| Transactions | BUY/SELL/DIVIDEND with automatic Average Buy Price & Realized P&L; overselling is rejected; deleting a transaction recalculates the position | FR-08 – FR-10, FR-13 |
| Investment Journal | Thesis, Target Price, Stop Loss, Emotion Tag (Rational, Deep Research, FOMO, Panic, Speculation), strategy tag; filter by emotion/asset; plan (TP/SL) vs. realization | FR-11 |
| Categories & Rebalancing | Category CRUD, target % per asset class (total must be 100%), Overweight / Ideal / Underweight status (±5% tolerance) | FR-12 |
| Settings | Dark/Light Mode, user profile, database connection status, Logout | FR-14 |
| Errors | Success/failure toast notifications, delete confirmation dialogs, DB connection failures do not crash the app | FR-15 |

Non-functional requirements: async/await on every DB operation (NFR-01), `decimal` in C# + `DECIMAL(18,2|4|6)` in SQL (NFR-02), one `SqlTransaction` for Transaction + Journal + Asset update (NFR-03), parameterized queries + BCrypt (NFR-04), View/ViewModel/Service/Repository/Model separation (NFR-05).

---

## Prerequisites

| Software | Version | Notes |
|---|---|---|
| .NET SDK | **8.0** | `dotnet --version` should print `8.0.x` |
| Docker | 20+ with Docker Compose v2 | For running SQL Server. Optional if you already have SQL Server installed |

### Installing the .NET 8 SDK

**Ubuntu / Debian**
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```
If the package isn't available on your distro, use the official script (no sudo needed):
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 8.0
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$PATH      # add both lines to ~/.bashrc
```

**Windows / macOS** — download the installer from <https://dotnet.microsoft.com/download/dotnet/8.0>
(Windows: `winget install Microsoft.DotNet.SDK.8`, macOS: `brew install --cask dotnet-sdk@8`).

---

## How to Run

### 1. Start SQL Server (Docker)

From the project root folder:

```bash
docker compose up -d
```

This runs SQL Server 2022 Developer in a container named `porta-sql` on port **1433**, with:

- User: `sa`
- Password: `Porta@12345`

Wait about 20–30 seconds until the container is **healthy**:

```bash
docker ps --filter name=porta-sql     # STATUS column shows "(healthy)"
```

### 2. Create the database (`PORTA_DB`)

**Option A — automatic (default).** When the app starts and `PORTA_DB` doesn't exist yet, the app runs `database/01_schema.sql` and `database/02_seed.sql` on its own (`"AutoInitialize": true` setting in `appsettings.json`). Skip straight to step 3.

**Option B — manual via sqlcmd** (the `database/` folder is already mounted into the container at `/scripts`):

```bash
docker exec -it porta-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Porta@12345' -C -i /scripts/01_schema.sql
docker exec -it porta-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Porta@12345' -C -i /scripts/02_seed.sql
```

Both scripts are *idempotent* (safe to run more than once). You can also run them from SSMS / Azure Data Studio.

### 3. Run the application

```bash
dotnet restore
dotnet run --project src/Porta.App
```

### 4. Log in with a demo account

| Username | Password | Role | Access |
|---|---|---|---|
| `investor` | `investor123` | Investor | Full CRUD on every module |
| `reviewer` | `reviewer123` | Reviewer | Read-only (action buttons disabled, the service layer rejects changes) |

The seed data includes 5 categories, 7 assets (BBCA, TLKM, BTC, ETH, RDPU, ANTAM, KAS-BCA), 10 transactions (BUY/SELL/DIVIDEND), and 7 journal entries. The allocation is deliberately unbalanced so the Overweight/Underweight indicators show up right away.

---

## Configuration

The connection string lives in `src/Porta.App/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PortaDb": "Server=localhost,1433;Database=PORTA_DB;User Id=sa;Password=Porta@12345;TrustServerCertificate=True;Encrypt=True;Connect Timeout=5"
  },
  "Database": { "AutoInitialize": true }
}
```

You can override it without editing the file by setting an environment variable:

```bash
# Linux/macOS
export PORTA_CONNECTION_STRING="Server=myserver,1433;Database=PORTA_DB;User Id=...;Password=...;TrustServerCertificate=True"
# Windows (PowerShell)
$env:PORTA_CONNECTION_STRING="Server=localhost\SQLEXPRESS;Database=PORTA_DB;Trusted_Connection=True;TrustServerCertificate=True"
```

---

## Running the Tests

```bash
dotnet test
```

The tests (xUnit, `tests/Porta.Tests`) cover every scenario in PRD §16 plus the formulas in PRD §8:

| Scenario | Expected result |
|---|---|
| Buy 100 @ Rp 1,000 then 100 @ Rp 2,000 | 200 units, Avg Rp 1,500 |
| Sell 250 units with only 200 held | Rejected (`InsufficientUnitsException`) |
| Sell 50 @ Rp 2,000, Avg Rp 1,500 | 150 units left, Avg stays Rp 1,500, Realized +Rp 25,000 |
| Market price makes a category exceed target by > 5% | Status becomes Overweight |
| Dividend | Realized grows, unit count unchanged |
| Deleting a transaction (replay) | Position is recalculated; rejected if a later SELL becomes invalid |

---

## Architecture (PRD §13)

```
View (XAML)  ──binding──▶  ViewModel  ──▶  Service  ──▶  Repository  ──▶  SQL Server
 Views/*.axaml              ViewModels/     Services/      Repositories/    PORTA_DB
                            (state, ICommand,  (business      (parameterized
                             validation)       formulas, auth,  ADO.NET queries,
                                               SqlTransaction)  SqlTransaction)
                                     Models/ (entities & DTOs)
```

```
PORTA/
├─ docker-compose.yml            SQL Server 2022 (container porta-sql)
├─ database/
│  ├─ 01_schema.sql              DDL: Users, AssetCategories, Assets, Transactions, JournalEntries
│  └─ 02_seed.sql                Demo accounts & sample data
├─ src/Porta.App/
│  ├─ Models/                    Asset, Transaction, JournalEntry, AssetCategory, User, PortfolioSummaryDto, ...
│  ├─ Data/                      AppSettings, DatabaseHelper, DbInitializer
│  ├─ Repositories/              User-, Category-, Asset-, Transaction-, JournalRepository
│  ├─ Services/                  PortfolioCalculationService (PRD §8 formulas), TransactionService,
│  │                             PortfolioService, AuthService, SessionService, Theme/Dialog/Notification
│  ├─ ViewModels/                Main, Login, Dashboard, Holdings, Transactions(+Form), Journal, Allocation, Settings
│  ├─ Views/                     MainWindow + one view per page + modal dialogs
│  ├─ Controls/DonutChart.cs     Allocation donut chart (no external dependencies)
│  ├─ Converters/                Rupiah, %, +/- sign, profit/loss colors
│  └─ Styles/AppStyles.axaml     Design system (PRD §10 palette)
└─ tests/Porta.Tests/            Unit tests for the calculations & formatting
```

### Core business rules (PRD §8)

- **BUY**: `AvgNew = (UnitsOld × AvgOld + UnitsBought × Price + Fee) / (UnitsOld + UnitsBought)`
- **SELL**: requires `UnitsSold ≤ UnitsOld`; `Realized = (UnitsSold × Price − Fee) − UnitsSold × AvgOld`; Avg is unchanged (becomes 0 when units reach 0)
- **DIVIDEND**: `Realized = Units × DividendPerUnit − Fee`; units are unchanged
- **Unrealized**: `MarketValue − TotalUnits × Avg`, percent = `Unrealized / InvestedCapital × 100`
- **Rebalancing**: `Deviation = Actual% − Target%`; `> +5%` Overweight, `< −5%` Underweight, otherwise Ideal

Every transaction save/delete runs inside one `SqlTransaction`: INSERT/DELETE Transactions → INSERT JournalEntries → recalculate the asset's history (replay) → UPDATE Assets → Commit (or Rollback if anything fails).

### Number input format

Number fields accept `9800`, `9.800.000`, `0,025`, or `0.025`. A comma is treated as the decimal separator; with no comma, a single dot is the decimal point and multiple dots are thousands separators.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Login page shows "Database tidak tersedia" (database unavailable) | Make sure `docker ps` shows `porta-sql` as healthy, then click **Coba Lagi** (Try Again) |
| `Login failed for user 'sa'` | The password in `appsettings.json` must match `MSSQL_SA_PASSWORD` in `docker-compose.yml` |
| SQL Server container exits right away | SQL Server needs ≥ 2 GB of RAM; check `docker logs porta-sql` |
| SSL/certificate error | Keep `TrustServerCertificate=True` in the connection string |
| Linux: app fails to open a window / fontconfig error | Install the GUI libraries: `sudo apt install libice6 libsm6 libfontconfig1` |
| Want to reset the database to the seed data | `docker compose down -v && docker compose up -d`, then rerun the app |

Stop the database: `docker compose down` (data persists in the `porta-sql-data` volume). Delete all data: `docker compose down -v`.
