import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AuditHistory } from './audit-history/audit-history';
import { ImportResult, Transaction } from './models';
import { TransactionTable } from './transaction-table/transaction-table';
import { TransactionsApi } from './transactions-api.service';

@Component({
  selector: 'app-root',
  imports: [AuditHistory, DatePipe, TransactionTable],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly api = inject(TransactionsApi);

  protected readonly transactions = signal<Transaction[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  protected readonly importing = signal(false);
  protected readonly lastImport = signal<ImportResult | null>(null);
  protected readonly importError = signal<string | null>(null);

  protected readonly selectedId = signal<number | null>(null);
  protected readonly selected = computed(
    () => this.transactions().find((t) => t.id === this.selectedId()) ?? null,
  );

  protected readonly totals = computed(() => {
    const list = this.transactions();
    const count = (status: Transaction['status']) => list.filter((t) => t.status === status).length;
    return {
      total: list.length,
      active: count('Active'),
      revoked: count('Revoked'),
      finalized: count('Finalized'),
    };
  });

  constructor() {
    this.loadTransactions();
  }

  protected loadTransactions(): void {
    this.loading.set(true);
    this.api
      .getTransactions()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => {
          this.transactions.set(items);
          this.loadError.set(null);
        },
        error: () =>
          this.loadError.set('Could not reach the API. Is the backend running on http://localhost:5071?'),
      });
  }

  protected runImport(): void {
    this.importing.set(true);
    this.importError.set(null);
    this.api
      .runImport()
      .pipe(finalize(() => this.importing.set(false)))
      .subscribe({
        next: (result) => {
          this.lastImport.set(result);
          this.loadTransactions();
        },
        error: (err: HttpErrorResponse) =>
          this.importError.set(err.error?.title ?? 'The import failed. Check the API logs for details.'),
      });
  }
}
