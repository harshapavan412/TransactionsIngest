import { Component, inject, input, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, tap } from 'rxjs';
import { AuditEntry, Transaction } from '../models';
import { TransactionsApi } from '../transactions-api.service';

/**
 * Details and audit history for the selected transaction.
 * switchMap cancels the previous request when the selection changes quickly,
 * so a slow response can never overwrite a newer one.
 */
@Component({
  selector: 'app-audit-history',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './audit-history.html',
  styleUrl: './audit-history.css',
})
export class AuditHistory {
  private readonly api = inject(TransactionsApi);

  readonly transaction = input.required<Transaction>();
  readonly closed = output<void>();

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly entries = toSignal(
    toObservable(this.transaction).pipe(
      tap(() => {
        this.loading.set(true);
        this.error.set(null);
      }),
      switchMap((t) =>
        this.api.getAuditHistory(t.id).pipe(
          catchError(() => {
            this.error.set('Could not load the audit history.');
            return of<AuditEntry[]>([]);
          }),
        ),
      ),
      tap(() => this.loading.set(false)),
    ),
    { initialValue: [] as AuditEntry[] },
  );
}
