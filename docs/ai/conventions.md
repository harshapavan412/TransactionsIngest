# Conventions and Rules Followed

## Process
- Build after every new file; commit after each completed piece (setup, domain model, database, sources, import service and API, tests, frontend, docs).
- Verify behavior manually against the running API before writing automated tests.
- No invented requirements: anything the spec leaves open is recorded as an assumption in the README.

## C# / .NET
- .NET 10, nullable reference types enabled, file-scoped namespaces.
- Primary constructors for dependency injection in services, controllers and the DbContext.
- Interfaces start with `I`; one public type per file (DTO records are grouped in `Contracts/ApiModels.cs`).
- Domain classes have no persistence attributes; all EF configuration uses the Fluent API in `TransactionsDbContext`.
- The Options pattern for configuration, with `ValidateDataAnnotations()` and `ValidateOnStart()`.
- `async`/`await` with `CancellationToken` passed through.
- Structured logging with message templates (`{SourceSystem}`, `{ImportRunId}`), never string concatenation.
- All times in UTC; `TimeProvider` instead of `DateTime.UtcNow` in business logic.
- Comments explain *why* (business rules, edge cases), not *what*.

## API
- Controllers stay thin; logic lives in services.
- DTOs are returned instead of entities; read queries use `AsNoTracking()`.
- Errors are returned as `ProblemDetails`.

## Testing
- Test names describe the business rule: `Method_Scenario_ExpectedResult`.
- Tests use the real service and the real database provider (in-memory SQLite) with fakes only at the boundaries (sources, clock).

## Angular
- Standalone components, signals for state, `computed` for derived data, built-in control flow (`@if`, `@for`).
- An HTTP service wraps all API calls; components never build URLs.
- RxJS where it adds value (`switchMap` cancellation for the audit panel).
- Accessible markup: real buttons for sortable headers, `aria-sort`, keyboard-selectable rows, visible focus styles.
