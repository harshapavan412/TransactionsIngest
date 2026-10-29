# Transactions Ingest

An ASP.NET 10 Web API and Angular application that imports card transaction snapshots from three source systems (**Store Uptown**, **Store Downtown**, **Online**), reconciles them with a local SQLite database, and keeps a field-level audit history of every change.

| Layer | Technology |
|---|---|
| Backend | ASP.NET 10 Web API (controllers), C# |
| Data | EF Core 10 Code-First, SQLite |
| Frontend | Angular 22 (standalone components, signals, RxJS) |
| Tests | xUnit (backend), Vitest (frontend) |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) **24.15+** (or 26.x), required by Angular 22
- Git

No database server is needed. SQLite is a single file that the app creates on first run.

## Build

```bash
# Backend
dotnet build

# Frontend
cd client
npm install
npm run build
```

## Run

Start the backend and frontend in two terminals.

**1. Backend API** (from the repository root):

```bash
dotnet run --project src/TransactionsIngest.Api
```

The API listens on `http://localhost:5071`. On startup it:
- applies EF Core migrations, creating `transactions.db` if it does not exist
- creates the mock source snapshot files in `src/TransactionsIngest.Api/sample-data/` if they do not exist

**2. Frontend:**

```bash
cd client
npm start
```

Open **http://localhost:4200** and click **Run import**.

During development, Angular proxies `/api` to the backend (`client/proxy.conf.json`), so no URL configuration is needed.

## Test

```bash
# Backend: 15 tests (import reconciliation and validation)
dotnet test

# Frontend: 6 tests (API service, table sorting/paging, app shell)
cd client
npx ng test --watch=false
```

---

## Trying the import manually

The mock source systems read their snapshot from JSON files **on every import**, so you can simulate source-system behavior by editing them between imports:

| To simulate | Do this in `sample-data/*.json` | Then run import and expect |
|---|---|---|
| A correction | Change an `amount` or `productName` | `updated: 1`, plus an audit row per changed field |
| A revoked transaction | Delete an entry less than 24 hours old | `revoked: 1` |
| A delayed or new transaction | Add an entry | `inserted: 1` |
| A revoked transaction coming back | Re-add a deleted entry | `restored: 1` |
| Idempotency | Change nothing | everything `unchanged` |

The seeded data includes:
- the same `transactionId` (100001) in all three sources, to exercise the composite key
- entries 30-50 hours old, which are **finalized** on the first import
- one deliberately invalid record (see [Assumptions](#assumptions)), which is **skipped**

**To reset:** stop the API, delete `src/TransactionsIngest.Api/sample-data/` and `src/TransactionsIngest.Api/transactions.db`, then start it again. The sample files are regenerated with timestamps relative to the current time.

You can also call the API directly; see `src/TransactionsIngest.Api/TransactionsIngest.Api.http`.

## API

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/import` | Runs an import and returns the run summary (counts per outcome) |
| `GET` | `/api/import/runs` | The 20 most recent import runs |
| `GET` | `/api/transactions` | Transactions, newest first. Optional filters: `?sourceSystem=Online&status=Revoked` |
| `GET` | `/api/transactions/{id}/audit` | Audit history for one transaction, oldest first |

Errors are returned as RFC 7807 `ProblemDetails` JSON.

---

## Project structure

```
src/TransactionsIngest.Api/
  Domain/        Entities: Transaction, TransactionAudit, ImportRun, and status/change-type enums
  Data/          TransactionsDbContext (Fluent API configuration), UTC converter, migrations
  Sources/       ITransactionSource, JSON-file mock implementation, sample-data seeder
  Import/        IImportService, ImportService (reconciliation), validator, options, result
  Contracts/     API DTOs
  Controllers/   ImportController, TransactionsController
  Program.cs     Composition root: DI, configuration, CORS, error handling, migrations
tests/TransactionsIngest.Tests/
  ImportServiceTests.cs               One test per business rule
  SourceTransactionValidatorTests.cs
  TestSupport.cs                      In-memory SQLite, fake sources, controllable clock
client/          Angular app
docs/ai/         AI usage disclosure
```

---

## Design decisions

### Import logic is independent of how it is triggered
All import behavior lives in `ImportService` behind `IImportService`. `POST /api/import` is a thin controller that calls it. A scheduled job (for example a `BackgroundService` with a `PeriodicTimer`, Hangfire, or Quartz) would resolve `IImportService` from a DI scope and call the same `RunImportAsync()` method, with no changes to the import code.

### Source systems behind an interface
`ITransactionSource` exposes a source name and `GetSnapshotAsync()`. The mock implementation reads a JSON file in the same shape as the source API sample. Replacing it with a real HTTP client requires only a new implementation and a registration change. Sources are listed in `appsettings.json`, so adding a source is a configuration change.

### Data model
- **`Transactions`** has a surrogate primary key (`Id`) and a **unique index on `(SourceSystem, TransactionId)`**, the logical key from the spec. The database itself rejects duplicates even if application logic were wrong.
- **`TransactionAudits`** stores one row per event. Updates produce **one row per changed field** with old and new values. Status changes (Revoked, Restored, Finalized) are recorded with `FieldName = "Status"`. Each row references the import run that caused it.
- **`ImportRuns`** stores one row per execution with timestamps, success flag, error message and counts. This gives operational traceability and makes idempotency visible.
- Enums are stored as strings for readability. Entity configuration uses the Fluent API, so domain classes contain no persistence attributes.

### Reconciliation algorithm (per source)
1. Fetch the snapshot. If the fetch fails, **skip that source for this run**. A missing or failed snapshot is never treated as "zero transactions", because that would revoke everything.
2. Validate each record; invalid or duplicate records are skipped and logged.
3. Load only the relevant existing rows: those in the snapshot, plus Active ones (revocation candidates).
4. For each incoming record:
   - **New:** insert, with a `Created` audit entry.
   - **Changed:** update the fields, with one `Updated` audit entry per field.
   - **Previously revoked and present again:** restore to Active, with a `Restored` audit entry.
   - **Identical:** no change.
5. For each **Active** existing transaction **missing** from the snapshot whose `TransactionTime` is **within the revocation window**: mark it Revoked, with a `Revoked` audit entry.

After all sources are processed, **Active transactions older than the window are Finalized**, and finalized transactions are immutable.

### Idempotency
Changes are written only when a field value or status actually differs, so re-running an import with identical source data produces no inserts, updates or audit rows (`ImportServiceTests.ImportingIdenticalSnapshotTwice_IsIdempotent`). Each run still records an `ImportRuns` row, which is an operational log, not transaction data.

### Atomicity and concurrency
- All data changes for a run happen in **one database transaction**. A failure rolls everything back, and the run is recorded as failed.
- A process-wide `SemaphoreSlim` ensures only one import runs at a time; a concurrent request waits.

### Time handling
- `TransactionTime` and all timestamps are UTC. SQLite does not preserve `DateTimeKind`, so a value converter marks values read from the database as UTC; the API therefore always returns ISO 8601 strings with `Z`. The UI shows local time, with UTC in the tooltip and details panel.
- The service uses .NET's `TimeProvider` instead of `DateTime.UtcNow`, so tests can control "now" and verify the 24-hour rules deterministically.

### Configuration, logging and errors
- Externalized in `appsettings.json`: connection string, revocation window (hours), finalization on/off, source list, CORS origins. Options are bound to typed classes and **validated at startup**.
- Structured logging (`ILogger` with message templates) for run start and end, per-source counts, skipped records, and failures.
- Unhandled exceptions return `ProblemDetails`, never stack traces.

### Frontend
- Standalone components with **signals** for state; filtering, sorting and paging are `computed` signals.
- The audit panel uses RxJS **`switchMap`**, so a quick change of selection cancels the in-flight request and a stale response can never overwrite a newer one.
- Implemented optional features: sorting, filtering, search, pagination, details view, audit history view, responsive layout, manual import button, light and dark theme.

### Testing
The backend tests run the **real `ImportService`** against an **in-memory SQLite database** (the same provider as production, so constraints and SQL translation are exercised), with fake sources and a controllable clock. Covered rules: insert, composite key, idempotency, field-level update audit, revocation inside and outside the window, finalization and immutability, restoration, invalid-record handling, and source failure isolation.

---

## Assumptions

1. **Snapshots are full snapshots** of each source's recent transactions, so "missing from the snapshot" means removed by the source.
2. **String length limit:** the spec says "length < 20", which I implemented as a maximum of 20 characters. SQLite does not enforce declared lengths, so the importer validates them. Note that the spec's own sample product name, `"USB-C Docking Station"`, is **21 characters**. It is included in the seed data as an invalid record to demonstrate that such records are **skipped and logged** rather than truncated silently or crashing the import.
3. **Revocation window:** a transaction is revoked only if `TransactionTime` is within the last 24 hours (configurable). Older missing transactions are left as they are.
4. **Finalization** (optional in the spec) is enabled and configurable. Changes to a finalized transaction are ignored and logged as a warning.
5. **Revoked transactions can be restored** if they reappear in a later snapshot. The spec does not define this case; restoring keeps the data consistent with the source, and the audit trail shows both events.
6. **Amounts** must have at most 2 decimal places, matching `decimal(18,2)`. Negative amounts are allowed, since refunds are plausible.
7. A partially failed run (one source unavailable) still commits the other sources and reports `succeeded: false` with the error message.
8. Finalization depends on the current time, so a later run can finalize transactions that crossed the 24-hour mark even when source data is identical. Idempotency is defined as "same data at the same point in time produces the same result".

## Known limitations and next steps

- **Single instance:** the import lock is in-process. Multiple API instances would need a distributed lock (a database lock row, or the scheduler's own concurrency control).
- **Paging:** the frontend pages client-side, which is fine for this data size. For large volumes, add server-side paging and sorting to `GET /api/transactions`.
- **SQLite decimals:** SQLite stores `decimal` as text, so sorting by amount is done in the client. A production database (SQL Server or PostgreSQL) supports it natively.
- **Real sources:** an HTTP implementation of `ITransactionSource` should add timeouts and retries (for example with Polly or `Microsoft.Extensions.Http.Resilience`).
- **Security:** no authentication or authorization; card numbers are already masked by the sources.

---

## AI usage

AI assistance was used. See [`docs/ai/`](docs/ai/README.md) for the disclosure, the prompts, the conventions, and the design discussion.
