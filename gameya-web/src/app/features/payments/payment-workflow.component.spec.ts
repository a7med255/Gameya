import { WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { PaymentDto, PaymentPageDto, PaymentPreviewDto } from '../../core/models/api.models';
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
  preview: WritableSignal<PaymentPreviewDto | null>;
  includeAdvance: WritableSignal<boolean>;
}

describe('PaymentWorkflowComponent', () => {
  let fixture: ComponentFixture<PaymentWorkflowComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentWorkflowComponent],
      providers: [
        { provide: PeopleApiService, useValue: { list: () => of({ items: [], page: 1, pageSize: 12, totalCount: 0 }) } },
        { provide: PaymentsApiService, useValue: { preview: () => of({} as PaymentPreviewDto) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        ApiErrorService, AssociationContextService, ConfirmService, NotificationService,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(PaymentWorkflowComponent);
  });

  it('renders the allocation returned by the backend, including association credit', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({
      personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', previousOverdue: 0, currentDue: 5000,
      amountDueNow: 5000, totalRemaining: 10000,
      dueItems: [{ associationId: 'a1', associationName: 'جمعية العيلة', periodId: 'period1', periodNumber: 1, dueDate: '2026-01-10', amount: 5000 }],
      associations: [{ associationId: 'a1', associationName: 'جمعية العيلة', monthlyContribution: 5000, amountDueNow: 5000, totalRemaining: 10000, oldestUnpaidDueDate: '2026-01-10', credit: 0, paymentStatus: 'Outstanding' }]
    });
    component.result.set({
      id: 'pay1', personId: 'p1', amount: 8000, method: 'Cash', paidAtUtc: '2026-01-15T13:30:00Z', entryType: 'Payment',
      allocations: [{ associationId: 'a1', periodId: 'period1', amount: 5000, dueDate: '2026-01-10', lateDays: 5 }],
      credits: [{ associationId: 'a1', amount: 3000 }]
    });
    component.stage.set(3); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('تم تسجيل الدفعة بنجاح');
    expect(text).toContain('جمعية العيلة');
    expect(text).toContain('رصيد للشهر القادم');
    expect(text).toContain('تأخير 5 أيام');
  });

  it('shows the amount due now as the primary payment value without association selection', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({
      personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', previousOverdue: 0, currentDue: 5000,
      amountDueNow: 5000, totalRemaining: 25000,
      dueItems: [{ associationId: 'a1', associationName: 'جمعية العيلة', periodId: 'period1', periodNumber: 1, dueDate: '2026-01-10', amount: 5000 }],
      associations: [{ associationId: 'a1', associationName: 'جمعية العيلة', monthlyContribution: 5000, amountDueNow: 5000, totalRemaining: 25000, oldestUnpaidDueDate: '2026-01-10', credit: 0, paymentStatus: 'Outstanding' }]
    });
    component.stage.set(2); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('المبلغ المطلوب دفعه الآن');
    expect(text).toContain('دفع');
    expect(text).not.toContain('اختار الجمعيات');
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]').length).toBe(0);
  });

  it('renders complete association breakdown table for selected person with individual monthly contributions and arrears', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({
      personId: 'p1', fullName: 'أحمد محمد', subscriberCode: 'SUB12345',
      previousOverdue: 1000, currentDue: 5000, amountDueNow: 6000, totalRemaining: 25000,
      currentYear: 2026, currentMonthNumber: 10, currentMonthLabel: 'أكتوبر 2026',
      dueItems: [],
      associations: [
        { associationId: 'a1', associationName: 'جمعية A', monthlyContribution: 1000, shareCount: 1, currentPeriodNumber: 1, currentRequired: 1000, currentPaid: 0, currentRemaining: 1000, previousArrears: 0, amountDueNow: 1000, totalRemaining: 5000, oldestUnpaidDueDate: '2026-10-10', credit: 0, paymentStatus: 'Unpaid', associationStatus: 'Active' },
        { associationId: 'a2', associationName: 'جمعية B', monthlyContribution: 2000, shareCount: 1, currentPeriodNumber: 2, currentRequired: 2000, currentPaid: 0, currentRemaining: 2000, previousArrears: 1000, amountDueNow: 3000, totalRemaining: 10000, oldestUnpaidDueDate: '2026-09-10', credit: 0, paymentStatus: 'PartiallyPaid', associationStatus: 'Active' },
        { associationId: 'a3', associationName: 'جمعية C', monthlyContribution: 2000, shareCount: 1, currentPeriodNumber: 1, currentRequired: 2000, currentPaid: 0, currentRemaining: 2000, previousArrears: 0, amountDueNow: 2000, totalRemaining: 10000, oldestUnpaidDueDate: '2026-10-10', credit: 0, paymentStatus: 'Unpaid', associationStatus: 'Active' }
      ]
    });
    component.stage.set(2); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('موقف المشترك في الجمعيات المشترك بها');
    expect(text).toContain('جمعية A');
    expect(text).toContain('جمعية B');
    expect(text).toContain('جمعية C');
    expect(text).toMatch(/١[٬,]٠٠٠|1[,.]000/);
    expect(text).toMatch(/٢[٬,]٠٠٠|2[,.]000/);
    expect(text).toContain('أكتوبر 2026');
  });

  it('renders dynamic month obligations and allows advance month selection', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({
      personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', previousOverdue: 0, currentDue: 5000,
      amountDueNow: 5000, totalRemaining: 20000, currentYear: 2026, currentMonthNumber: 10,
      currentMonthLabel: 'أكتوبر 2026',
      currentMonth: { year: 2026, month: 10, monthLabel: 'أكتوبر 2026', required: 5000, paid: 0, remaining: 5000, paymentStatus: 'Unpaid', items: [] },
      nextAdvanceMonth: { year: 2026, month: 11, monthLabel: 'نوفمبر 2026', required: 5000, paid: 0, remaining: 5000, paymentStatus: 'Unpaid', items: [] },
      dueItems: [], associations: []
    });
    component.stage.set(2); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('استحقاق أكتوبر 2026');
    expect(text).toContain('استحقاق نوفمبر 2026 — دفع مقدم');
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]').length).toBe(1);
  });

  it('renders backend authoritative preview with notices and proportional distribution', () => {
    const component = fixture.componentInstance as unknown as PaymentHarness;
    component.paymentPage.set({
      personId: 'p1', fullName: 'أحمد', subscriberCode: 'ABC12345', previousOverdue: 0, currentDue: 5000,
      amountDueNow: 5000, totalRemaining: 20000, currentMonthLabel: 'أكتوبر 2026',
      dueItems: [], associations: []
    });
    component.preview.set({
      personId: 'p1', amountReceived: 2500, allocatedAmount: 2500, creditAmount: 0,
      wasIntentReallocated: false, allocationNotice: null, previewToken: 'token123',
      allocations: [
        { associationId: 'a1', associationName: 'جمعية A', periodId: 'p1', periodNumber: 1, periodDate: '2026-10-01', dueDate: '2026-10-10', category: 'Current', required: 1000, paidBefore: 0, allocated: 500, remainingAfter: 500, paymentStatus: 'PartiallyPaid' },
        { associationId: 'a2', associationName: 'جمعية B', periodId: 'p2', periodNumber: 1, periodDate: '2026-10-01', dueDate: '2026-10-10', category: 'Current', required: 2000, paidBefore: 0, allocated: 1000, remainingAfter: 1000, paymentStatus: 'PartiallyPaid' },
        { associationId: 'a3', associationName: 'جمعية C', periodId: 'p3', periodNumber: 1, periodDate: '2026-10-01', dueDate: '2026-10-10', category: 'Current', required: 2000, paidBefore: 0, allocated: 1000, remainingAfter: 1000, paymentStatus: 'PartiallyPaid' }
      ],
      credits: []
    });
    component.stage.set(2); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('معاينة التوزيع المعتمدة قبل التأكيد');
    expect(text).toContain('جمعية A');
    expect(text).toContain('جمعية B');
    expect(text).toContain('جمعية C');
    expect(text).toContain('متطابق تمامًا مع المبلغ المستلم');
  });
});
