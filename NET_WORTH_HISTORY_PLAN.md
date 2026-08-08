# Net Worth History Feature Plan (Blazor Interactive Server)

## Problem statement
Provide a net worth tracking application where authenticated users can manage catalogue data, maintain accounts and holdings, capture dated balance snapshots, and review history at total, account, and instrument levels.  
The model should support reusable instruments across accounts while keeping catalogue management and snapshot behavior clear and consistent.

## Current state analysis
- The repository is now a working ASP.NET Core + Blazor Web App using Interactive Server components rather than a greenfield scaffold.
- Microsoft Identity authentication, a global fallback authorization policy, EF Core, and SQL Server persistence are already wired up.
- The app already includes pages and services for institutions, accounts, account-linked instruments, snapshots, and a unified history experience.
- History is already implemented as a single `/history` page with net worth, account, and instrument drill-down support.
- Institutions already behave as a shared catalogue entity.
- Instruments are still modeled as children of a single account, so reusable cross-account instruments are not yet supported.
- The current snapshot and history logic assumes instrument ownership is derived directly from `Account -> Instruments`.
- There is still no automated test project in the repository.

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

5. **Unified history screen**
   - Query dated totals: `sum(asset balances) - sum(liability balances)` by date.
   - Display a default net worth line chart with a date range filter.
   - Add an account selector to switch from total net worth history to per-account history on the same page.
   - If instruments exist, allow drill-down charting by instrument within the selected account on the same page.
   - Optional grouped view by type (cash, investment, debt, etc.).

6. **Azure readiness**
   - Move secrets/connection strings to user secrets and then Azure App Configuration/Key Vault or App Service settings.
   - Add deployment notes and migration execution steps for Azure.

## Instrument architecture update
- `Instrument` should move from an account-owned child entity to a **shared catalogue entity**.
- Add an `AccountInstrument` link entity so accounts can reuse existing instruments.
- Instruments are universal rather than user-scoped.
- Enforce unique constraints on instrument `Name` and `Ticker`.
- Removing an instrument from an account should **unlink only**; it should not delete the shared instrument record.
- Editing an instrument updates the shared record for **all** linked accounts.
- Instrument snapshots should be tied to the account-specific relationship, so the snapshot model should reference `AccountInstrumentId` rather than only `InstrumentId`.
- The Accounts page should support both:
  - linking an existing instrument to an account
  - creating a new instrument inline and linking it immediately
- Institutions and instruments should be managed from a single shared **catalogue page** rather than separate catalogue pages.

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

- `Instrument` (shared catalogue entity)
  - `Id` (guid)
  - `Name`
  - `Ticker` (nullable)
  - `Type` (enum/string: Stock, ETF, MutualFund, Bond, Crypto, CashPosition, DebtLine, Other)
  - `CreatedUtc`
  - Unique index on `Name`
  - Unique index on `Ticker`

- `AccountInstrument`
  - `Id` (guid)
  - `AccountId` (fk)
  - `InstrumentId` (fk)
  - `CreatedUtc`
  - Unique index on (`AccountId`, `InstrumentId`)

- `InstrumentBalanceSnapshot`
  - `Id` (guid)
  - `AccountSnapshotId` (fk to `AccountSnapshot`)
  - `AccountInstrumentId` (fk)
  - `Balance` (decimal(18,2))
  - `CreatedUtc`
  - Unique index on (`AccountSnapshotId`, `AccountInstrumentId`)

## Model rules and invariants
- Snapshot value model: store value only in v1 (no quantity/price columns).
- For each `AccountId + SnapshotDate`, snapshot source must be exactly one of:
  - an `AccountSnapshot` with `AccountBalance` populated (account-level snapshot), or
  - an `AccountSnapshot` with one or more linked `InstrumentBalanceSnapshot` rows (instrument-level snapshot).
- Net worth calculation per date:
  - accounts using instrument snapshots: sum linked instrument snapshot values,
  - accounts using account snapshots: use `AccountSnapshot.AccountBalance`.
- All balances are stored as positive values; account `Category` determines plus/minus effect in net worth math.
- Instrument membership in an account is derived through `AccountInstrument`; `Instrument` itself is shared.
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
- Instrument ownership model: **shared `Instrument` catalogue linked through `AccountInstrument`**.
- Instrument reuse: **accounts can link existing instruments or create-and-link new instruments inline**.
- Instrument edit behavior: **editing an instrument affects all linked accounts**.
- Instrument removal behavior: **removing from an account unlinks only**.
- Instrument uniqueness: **instrument `Name` and `Ticker` must be unique**.
- Catalogue management UI: **institutions and instruments should be managed from a single catalogue page**.
- Instrument snapshot fields: **value only**.
- Snapshot precedence: **instrument snapshots are used when present for that account/date**.
- Snapshot input rule: **block account snapshot if instrument snapshots exist for account/date (and vice versa)**.
- Snapshot linkage: **instrument snapshots are linked to `AccountSnapshot` and the account-specific `AccountInstrument` relationship**.
- Ownership key: **use `UserId` (not `OwnerId`) sourced from authenticated claims (`oid`, fallback `sub`)**.
- Ownership FK: **none in v1** (no local `Users` table yet).
- Claim mapping definition: **`oid with sub fallback` means use `oid` first (Microsoft Entra object/user ID for the signed-in user). If `oid` is missing, use `sub`, where `sub` is the OpenID Connect "subject" claim (the token issuer's unique identifier for that user).**

## Todos
1. Add Blazor Web App (Interactive Server) shell and navigation.
2. Add EF Core SQL Server infrastructure and initial migration.
3. Implement account entity/model and account CRUD components.
4. Implement shared instrument catalogue support, `AccountInstrument` linking, and catalogue-page management for institutions and instruments.
5. Implement dated snapshot entry/edit workflow with partial snapshots.
6. Implement a unified history page for net worth, per-account, and per-instrument charts.
7. Add auth-bound data ownership checks in all queries/commands.
8. Enforce account/date snapshot exclusivity validation in application and persistence layers.
9. Add Azure deployment and database configuration documentation updates.

## Notes
- Keep first release intentionally simple: single user profile data tied to logged-in identity, single currency, no account aggregation automation.
- This creates a stable foundation for future enhancements (import from institutions, recurring valuations, projection forecasting).
