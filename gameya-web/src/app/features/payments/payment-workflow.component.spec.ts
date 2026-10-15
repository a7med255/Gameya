import { WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { PaymentDto, PaymentPageDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationContextService } from '../../core/services/association-context.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { PaymentsApiService } from '../../core/services/payments-api.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { PaymentWorkflowComponent } from './payment-workflow.component';

interface PaymentHarness {
  stage: WritableSignal<1 | 2 | 3>;
  result: WritableSignal<PaymentDto | null>;
  paymentPage: WritableSignal<PaymentPageDto | null>;
}

describe('PaymentWorkflowComponent', () => {
  let fixture: ComponentFixture<PaymentWorkflowComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentWorkflowComponent],
      providers: [
        { provide: PeopleApiService, useValue: { list: () => of({ items: [], page: 1, pageSize: 12, totalCount: 0 }) } },
        { provide: PaymentsApiService, useValue: {} },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        ApiErrorService, AssociationContextService, ConfirmService, NotificationService,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(PaymentWorkflowComponent);
  });

  it('renders the allocation returned by the backend, including association credit', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({ personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', amountDueNow: 5000, totalRemaining: 10000, associations: [{ associationId: 'a1', associationName: 'جمعية العيلة', monthlyContribution: 5000, amountDueNow: 5000, totalRemaining: 10000, oldestUnpaidDueDate: '2026-01-10', credit: 0, paymentStatus: 'Outstanding' }] });
    component.result.set({ id: 'pay1', personId: 'p1', amount: 8000, method: 'Cash', paidAtUtc: '2026-01-15T13:30:00Z', entryType: 'Payment', allocations: [{ associationId: 'a1', periodId: 'period1', amount: 5000, dueDate: '2026-01-10', lateDays: 5 }], credits: [{ associationId: 'a1', amount: 3000 }] });
    component.stage.set(3); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('تم تسجيل الدفعة بنجاح');
    expect(text).toContain('جمعية العيلة');
    expect(text).toContain('رصيد للشهر القادم');
    expect(text).toContain('تأخير 5 أيام');
  });

  it('shows the amount due now as the primary payment value without association selection', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({ personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', amountDueNow: 5000, totalRemaining: 25000, associations: [{ associationId: 'a1', associationName: 'جمعية العيلة', monthlyContribution: 5000, amountDueNow: 5000, totalRemaining: 25000, oldestUnpaidDueDate: '2026-01-10', credit: 0, paymentStatus: 'Outstanding' }] });
    component.stage.set(2); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('المبلغ المطلوب دفعه الآن');
    expect(text).toContain('دفع');
    expect(text).not.toContain('اختار الجمعيات');
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]').length).toBe(0);
  });
});
