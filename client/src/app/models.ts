// Shapes returned by the TransactionsIngest API.

export type TransactionStatus = 'Active' | 'Revoked' | 'Finalized';

export type AuditChangeType = 'Created' | 'Updated' | 'Revoked' | 'Restored' | 'Finalized';

export interface Transaction {
  id: number;
  sourceSystem: string;
  transactionId: number;
  cardNumber: string;
  locationCode: string;
  productName: string;
  amount: number;
  transactionTime: string;
  status: TransactionStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AuditEntry {
  id: number;
  importRunId: number;
  changeType: AuditChangeType;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  changedAtUtc: string;
}

export interface ImportResult {
  importRunId: number;
  startedAtUtc: string;
  completedAtUtc: string | null;
  succeeded: boolean;
  errorMessage: string | null;
  inserted: number;
  updated: number;
  revoked: number;
  restored: number;
  finalized: number;
  unchanged: number;
  skipped: number;
}
