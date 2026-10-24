namespace TransactionsIngest.Api.Domain;

public enum AuditChangeType
{
    Created = 0,
    Updated = 1,
    Revoked = 2,
    Restored = 3,   // a revoked transaction reappeared in a later snapshot
    Finalized = 4
}