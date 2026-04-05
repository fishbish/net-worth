# Project Instructions (NetWorth)

This file captures project conventions and decisions provided during implementation.

## Naming and namespaces

- Use `NetWorth` as the root namespace (not `net_worth_app`).
- `Institution` is the accepted model name for account providers.

## Configuration conventions

- Use SQL Server with `Server=(local)` in connection strings.
- Keep the `NetWorth` connection string in `appsettings.json`.
- Do **not** duplicate the same connection string in `appsettings.Development.json` when it is identical.

## Data model conventions

- `Account` must reference a required `Institution` entity (FK), not a string institution name.
- Do **not** include `Notes` on `Account`.
- Keep blank lines between model properties for readability.
- Place navigation properties directly under their corresponding foreign key properties.

## EF Core conventions

- Store enum values as integers (default EF behavior), not strings.
- Prefer data annotations on model properties for:
  - required fields (`[Required]`)
  - max lengths (`[MaxLength]`)
  - decimal column types (`[Column(TypeName = "...")]`)
- Prefer index attributes on models (including unique indexes) over Fluent API index definitions when practical.
- Rely on EF key conventions for `Id` properties (no explicit `HasKey` unless needed).
- In `Data/NetWorthDbContext.cs`, keep `DbSet` properties in alphabetical order.
- Keep migration generation/application paused until explicitly requested.

