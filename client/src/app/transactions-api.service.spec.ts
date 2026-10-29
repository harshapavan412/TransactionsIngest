import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TransactionsApi } from './transactions-api.service';

describe('TransactionsApi', () => {
  let api: TransactionsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(TransactionsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts to /api/import to run an import', () => {
    api.runImport().subscribe();
    const req = http.expectOne('/api/import');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('requests the audit history for a transaction', () => {
    api.getAuditHistory(42).subscribe();
    expect(http.expectOne('/api/transactions/42/audit').request.method).toBe('GET');
  });
});
