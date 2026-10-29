import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditEntry, ImportResult, Transaction } from './models';

/**
 * Thin wrapper around the backend API.
 * During development, /api is proxied to the ASP.NET app (see proxy.conf.json).
 */
@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api';

  getTransactions(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${this.baseUrl}/transactions`);
  }

  getAuditHistory(id: number): Observable<AuditEntry[]> {
    return this.http.get<AuditEntry[]>(`${this.baseUrl}/transactions/${id}/audit`);
  }

  runImport(): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.baseUrl}/import`, null);
  }
}
