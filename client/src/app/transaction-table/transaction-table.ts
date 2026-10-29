import { Component, computed, input, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Transaction, TransactionStatus } from '../models';

type SortKey =
  | 'sourceSystem'
  | 'transactionId'
  | 'locationCode'
  | 'productName'
  | 'amount'
  | 'transactionTime'
  | 'status';

/**
 * Transactions grid with client-side filtering, sorting and paging.
 * The full list is small, so it is loaded once and shaped with computed signals.
 */
@Component({
  selector: 'app-transaction-table',
  imports: [CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './transaction-table.html',
  styleUrl: './transaction-table.css',
})
export class TransactionTable {
  readonly transactions = input.required<Transaction[]>();
  readonly selectedId = input<number | null>(null);
  readonly selected = output<Transaction>();

  protected readonly columns: { key: SortKey; label: string; numeric?: boolean }[] = [
    { key: 'sourceSystem', label: 'Source System' },
    { key: 'transactionId', label: 'Transaction ID' },
    { key: 'locationCode', label: 'Location' },
    { key: 'productName', label: 'Product' },
    { key: 'amount', label: 'Amount', numeric: true },
    { key: 'transactionTime', label: 'Transaction Time' },
    { key: 'status', label: 'Status' },
  ];
  protected readonly statuses: TransactionStatus[] = ['Active', 'Revoked', 'Finalized'];
  protected readonly pageSizes = [10, 25, 50];

  protected readonly sourceFilter = signal('');
  protected readonly statusFilter = signal('');
  protected readonly search = signal('');
  protected readonly sortKey = signal<SortKey>('transactionTime');
  protected readonly sortDir = signal<'asc' | 'desc'>('desc');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);

  protected readonly sources = computed(() =>
    [...new Set(this.transactions().map((t) => t.sourceSystem))].sort(),
  );

  protected readonly filtered = computed(() => {
    const source = this.sourceFilter();
    const status = this.statusFilter();
    const term = this.search().trim().toLowerCase();

    return this.transactions().filter(
      (t) =>
        (!source || t.sourceSystem === source) &&
        (!status || t.status === status) &&
        (!term ||
          t.productName.toLowerCase().includes(term) ||
          t.locationCode.toLowerCase().includes(term) ||
          String(t.transactionId).includes(term)),
    );
  });

  protected readonly sorted = computed(() => {
    const key = this.sortKey();
    const direction = this.sortDir() === 'asc' ? 1 : -1;
    const value = (t: Transaction) =>
      key === 'transactionTime' ? Date.parse(t.transactionTime) : t[key];

    return [...this.filtered()].sort((a, b) => {
      const x = value(a);
      const y = value(b);
      return (x < y ? -1 : x > y ? 1 : 0) * direction;
    });
  });

  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.sorted().length / this.pageSize())),
  );
  protected readonly currentPage = computed(() => Math.min(this.page(), this.pageCount()));
  protected readonly pageItems = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.sorted().slice(start, start + this.pageSize());
  });

  protected sortBy(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(key);
      this.sortDir.set('asc');
    }
  }

  protected ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    if (this.sortKey() !== key) return 'none';
    return this.sortDir() === 'asc' ? 'ascending' : 'descending';
  }

  protected arrow(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '▲' : '▼';
  }

  protected setFilter(filter: 'source' | 'status' | 'search', value: string): void {
    ({ source: this.sourceFilter, status: this.statusFilter, search: this.search })[filter].set(value);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(Number(size));
    this.page.set(1);
  }
}
