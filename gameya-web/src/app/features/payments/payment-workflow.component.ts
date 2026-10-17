import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, EMPTY, finalize, startWith, switchMap, tap } from 'rxjs';
import { PAYMENT_METHOD_LABELS, PAYMENT_METHODS } from '../../core/constants/app.constants';
import { PaymentDto, PaymentMethod, PaymentPageDto, PaymentPreviewDto, PersonDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationContextService } from '../../core/services/association-context.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { PaymentsApiService } from '../../core/services/payments-api.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';

@Component({
  selector: 'app-payment-workflow', standalone: true,
  imports: [ReactiveFormsModule, MoneyPipe, ArabicDatePipe, LoadingStateComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page payment-page">
      <header class="page-header">
        <div>
          <h1 class="page-title">تسجيل دفعة</h1>
          <p class="page-subtitle">ابحث عن المشترك، راجع تفاصيل الجمعيات والاستحقاقات، وسجّل المبلغ مع معاينة فورية للتوزيع.</p>
        </div>
      </header>

      <section class="flow-steps" aria-label="خطوات تسجيل الدفعة">
        <span class="flow-step" [class.flow-step--active]="stage() >= 1">1 <small>اختيار المشترك والجمعيات</small></span>
        <span aria-hidden="true">←</span>
        <span class="flow-step" [class.flow-step--active]="stage() >= 2">2 <small>تحديد الاستحقاق والمبلغ</small></span>
        <span aria-hidden="true">←</span>
        <span class="flow-step" [class.flow-step--active]="stage() >= 3">3 <small>نتيجة التوزيع والإيصال</small></span>
      </section>

      <!-- Step 1: Select Person -->
      @if (stage() === 1) {
        <section class="card search-panel">
          <div class="search-box">
            <span class="search-box__icon">⌕</span>
            <label for="person-search">ابحث بالاسم أو كود المشترك</label>
            <input class="control" id="person-search" type="search" placeholder="ابدأ بكتابة الاسم أو الكود…" [formControl]="search" autocomplete="off">
          </div>
          @if (searching()) {
            <div class="stack" style="margin-top:1rem"><div class="skeleton skeleton-line"></div><div class="skeleton skeleton-line"></div></div>
          } @else if (search.value.trim().length > 0 && people().length === 0) {
            <p class="muted" style="margin-top:1rem;text-align:center">لا توجد نتائج مطابقة لـ "{{ search.value.trim() }}".</p>
          } @else {
            <div class="people-results">
              @for (person of people(); track person.id) {
                <button class="person-option" type="button" (click)="selectPerson(person)">
                  <span class="avatar">{{ person.fullName.charAt(0) }}</span>
                  <span>
                    <strong>{{ person.fullName }}</strong>
                    <small><span dir="ltr">{{ person.phoneNumber }}</span> · <span class="code">{{ person.subscriberCode }}</span></small>
                  </span>
                  <span aria-hidden="true">←</span>
                </button>
              }
            </div>
          }
        </section>
      }

      <!-- Step 2: Selected Person, Breakdown & Payment Form -->
      @if (stage() === 2) {
        @if (pageLoading()) {
          <app-loading-state />
        } @else if (paymentPage(); as data) {
          <!-- Person Identity Bar -->
          <section class="selected-person card">
            <div class="person-meta">
              <span class="avatar avatar--lg">{{ data.fullName.charAt(0) }}</span>
              <div>
                <span class="muted">المشترك المختار</span>
                <h2>{{ data.fullName }}</h2>
                <span class="code">{{ data.subscriberCode }}</span>
              </div>
            </div>
            <button class="btn btn--ghost" type="button" (click)="reset()">تغيير المشترك</button>
          </section>

          <!-- Association Breakdown Table (Requirement 2) -->
          <section class="card breakdown-section" style="margin-top:1rem">
            <div class="section-heading">
              <h3>موقف المشترك في الجمعيات المشترك بها</h3>
              <p class="muted">بيان تفصيلي بكل جمعية ذات صلة، والاشتراك الشهري، والمدفوع والمتبقي والمتأخرات</p>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>الجمعية</th>
                    <th>اشتراكه الشهري</th>
                    <th>الأسهم</th>
                    <th>الحالة</th>
                    <th>الفترة</th>
                    <th>استحقاق الشهر الحالي</th>
                    <th>المدفوع حالياً</th>
                    <th>المتبقي للشهر الحالي</th>
                    <th>المتأخرات السابقة</th>
                    <th>إجمالي المتبقي</th>
                    <th>حالة الاستحقاق</th>
                  </tr>
                </thead>
                <tbody>
                  @for (assoc of data.associations; track assoc.associationId) {
                    <tr>
                      <td><strong>{{ assoc.associationName }}</strong></td>
                      <td class="money">{{ assoc.monthlyContribution | money }}</td>
                      <td>{{ assoc.shareCount ?? 1 }}</td>
                      <td>
                        <span class="badge" [class.badge--success]="(assoc.associationStatus ?? 'Active') === 'Active'" [class.badge--warning]="assoc.associationStatus === 'Paused'">
                          {{ associationStatusLabel(assoc.associationStatus ?? 'Active') }}
                        </span>
                      </td>
                      <td>{{ assoc.currentPeriodNumber ? ('فترة ' + assoc.currentPeriodNumber) : '—' }}</td>
                      <td class="money">{{ (assoc.currentRequired ?? assoc.monthlyContribution) | money }}</td>
                      <td class="money">{{ (assoc.currentPaid ?? 0) | money }}</td>
                      <td class="money font-bold" [class.warning-text]="(assoc.currentRemaining ?? assoc.amountDueNow) > 0">{{ (assoc.currentRemaining ?? assoc.amountDueNow) | money }}</td>
                      <td class="money" [class.danger-text]="(assoc.previousArrears ?? 0) > 0">{{ (assoc.previousArrears ?? 0) | money }}</td>
                      <td class="money font-bold">{{ assoc.totalRemaining | money }}</td>
                      <td>
                        <span class="badge" [class.badge--success]="assoc.paymentStatus === 'Paid'" [class.badge--warning]="assoc.paymentStatus === 'PartiallyPaid' || assoc.paymentStatus === 'Outstanding'" [class.badge--danger]="assoc.paymentStatus === 'Unpaid'">
                          {{ paymentStatusLabel(assoc.paymentStatus) }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>

          <!-- Step Two: Monthly Obligations Breakdown (Requirements 3 & 4) -->
          <section class="card obligations-card" style="margin-top:1rem">
            <div class="section-heading">
              <h3>الاستحقاقات الشهرية (تحديد أولوية الدفع)</h3>
              <p class="muted">الأولوية الإلزامية للمتأخرات الأقدم، ثم استحقاق الشهر الحالي، ثم الدفع المقدم إن وُجد.</p>
            </div>

            <div class="obligation-items">
              <!-- A. Previous Arrears -->
              <div class="obligation-item obligation-item--priority" [class.obligation-item--settled]="data.previousOverdue === 0">
                <div class="obligation-item__title">
                  <span class="check-icon" [class.check-icon--active]="data.previousOverdue > 0">✓</span>
                  <div>
                    <strong>المتأخرات السابقة</strong>
                    <small class="muted">استحقاقات من أشهر سابقة لم تُسدد بالكامل (أولوية مطلقة في التوزيع)</small>
                  </div>
                </div>
                <strong class="money obligation-item__amount" [class.danger-text]="data.previousOverdue > 0">
                  {{ data.previousOverdue | money }}
                </strong>
              </div>
              @if ((data.arrearMonths || []).length > 0) {
                <div class="arrears-details">
                  @for (m of data.arrearMonths; track m.year + '-' + m.month) {
                    <div class="arrear-month-row">
                      <span>متأخرات {{ m.monthLabel }}</span>
                      <strong class="money danger-text">{{ m.remaining | money }}</strong>
                    </div>
                  }
                </div>
              }

              <!-- B. Current Month with dynamic name and year -->
              <div class="obligation-item" [class.obligation-item--settled]="data.currentDue === 0">
                <div class="obligation-item__title">
                  <span class="check-icon check-icon--active">✓</span>
                  <div>
                    <strong>استحقاق {{ (data.currentMonthLabel || 'الشهر الحالي') || 'الشهر الحالي' }}</strong>
                    <small class="muted">
                      @if (data.currentMonth; as cur) {
                        المطلوب: {{ cur.required | money }} · المدفوع: {{ cur.paid | money }} · 
                        <span class="badge badge--sm" [class.badge--success]="cur.paymentStatus === 'Paid'" [class.badge--warning]="cur.paymentStatus === 'PartiallyPaid'" [class.badge--danger]="cur.paymentStatus === 'Unpaid'">
                          {{ paymentStatusLabel(cur.paymentStatus) }}
                        </span>
                      } @else {
                        المتبقي: {{ data.currentDue | money }}
                      }
                    </small>
                  </div>
                </div>
                <strong class="money obligation-item__amount" [class.warning-text]="data.currentDue > 0">
                  {{ data.currentDue | money }}
                </strong>
              </div>

              <!-- C. Advance Payment (if eligible next month exists) -->
              @if (data.nextAdvanceMonth; as nextMonth) {
                <div class="obligation-item obligation-item--advance" [class.obligation-item--selected]="includeAdvance()">
                  <div class="obligation-item__title">
                    <label class="advance-checkbox-label" for="advance-check">
                      <input id="advance-check" type="checkbox" [checked]="includeAdvance()" (change)="toggleAdvance($event)">
                      <div>
                        <strong>استحقاق {{ nextMonth.monthLabel }} — دفع مقدم</strong>
                        <small class="muted">الدفع مقدماً للشهر القادم بعد سداد المستحقات السابقة والحالية</small>
                      </div>
                    </label>
                  </div>
                  <strong class="money obligation-item__amount">
                    {{ nextMonth.remaining | money }}
                  </strong>
                </div>

                @if (includeAdvance()) {
                  <div class="advance-subform field">
                    <label for="advance-amount-input">مبلغ مخصص للشهر القادم (اختياري)</label>
                    <input class="control" id="advance-amount-input" type="number" inputmode="decimal" min="0.01" step="0.01"
                      [formControl]="advanceAmountControl" placeholder="اتركه فارغاً لدفع المطلوب بالكامل ({{ nextMonth.remaining }} جنيه)">
                    <span class="field__hint">يمكنك دفع جزء محدد من استحقاق {{ nextMonth.monthLabel }}، وسيتم حفظ أي متبقٍ كالتزام للشهر القادم.</span>
                  </div>
                }
              }
            </div>
          </section>

          <!-- Payment Form & Amount Input -->
          <form class="card payment-form payment-form--focused" style="margin-top:1rem" [formGroup]="form" (ngSubmit)="confirmAndRecord()">
            <div class="due-now">
              <span>المبلغ المطلوب دفعه الآن</span>
              <strong class="payment-total money">{{ data.amountDueNow | money }}</strong>
              @if (oldestDueDate()) {
                <small>يشمل أقدم استحقاق بتاريخ {{ oldestDueDate() | arabicDate }}</small>
              }
            </div>

            <dl class="due-breakdown">
              <div><dt>متأخرات سابقة</dt><dd class="money">{{ data.previousOverdue | money }}</dd></div>
              <div><dt>استحقاق حالي</dt><dd class="money">{{ data.currentDue | money }}</dd></div>
              <div><dt>الإجمالي المطلوب الآن</dt><dd class="money">{{ data.amountDueNow | money }}</dd></div>
            </dl>

            <div class="quick-chips">
              <span class="muted" style="align-self:center;font-size:0.85rem">مبالغ سريعة:</span>
              @if (data.amountDueNow > 0) {
                <button class="chip" type="button" (click)="setQuickAmount(data.amountDueNow)">
                  المطلوب الآن ({{ formatAmount(data.amountDueNow) }} جنيه)
                </button>
              }
              @if (data.nextAdvanceMonth && (data.amountDueNow + data.nextAdvanceMonth.remaining) > data.amountDueNow) {
                <button class="chip" type="button" (click)="setQuickAmountWithAdvance(data.amountDueNow + data.nextAdvanceMonth.remaining)">
                  الآن + القادم ({{ formatAmount(data.amountDueNow + data.nextAdvanceMonth.remaining) }} جنيه)
                </button>
              }
            </div>

            <div class="field">
              <label for="amount">المبلغ المستلم</label>
              <input class="control control--lg" id="amount" type="number" inputmode="decimal" min="0.01" step="0.01" formControlName="amount" placeholder="0.00">
              @if (form.controls.amount.invalid && form.controls.amount.touched) {
                <span class="field__error">المبلغ يجب أن يكون أكبر من صفر.</span>
              }
              <span class="field__hint">الخادم يوزع المبلغ تلقائيًا على الجمعيات المستحقة بالتناسب العادل، مع أولوية الأقدم أولاً.</span>
            </div>

            <fieldset class="method-field">
              <legend>طريقة الدفع</legend>
              <div class="method-grid">
                @for (method of methods; track method) {
                  <label>
                    <input type="radio" formControlName="method" [value]="method">
                    <span>{{ methodLabels[method] }}</span>
                  </label>
                }
              </div>
            </fieldset>

            <!-- Authoritative Server Preview (Requirement 10) -->
            @if (previewLoading()) {
              <div class="preview-loading card">
                <div class="skeleton skeleton-line"></div>
                <div class="skeleton skeleton-line"></div>
              </div>
            } @else if (preview(); as p) {
              <section class="allocation-preview card">
                <div class="preview-head">
                  <h3>معاينة التوزيع المعتمدة قبل التأكيد</h3>
                  <span class="badge badge--success">محسوبة من الخادم</span>
                </div>
                <p class="muted">المبلغ المدفوع: <strong class="money">{{ p.amountReceived | money }}</strong></p>

                @if (p.allocationNotice) {
                  <div class="notice-box" [class.notice-box--warning]="p.wasIntentReallocated" [class.notice-box--info]="!p.wasIntentReallocated">
                    {{ p.allocationNotice }}
                  </div>
                }

                <div class="preview-groups">
                  <h4>سيتم التوزيع كالتالي:</h4>
                  @if (p.allocations.length === 0 && p.credits.length === 0) {
                    <p class="muted">لا توجد مبالغ مخصصة.</p>
                  } @else {
                    <div class="preview-alloc-list">
                      @for (item of p.allocations; track item.associationId + item.periodId) {
                        <div class="preview-alloc-row">
                          <div class="preview-alloc-row__info">
                            <strong>{{ item.associationName }}</strong>
                            <div class="muted">
                              <span>{{ item.category === 'Arrears' ? 'متأخرات' : item.category === 'Current' ? ('استحقاق ' + (data.currentMonthLabel || 'الشهر الحالي')) : 'دفع مقدم' }}</span>
                              · استحقاق {{ item.dueDate | arabicDate }}
                            </div>
                          </div>
                          <div class="preview-alloc-row__amount">
                            <strong class="money font-bold">+{{ item.allocated | money }}</strong>
                            <small class="muted">المتبقي: {{ item.remainingAfter | money }} ({{ paymentStatusLabel(item.paymentStatus) }})</small>
                          </div>
                        </div>
                      }
                    </div>
                  }

                  @for (credit of p.credits; track credit.associationId) {
                    <div class="preview-alloc-row credit-row">
                      <div>
                        <strong>رصيد للمستقبل</strong>
                        <div class="muted">{{ credit.associationName }}</div>
                      </div>
                      <strong class="money font-bold">+{{ credit.amount | money }}</strong>
                    </div>
                  }
                </div>

                <div class="reconcile-footer">
                  <span>إجمالي التوزيع الفعلي: <strong class="money">{{ (p.allocatedAmount + p.creditAmount) | money }}</strong></span>
                  <span class="reconcile-check">✓ متطابق تمامًا مع المبلغ المستلم</span>
                </div>
              </section>
            }

            <button class="btn btn--primary btn--block pay-due" type="submit" [disabled]="saving() || form.invalid">
              {{ saving() ? 'جاري التسجيل…' : 'دفع ' + formatAmount(currentAmount() || data.amountDueNow) + ' جنيه' }}
            </button>
          </form>
        }
      }

      <!-- Step 3: Success Result & Receipt -->
      @if (stage() === 3 && result(); as payment) {
        <section class="success-hero card">
          <div class="state__icon">✓</div>
          <div>
            <span class="muted">تم تسجيل الدفعة بنجاح</span>
            <h2 class="money">{{ payment.amount | money }}</h2>
            <p>{{ payment.paidAtUtc | arabicDate:true }} · {{ methodLabels[payment.method] }}</p>
          </div>
        </section>

        <section class="card" style="margin-top:1rem">
          <h2 class="section-title">توزيع الدفعة المعتمد</h2>
          <p class="muted">التوزيع المحفوظ نهائياً في قاعدة البيانات.</p>
          @if (payment.allocations.length === 0 && payment.credits.length === 0) {
            <p class="muted">لم يتم تخصيص جزء على فترات مستحقة.</p>
          } @else {
            <div class="allocation-list">
              @for (allocation of payment.allocations; track allocation.associationId + allocation.periodId) {
                <div class="allocation-row">
                  <div>
                    <strong>{{ associationName(allocation.associationId) }}</strong>
                    <div class="muted">
                      استحقاق {{ allocation.dueDate | arabicDate }}
                      @if (allocation.lateDays > 0) {
                        · <span class="danger-text">تأخير {{ allocation.lateDays }} أيام</span>
                      }
                    </div>
                  </div>
                  <strong class="money">{{ allocation.amount | money }}</strong>
                </div>
              }
              @for (credit of payment.credits; track credit.associationId) {
                <div class="allocation-row credit-row">
                  <div>
                    <strong>رصيد للشهر القادم</strong>
                    <div class="muted">{{ associationName(credit.associationId) }}</div>
                  </div>
                  <strong class="money">{{ credit.amount | money }}</strong>
                </div>
              }
            </div>
          }
        </section>

        <div class="inline" style="justify-content:center;margin-top:1.5rem;gap:1rem">
          <button class="btn btn--primary" type="button" (click)="newPayment()">تسجيل دفعة جديدة</button>
          <button class="btn btn--secondary" type="button" (click)="backToPerson()">دفعة أخرى لنفس المشترك</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .payment-page { max-width: 980px; }
    .flow-steps { display: flex; align-items: center; justify-content: center; gap: .65rem; margin-bottom: 1.5rem; color: var(--ink-500); }
    .flow-step { display: flex; align-items: center; gap: .4rem; min-width: 2rem; height: 2rem; padding-inline: .75rem; border-radius: 999px; background: #e7eeec; font-weight: 800; font-size: 0.9rem; }
    .flow-step small { display: none; white-space: nowrap; }
    .flow-step--active { color: #fff; background: var(--brand-700); }
    .search-panel { max-width: 48rem; margin: auto; }
    .search-box label { display: block; font-weight: 800; margin-bottom: .5rem; }
    .search-box__icon { top: auto; bottom: .8rem; }
    .people-results { display: grid; gap: .5rem; margin-top: 1rem; }
    .person-option { width: 100%; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: .8rem; text-align: start; padding: .8rem; border: 1px solid var(--line); border-radius: .8rem; background: #fff; cursor: pointer; transition: all .15s ease; }
    .person-option:hover { background: var(--brand-50); border-color: var(--brand-600); }
    .person-option small { display: block; color: var(--ink-500); }
    .selected-person { display: flex; justify-content: space-between; align-items: center; }
    .person-meta { display: flex; align-items: center; gap: 1rem; }
    .avatar--lg { width: 3.5rem; height: 3.5rem; font-size: 1.5rem; font-weight: 800; }
    .selected-person h2 { margin: .1rem 0; font-size: 1.4rem; }
    .breakdown-section .section-heading, .obligations-card .section-heading { margin-bottom: 1rem; }
    .section-heading h3 { margin: 0; font-size: 1.15rem; font-weight: 800; }
    .section-heading p { margin: .25rem 0 0; }
    .table-container { overflow-x: auto; -webkit-overflow-scrolling: touch; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; white-space: nowrap; }
    .data-table th, .data-table td { padding: .75rem .6rem; text-align: start; border-bottom: 1px solid var(--line); }
    .data-table th { background: #f8faf9; font-weight: 800; color: var(--ink-600); }
    .data-table tr:hover td { background: var(--brand-50); }
    .badge--sm { font-size: 0.75rem; padding: 0.15rem 0.5rem; }
    .obligation-items { display: grid; gap: .75rem; }
    .obligation-item { display: flex; justify-content: space-between; align-items: center; padding: 1rem; border: 1px solid var(--line); border-radius: .8rem; background: #fff; }
    .obligation-item--priority { border-inline-start: 4px solid var(--brand-700); background: #fafdfc; }
    .obligation-item--settled { opacity: 0.65; }
    .obligation-item--advance { border-style: dashed; }
    .obligation-item--selected { border-color: var(--brand-600); background: var(--brand-50); }
    .obligation-item__title { display: flex; align-items: center; gap: .75rem; }
    .check-icon { display: inline-grid; place-items: center; width: 1.5rem; height: 1.5rem; border-radius: 50%; background: #e2e8e5; color: #888; font-weight: 800; font-size: 0.85rem; }
    .check-icon--active { background: var(--brand-700); color: #fff; }
    .advance-checkbox-label { display: flex; align-items: center; gap: .75rem; cursor: pointer; }
    .advance-checkbox-label input[type="checkbox"] { width: 1.25rem; height: 1.25rem; accent-color: var(--brand-700); cursor: pointer; }
    .arrears-details { background: #fdf6f6; border-radius: .6rem; padding: .6rem 1rem; margin-inline-start: 2.25rem; display: grid; gap: .4rem; font-size: 0.88rem; }
    .arrear-month-row { display: flex; justify-content: space-between; align-items: center; }
    .advance-subform { margin-inline-start: 2.25rem; margin-top: .5rem; padding: .75rem 1rem; background: var(--brand-50); border-radius: .6rem; }
    .payment-form--focused { display: grid; gap: 1.25rem; max-width: 42rem; margin-inline: auto !important; }
    .due-now { display: grid; gap: .35rem; text-align: center; padding: 1.25rem; border-radius: 1rem; background: var(--brand-50); color: var(--brand-900); }
    .due-now > span { font-weight: 800; }
    .due-now small { color: var(--ink-500); }
    .due-breakdown { display: grid; grid-template-columns: repeat(3, 1fr); gap: .5rem; margin: 0; }
    .due-breakdown > div { padding: .75rem; border: 1px solid var(--line); border-radius: .7rem; text-align: center; }
    .due-breakdown dt { color: var(--ink-500); font-size: 0.85rem; }
    .due-breakdown dd { margin: .25rem 0 0; font-weight: 800; }
    .quick-chips { display: flex; flex-wrap: wrap; gap: .5rem; }
    .chip { padding: .45rem .85rem; border: 1px solid var(--line); border-radius: 999px; background: #fff; cursor: pointer; font-weight: 700; font-size: 0.85rem; color: var(--brand-900); transition: all .15s ease; }
    .chip:hover { background: var(--brand-100); border-color: var(--brand-600); }
    .payment-total { display: block; font-size: 2.35rem; font-weight: 900; }
    .pay-due { font-size: 1.15rem; min-height: 3.5rem; font-weight: 800; }
    .method-field { border: 0; padding: 0; margin: 0; }
    .method-field legend { font-weight: 700; margin-bottom: .45rem; }
    .method-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: .5rem; }
    .method-grid input { position: absolute; opacity: 0; }
    .method-grid span { min-height: 2.8rem; display: grid; place-items: center; border: 1px solid var(--line); border-radius: .65rem; font-weight: 700; cursor: pointer; transition: all .15s; }
    .method-grid label:has(input:checked) span { color: var(--brand-900); background: var(--brand-100); border-color: var(--brand-600); }
    .preview-card { padding: 1.25rem; }
    .allocation-preview { display: grid; gap: .75rem; background: #fbfdfc; border: 1px solid var(--brand-200); border-radius: 1rem; padding: 1.25rem; }
    .preview-head { display: flex; justify-content: space-between; align-items: center; }
    .preview-head h3 { margin: 0; font-size: 1.15rem; font-weight: 800; }
    .notice-box { padding: .75rem 1rem; border-radius: .65rem; font-size: 0.9rem; font-weight: 600; line-height: 1.5; }
    .notice-box--warning { background: #fff8e6; border: 1px solid #ffd591; color: #873800; }
    .notice-box--info { background: #e6f7ff; border: 1px solid #91d5ff; color: #0050b3; }
    .preview-groups h4 { margin: .5rem 0 .4rem; font-size: 0.95rem; color: var(--ink-700); }
    .preview-alloc-list { display: grid; gap: .4rem; }
    .preview-alloc-row { display: flex; justify-content: space-between; align-items: center; padding: .65rem .85rem; background: #fff; border: 1px solid var(--line); border-radius: .6rem; }
    .preview-alloc-row__info strong { display: block; font-size: 0.95rem; }
    .preview-alloc-row__info .muted { font-size: 0.8rem; }
    .preview-alloc-row__amount { text-align: end; }
    .preview-alloc-row__amount small { display: block; font-size: 0.8rem; }
    .credit-row { background: var(--brand-50); border-color: var(--brand-200); }
    .reconcile-footer { display: flex; justify-content: space-between; align-items: center; padding-top: .75rem; border-top: 1px dashed var(--line); font-size: 0.9rem; }
    .reconcile-check { color: #237804; font-weight: 700; }
    .success-hero { display: flex; align-items: center; justify-content: center; gap: 1.25rem; text-align: start; padding: 2rem; }
    .success-hero .state__icon { margin: 0; width: 4rem; height: 4rem; font-size: 2rem; }
    .success-hero h2 { font-size: 2.2rem; margin: .2rem 0; }
    .success-hero p { margin: 0; color: var(--ink-500); }
    .allocation-list { display: grid; }
    .allocation-row { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 1rem; border-block-start: 1px solid var(--line); }
    .font-bold { font-weight: 800; }
    .danger-text { color: #cf1322; }
    .warning-text { color: #d46b08; }
    @media(min-width: 768px) { .flow-step small { display: inline; } }
    @media(max-width: 600px) {
      .method-grid, .due-breakdown { grid-template-columns: 1fr; }
      .selected-person { align-items: flex-start; flex-direction: column; gap: 1rem; }
      .allocation-row { align-items: flex-start; flex-direction: column; }
      .allocation-row > strong { align-self: flex-end; }
      .reconcile-footer { flex-direction: column; align-items: flex-start; gap: .4rem; }
    }
  `],
})
export class PaymentWorkflowComponent {
  private readonly peopleApi = inject(PeopleApiService);
  private readonly paymentsApi = inject(PaymentsApiService);
  private readonly associationContext = inject(AssociationContextService);
  private readonly notifications = inject(NotificationService);
  private readonly errors = inject(ApiErrorService);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly initialPersonId = inject(ActivatedRoute).snapshot.queryParamMap.get('personId');

  protected readonly stage = signal<1 | 2 | 3>(1);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly people = signal<readonly PersonDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly pageLoading = signal(false);
  protected readonly paymentPage = signal<PaymentPageDto | null>(null);
  protected readonly includeAdvance = signal(false);
  protected readonly advanceAmountControl = new FormControl<number | null>(null, [Validators.min(0.01)]);
  protected readonly preview = signal<PaymentPreviewDto | null>(null);
  protected readonly previewLoading = signal(false);
  protected readonly saving = signal(false);
  protected readonly result = signal<PaymentDto | null>(null);
  protected readonly methods = PAYMENT_METHODS;
  protected readonly methodLabels = PAYMENT_METHOD_LABELS;

  protected readonly form = new FormGroup({
    amount: new FormControl<number | null>(null, [Validators.required, Validators.min(0.01)]),
    method: new FormControl<PaymentMethod>('Cash', { nonNullable: true })
  });

  constructor() {
    this.search.valueChanges.pipe(
      startWith(''),
      debounceTime(350),
      distinctUntilChanged(),
      tap(() => this.searching.set(true)),
      switchMap((search) => this.peopleApi.list(search, 1, 12).pipe(catchError(() => EMPTY))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => {
      this.people.set(result.items);
      this.searching.set(false);
    });

    this.form.controls.amount.valueChanges.pipe(
      debounceTime(300),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.fetchPreview());

    this.advanceAmountControl.valueChanges.pipe(
      debounceTime(300),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      if (this.includeAdvance()) this.fetchPreview();
    });

    if (this.initialPersonId) this.loadPersonById(this.initialPersonId);
  }

  protected selectPerson(person: PersonDto): void {
    this.loadPaymentPage(person.id);
  }

  protected oldestDueDate(): string | null {
    return this.paymentPage()?.associations
      .map((x) => x.oldestUnpaidDueDate)
      .filter((x): x is string => !!x)
      .sort()[0] ?? null;
  }

  protected formatAmount(amount: number): string {
    return new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 }).format(amount);
  }

  protected currentAmount(): number {
    return this.form.controls.amount.value ?? 0;
  }

  protected setQuickAmount(amount: number): void {
    this.form.controls.amount.setValue(amount);
  }

  protected setQuickAmountWithAdvance(amount: number): void {
    this.includeAdvance.set(true);
    this.form.controls.amount.setValue(amount);
  }

  protected toggleAdvance(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.includeAdvance.set(checked);
    const page = this.paymentPage();
    if (page && checked) {
      const advanceDue = page.nextAdvanceMonth?.remaining ?? 0;
      const current = this.form.controls.amount.value ?? 0;
      if (current === page.amountDueNow || current === 0) {
        this.form.controls.amount.setValue(page.amountDueNow + advanceDue);
      }
    }
    this.fetchPreview();
  }

  protected associationName(id: string): string {
    return this.paymentPage()?.associations.find((x) => x.associationId === id)?.associationName ?? 'الجمعية';
  }

  protected paymentStatusLabel(status: string): string {
    switch (status) {
      case 'Paid': return 'مدفوع بالكامل';
      case 'PartiallyPaid': return 'مدفوع جزئيًا';
      case 'Unpaid': return 'غير مدفوع';
      case 'Outstanding': return 'مستحق';
      default: return status;
    }
  }

  protected associationStatusLabel(status: string): string {
    switch (status) {
      case 'Active': return 'نشطة';
      case 'Paused': return 'متوقفة مؤقتًا';
      case 'Completed': return 'مكتملة';
      case 'Cancelled': return 'ملغاة';
      case 'Draft': return 'مسودة';
      default: return status;
    }
  }

  protected fetchPreview(): void {
    const page = this.paymentPage();
    const amount = this.currentAmount();
    if (!page || amount <= 0 || typeof this.paymentsApi.preview !== 'function') {
      this.preview.set(null);
      return;
    }
    this.previewLoading.set(true);
    const advanceMonth = this.includeAdvance() ? page.nextAdvanceMonth : null;
    this.paymentsApi.preview({
      personId: page.personId,
      amount,
      advanceYear: advanceMonth?.year ?? null,
      advanceMonth: advanceMonth?.month ?? null,
      advanceAmount: this.includeAdvance() ? (this.advanceAmountControl.value || null) : null
    }).pipe(
      finalize(() => this.previewLoading.set(false)),
      catchError(() => {
        this.preview.set(null);
        return EMPTY;
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((p) => this.preview.set(p));
  }

  protected async confirmAndRecord(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const page = this.paymentPage();
    const amount = this.currentAmount();
    if (!page || amount <= 0 || this.saving()) return;

    if (!await this.confirm.ask({
      title: 'تأكيد تسجيل الدفعة',
      message: `هل أنت متأكد من تسجيل دفعة بقيمة ${this.formatAmount(amount)} جنيه؟ التوزيع سيتم تلقائيًا وفق قاعدة الأقدم فالأقدم.`,
      confirmLabel: 'تسجيل الدفعة'
    })) return;

    this.saving.set(true);
    const advanceMonth = this.includeAdvance() ? page.nextAdvanceMonth : null;
    this.paymentsApi.record({
      personId: page.personId,
      amount,
      method: this.form.controls.method.value,
      advanceYear: advanceMonth?.year ?? null,
      advanceMonth: advanceMonth?.month ?? null,
      advanceAmount: this.includeAdvance() ? (this.advanceAmountControl.value || null) : null,
      previewToken: this.preview()?.previewToken ?? null,
      idempotencyKey: crypto.randomUUID()
    }).pipe(
      finalize(() => this.saving.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (payment) => {
        this.result.set(payment);
        this.stage.set(3);
        this.notifications.success('تم تسجيل الدفعة بنجاح.');
      },
      error: (error: unknown) => this.notifications.error(this.errors.message(error))
    });
  }

  protected reset(): void {
    this.paymentPage.set(null);
    this.people.set([]);
    this.search.setValue('');
    this.stage.set(1);
    this.includeAdvance.set(false);
    this.advanceAmountControl.reset();
    this.preview.set(null);
    this.form.reset({ amount: null, method: 'Cash' });
  }

  protected newPayment(): void {
    this.reset();
    this.result.set(null);
  }

  protected backToPerson(): void {
    const page = this.paymentPage();
    if (!page) return;
    this.result.set(null);
    this.includeAdvance.set(false);
    this.advanceAmountControl.reset();
    this.preview.set(null);
    this.form.reset({ amount: null, method: 'Cash' });
    this.stage.set(2);
    this.loadPaymentPage(page.personId);
  }

  private loadPersonById(id: string): void {
    this.loadPaymentPage(id);
  }

  private loadPaymentPage(id: string): void {
    this.pageLoading.set(true);
    this.stage.set(2);
    this.paymentsApi.page(id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (page) => {
        this.paymentPage.set(page);
        this.form.controls.amount.setValue(page.amountDueNow || null);
        this.associationContext.setAccessible(page.associations.map((x) => x.associationId));
        this.pageLoading.set(false);
        this.fetchPreview();
      },
      error: (error: unknown) => {
        this.notifications.error(this.errors.message(error));
        this.pageLoading.set(false);
        this.stage.set(1);
      }
    });
  }
}
