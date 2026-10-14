import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { BusinessValidationError, PersonDto } from '../../core/models/api.models';
import { EditorMember, EditorPayout } from './association-editor.models';

@Component({
  selector: 'app-association-editor-summary', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="summary card" aria-labelledby="summary-title">
      <div class="summary-title"><span>✓</span><h2 id="summary-title">المراجعة المباشرة</h2></div>
      <dl>
        <div><dt>الأعضاء</dt><dd>{{ members.length }}</dd></div>
        <div><dt>إجمالي الحصص</dt><dd>{{ totalShares() }}</dd></div>
        <div><dt>التزام الأعضاء</dt><dd>{{ money(obligationTotal()) }}</dd></div>
        <div><dt>القبض المجدول للأعضاء</dt><dd>{{ money(scheduledTotal()) }}</dd></div>
        <div><dt>بدون اسم</dt><dd>{{ virtualCount('ReserveSlot') }}</dd></div>
        <div><dt>سلف</dt><dd>{{ virtualCount('AdvanceSlot') }}</dd></div>
      </dl>
      <div class="balance" [class.balance--ok]="isBalanced()">
        <strong>{{ isBalanced() ? 'متوازن مبدئيًا' : 'يحتاج مراجعة' }}</strong>
        <small>الخادم هو المرجع النهائي عند البدء.</small>
      </div>
      @if (errors.length > 0) {
        <section class="validation" role="alert" aria-label="أخطاء إعداد الجمعية">
          <h3>صحّح النقاط التالية</h3>
          <ul>@for (error of errors; track error.code + error.field + error.periodNumber) {
            <li><button type="button" (click)="focusField(error.field)">{{ error.periodNumber ? 'الشهر ' + error.periodNumber + ': ' : '' }}{{ error.message }}</button></li>
          }</ul>
        </section>
      }
    </aside>
  `,
  styles: [`
    .summary{position:sticky;top:1rem}.summary-title{display:flex;align-items:center;gap:.6rem}.summary-title span{display:grid;place-items:center;width:2rem;height:2rem;border-radius:50%;background:var(--brand-100,#dff1ea);color:var(--brand-800)}.summary h2{font-size:1.1rem;margin:0}.summary dl{margin:1rem 0}.summary dl div{display:flex;justify-content:space-between;gap:1rem;padding:.65rem 0;border-bottom:1px solid var(--line)}.summary dt{color:var(--ink-500)}.summary dd{margin:0;font-weight:800}.balance{padding:.8rem;border-radius:.75rem;background:#fff3d9;color:#755300}.balance--ok{background:#e1f3e9;color:#155c42}.balance small{display:block;margin-top:.2rem}.validation{margin-top:1rem;padding:.8rem;border-radius:.75rem;background:#fff0ee;color:#8b2525}.validation h3{font-size:.95rem;margin:0}.validation ul{padding-inline-start:1.2rem;margin:.5rem 0 0}.validation button{border:0;background:transparent;color:inherit;text-align:start;cursor:pointer;padding:.2rem 0}@media(max-width:980px){.summary{position:static}}
  `],
})
export class AssociationEditorSummaryComponent {
  @Input({ required: true }) members: readonly EditorMember[] = [];
  @Input({ required: true }) payouts: readonly EditorPayout[] = [];
  @Input({ required: true }) people: readonly PersonDto[] = [];
  @Input() contribution = 0;
  @Input() months = 0;
  @Input() errors: readonly BusinessValidationError[] = [];
  protected totalShares(): number { return this.members.reduce((sum, member) => sum + member.shareCount, 0); }
  protected obligationTotal(): number { return this.amountForShares(this.totalShares()); }
  protected scheduledTotal(): number {
    return this.amountForShares(this.payouts.filter((payout) => payout.recipientType === 'Membership').reduce((sum, payout) => sum + payout.shareCount, 0));
  }
  protected virtualCount(type: 'ReserveSlot' | 'AdvanceSlot'): number { return this.payouts.filter((payout) => payout.recipientType === type).length; }
  protected isBalanced(): boolean {
    return this.members.length > 0 && this.members.every((member) => this.payouts
      .filter((payout) => payout.recipientType === 'Membership' && payout.memberClientKey === member.clientKey)
      .reduce((sum, payout) => sum + payout.shareCount, 0) === member.shareCount)
      && Array.from({ length: this.months }, (_, index) => index + 1).every((period) => this.payouts.some((payout) => payout.periodNumber === period));
  }
  protected money(amount: number): string { return new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 }).format(amount); }
  protected focusField(field: string): void {
    const element = document.querySelector<HTMLElement>(`[data-field="${CSS.escape(field)}"]`) ?? document.getElementById(field);
    element?.focus();
    element?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
  private amountForShares(shares: number): number {
    return Math.round(this.contribution * 100) * this.months * Math.round(shares * 2) / 200;
  }
}
