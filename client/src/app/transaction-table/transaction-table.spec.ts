import { TestBed } from '@angular/core/testing';
import { Transaction } from '../models';
import { TransactionTable } from './transaction-table';

const tx = (id: number, overrides: Partial<Transaction> = {}): Transaction => ({
  id,
  sourceSystem: 'Online',
  transactionId: 100000 + id,
  cardNumber: '***1111',
  locationCode: 'ONLINE',
  productName: `Product ${id}`,
  amount: id * 10,
  transactionTime: `2026-03-01T0${id}:00:00Z`,
  status: 'Active',
  createdAtUtc: '2026-03-01T12:00:00Z',
  updatedAtUtc: '2026-03-01T12:00:00Z',
  ...overrides,
});

describe('TransactionTable', () => {
  async function render(items: Transaction[]) {
    const fixture = TestBed.createComponent(TransactionTable);
    fixture.componentRef.setInput('transactions', items);
    await fixture.whenStable();
    return fixture;
  }

  const productCells = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('tbody tr td:nth-child(4)')).map((td) => td.textContent?.trim());

  it('shows the newest transactions first by default', async () => {
    const fixture = await render([tx(1), tx(3), tx(2)]);
    expect(productCells(fixture.nativeElement)).toEqual(['Product 3', 'Product 2', 'Product 1']);
  });

  it('sorts by a column when its header is clicked', async () => {
    const fixture = await render([tx(2), tx(1), tx(3)]);
    const amountHeader = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('th button'),
    ).find((b) => b.textContent?.includes('Amount'))!;

    amountHeader.click();
    await fixture.whenStable();

    expect(productCells(fixture.nativeElement)).toEqual(['Product 1', 'Product 2', 'Product 3']);
  });

  it('pages results ten at a time', async () => {
    const items = Array.from({ length: 12 }, (_, i) => tx(1, { id: i + 1, transactionId: i + 1 }));
    const fixture = await render(items);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr').length).toBe(10);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Page 1 of 2');
  });
});
