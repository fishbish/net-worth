# Net Worth History Feature Plan (Blazor Interactive Server)

## Problem statement
The current app is an ASP.NET Core Razor Pages app with Microsoft Identity authentication scaffolded, but no domain model, no data persistence, and no net-worth tracking functionality yet.  
Goal: add the ability to capture dated snapshots of assets and liabilities, then graph total net worth history and per-account history.  
Also include an optional level under account for underlying instruments/holdings.

## Current state analysis
- Project is a single web project: `net-worth-app` targeting `.NET 10` (`net-worth-app.csproj`).
- UI is still starter pages (`Index`, `Privacy`) with no business features.
- Authentication is wired through Azure AD / Microsoft Identity (`Program.cs`, `appsettings.json`).
- No EF Core, no database context, no entities, no migrations.
- Layout/nav is simple and ready to extend (`Pages/Shared/_Layout.cshtml`).

## Technology choice assessment
Blazor is a strong choice for this product and Azure hosting target.

Decision for this plan:
- Use **Blazor Web App with Interactive Server render mode** (modern Blazor Server model).
- Keep Microsoft Identity integration.
- Build feature pages as Blazor components.

Why this is reasonable:
- Great fit for interactive dashboards/charts.
- Full-stack C# and shared models reduce duplication.
- Azure App Service hosting and auth integration are straightforward.

## Storage recommendation
Given your familiarity and Azure target, SQL Server is a strong default and not overkill for financial history data.

Recommended phased storage strategy:
1. Start with SQL Server LocalDB / SQL Express in development.
2. Use EF Core with SQL Server provider and migrations.
3. Deploy to Azure SQL Database (free/low-cost tier where available in your subscription).

Why this is a good fit:
- You already know SQL Server.
- Relational model cleanly supports user-scoped accounts, instruments, snapshots, and time-series queries.
- Azure SQL operational path is straightforward.

## Proposed implementation approach
Build in vertical slices so value appears early:

1. **Convert UI foundation to Blazor Web App (Interactive Server)**
   - Add Blazor services/endpoints in startup and app shell.
   - Create initial layout/navigation for Accounts, Instruments, Snapshots, Net Worth, Account History.
   - Preserve current authentication requirements.

2. **Domain and persistence foundation**
   - Add EF Core + SQL Server provider.
   - Create core entities for accounts, optional underlying instruments, and dated balances.
   - Add `DbContext`, connection string config, and first migration.

3. **Account and instrument management**
   - Blazor CRUD pages/components for accounts (name, type, asset/liability classification, optional institution, optional notes).
   - Add optional child instruments under each account (name/ticker/type/notes).
   - Per-user data ownership tied to authenticated user identity.

4. **Snapshot capture workflow**
   - Snapshot entry page where user selects a date and enters balances at account level and/or instrument level.
   - Save one row per account/instrument per date (upsert behavior for same entity/date).
   - Validation for required date and numeric values.
   - Support **partial snapshots** (missing entities are treated as no entry for that date).
   - Enforce exclusivity per account/date: block account snapshot when instrument snapshots exist for that account/date, and block instrument snapshots when an account snapshot already exists.

5. **Net worth history graph**
   - Query dated totals: `sum(asset balances) - sum(liability balances)` by date.
   - Display in a line chart component with date range filter.

6. **Per-account and per-instrument history graph**
   - Account detail component charting account balance over time.
   - If instruments exist, allow drill-down chart by instrument within the account.
   - Optional grouped view by type (cash, investment, debt, etc.).

7. **Azure readiness**
   - Move secrets/connection strings to user secrets and then Azure App Configuration/Key Vault or App Service settings.
   - Add deployment notes and migration execution steps for Azure.

## Data model (initial)
- `Account`
  - `Id` (guid)
  - `UserId` (string; Entra claim id, `oid` with `sub` fallback; not a FK in v1)
  - `Name`
  - `Category` (enum: Asset or Liability)
  - `Type` (enum/string: Cash, Brokerage, Retirement, CreditCard, Mortgage, Loan, Other)
  - `CreatedUtc`

- `AccountSnapshot` (header per account/date)
  - `Id` (guid)
  - `AccountId` (fk)
  - `SnapshotDate` (date)
  - `AccountBalance` (decimal(18,2), nullable; used for account-level snapshots)
  - `CreatedUtc`
  - Unique index on (`AccountId`, `SnapshotDate`)

- `Instrument` (optional child of account)
  - `Id` (guid)
  - `AccountId` (fk)
  - `Name`
  - `Ticker` (nullable)
  - `Type` (enum/string: Stock, ETF, MutualFund, Bond, Crypto, CashPosition, DebtLine, Other)
  - `CreatedUtc`

- `InstrumentBalanceSnapshot`
  - `Id` (guid)
  - `AccountSnapshotId` (fk to `AccountSnapshot`)
  - `InstrumentId` (fk)
  - `Balance` (decimal(18,2))
  - `CreatedUtc`
  - Unique index on (`AccountSnapshotId`, `InstrumentId`)

## Model rules and invariants
- Snapshot value model: store value only in v1 (no quantity/price columns).
- For each `AccountId + SnapshotDate`, snapshot source must be exactly one of:
  - an `AccountSnapshot` with `AccountBalance` populated (account-level snapshot), or
  - an `AccountSnapshot` with one or more linked `InstrumentBalanceSnapshot` rows (instrument-level snapshot).
- Net worth calculation per date:
  - accounts using instrument snapshots: sum linked instrument snapshot values,
  - accounts using account snapshots: use `AccountSnapshot.AccountBalance`.
- All balances are stored as positive values; account `Category` determines plus/minus effect in net worth math.
- Instrument ownership is derived through its parent `Account` (no separate `UserId` on `Instrument`).
- Ownership model in v1 is external-identity based: `UserId` is claim-backed and scoped in queries, without a local `Users` table FK.

## Graphing options
Primary recommendation: use Chart.js (or equivalent) from Blazor components via JS interop.  
Alternative: adopt a Blazor-native chart component library after initial MVP.

## Key design decisions confirmed
- Snapshot granularity: one daily value per account/instrument.
- Missing values on a date: **allow blank and treat as “no data”**.
- Multi-currency support: start single currency.
- Liability sign convention: store balances as positive values and apply sign in calculations.
- UI stack: **Blazor Web App (Interactive Server)**.
- Hierarchy: **optional instrument level under account**.
- Instrument snapshot fields: **value only**.
- Snapshot precedence: **instrument snapshots are used when present for that account/date**.
- Snapshot input rule: **block account snapshot if instrument snapshots exist for account/date (and vice versa)**.
- Snapshot linkage: **instrument snapshots are linked to `AccountSnapshot` header**.
- Ownership key: **use `UserId` (not `OwnerId`) sourced from authenticated claims (`oid`, fallback `sub`)**.
- Ownership FK: **none in v1** (no local `Users` table yet).
- Claim mapping definition: **`oid with sub fallback` means use `oid` first (Microsoft Entra object/user ID for the signed-in user). If `oid` is missing, use `sub`, where `sub` is the OpenID Connect "subject" claim (the token issuer's unique identifier for that user).**

## Todos
1. Add Blazor Web App (Interactive Server) shell and navigation.
2. Add EF Core SQL Server infrastructure and initial migration.
3. Implement account entity/model and account CRUD components.
4. Implement optional instrument entity/model and instrument CRUD under account.
5. Implement dated snapshot entry/edit workflow with partial snapshots.
6. Implement net worth history query and chart component.
7. Implement per-account and per-instrument history chart components.
8. Add auth-bound data ownership checks in all queries/commands.
9. Enforce account/date snapshot exclusivity validation in application and persistence layers.
10. Add Azure deployment and database configuration documentation updates.

## Notes
- Keep first release intentionally simple: single user profile data tied to logged-in identity, single currency, no account aggregation automation.
- This creates a stable foundation for future enhancements (import from institutions, recurring valuations, projection forecasting).
