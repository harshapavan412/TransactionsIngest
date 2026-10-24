using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TransactionsIngest.Api.Data;

/// <summary>
/// SQLite has no native date type and loses DateTimeKind.
/// This stores values as UTC and marks them as UTC when read back,
/// so the API always returns times with a trailing "Z".
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDb => toDb.Kind == DateTimeKind.Utc ? toDb : toDb.ToUniversalTime(),
    fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));