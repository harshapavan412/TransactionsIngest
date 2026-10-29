# Design Discussion

The design questions raised while building, and the decisions made.

**Build order: frontend, backend or database first?**
Backend first, starting with the data model. With EF Core Code-First the database is generated from the C# model, so "database first" and "model first" are the same step. The frontend only displays what the API returns, so it comes last.

**One project or several (Domain / Infrastructure / Api)?**
One API project with clear folders, plus a test project. Interfaces (`ITransactionSource`, `IImportService`) already keep the parts decoupled and testable; extra class libraries would add ceremony without changing the design.

**How should a transaction be identified?**
A surrogate primary key (`Id`) for relationships, plus a unique index on `(SourceSystem, TransactionId)`, because transaction IDs repeat across sources. The audit table's foreign key is named `TransactionRecordId` so it cannot be confused with the source system's `TransactionId`.

**How should field-level audit be stored?**
One row per changed field (`FieldName`, `OldValue`, `NewValue`), linked to the import run that caused it. This is easy to query ("what changed in run 6?") and easy to display.

**What if a source is unavailable?**
Skip it for that run and record the error. It must not be treated as an empty snapshot, which would revoke all of its recent transactions. The run is reported as `succeeded: false`.

**What if a record is invalid?**
Skip and log it, and count it in `SkippedCount`. Its ID is still treated as present in the source, so an existing transaction is not revoked just because the source sent a malformed update. This came up because the spec's own sample (`"USB-C Docking Station"`, 21 characters) violates the 20-character limit.

**What if a revoked transaction comes back?**
Restore it to Active with a `Restored` audit entry. This is not in the spec, but it keeps the data consistent with the source.

**How do we make the mock sources useful for testing by hand?**
Each source reads a JSON file on every import, so editing the file simulates corrections and removals without restarting. The files are generated on first run with timestamps relative to "now"; otherwise static dates would all be older than 24 hours, and revocation could never be demonstrated.

**How do we test 24-hour rules without waiting?**
Inject .NET's `TimeProvider`; tests use a controllable clock and move it forward.

**Why does SQLite need a UTC converter?**
SQLite stores dates as text and loses `DateTimeKind`. Without the converter, the API would return times without `Z`, and the browser would interpret them as local time.

**How do we keep the import safe?**
One database transaction per run (all or nothing), the run recorded before processing so failures leave a trace, and a lock so two imports cannot run at the same time.

**Where do business rules live?**
In configuration: `RevocationWindowHours` and `FinalizationEnabled` in `appsettings.json`, validated at startup. Adding a source system is also a configuration change.

**Frontend paging: client or server?**
Client-side for this data size, using computed signals. The API already supports server-side filters; server-side paging is listed as a next step for production volumes.
