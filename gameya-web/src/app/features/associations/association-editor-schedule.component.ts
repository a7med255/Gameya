import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PersonDto } from '../../core/models/api.models';
import { EditorMember, EditorPayout, ScheduleChange } from './association-editor.models';

@Component({
  selector: 'app-association-editor-schedule',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="editor-section card" aria-labelledby="schedule-title">
      <div class="section-heading">
        <div><span class="section-index">٣</span><div><h2 id="schedule-title">جدول القبض</h2><p>الاقتراح الأولي يتبع ترتيب الأعضاء؛ الحصص الإضافية تظل لك لتوزيعها صراحةً.</p></div></div>
        <div class="schedule-actions">
          <button class="btn btn--secondary" type="button" [disabled]="members.length === 0" (click)="add.emit('Membership')">+ عضو</button>
          <button class="btn btn--ghost" type="button" (click)="add.emit('ReserveSlot')">+ بدون اسم</button>
          <button class="btn btn--ghost" type="button" (click)="add.emit('AdvanceSlot')">+ سلف</button>
        </div>
      </div>

      <div class="legend">
        <span><i class="dot dot--member"></i> عضو حقيقي</span>
        <span><i class="dot dot--reserve"></i> بدون اسم — حجز افتراضي</span>
        <span><i class="dot dot--advance"></i> سلف — ليس شخصًا ولا يدفع</span>
      </div>

      <div class="period-grid">
        @for (period of periods(); track period) {
          <article class="period-card">
            <header><strong>الشهر {{ period }}</strong><small>{{ entriesFor(period).length }} مستفيد</small></header>
            @if (entriesFor(period).length === 0) {
              <button class="empty-period" type="button" (click)="addToPeriod(period)">+ إضافة مستفيد</button>
            }
            @for (entry of entriesFor(period); track entry.index) {
              <div class="payout-row" [class.payout-row--virtual]="entry.value.recipientType !== 'Membership'">
                <span class="recipient-badge">{{ typeLabel(entry.value) }}</span>
                @if (entry.value.recipientType === 'Membership') {
                  <select class="control" aria-label="المستفيد" [ngModel]="entry.value.memberClientKey"
                    (ngModelChange)="patch(entry.index, { memberClientKey: $event })">
                    @for (member of members; track member.clientKey) { <option [value]="member.clientKey">{{ memberName(member) }}</option> }
                  </select>
                  <input class="control shares" aria-label="حصص القبض" type="number" min="0.5" step="0.5"
                    [ngModel]="entry.value.shareCount" (ngModelChange)="patch(entry.index, { shareCount: +$event })">
                } @else {
                  <select class="control" aria-label="المستفيد اللاحق" [ngModel]="entry.value.beneficiaryMemberClientKey ?? ''"
                    (ngModelChange)="patch(entry.index, { beneficiaryMemberClientKey: $event || null })">
                    <option value="">المستفيد اللاحق غير محدد</option>
                    @for (member of members; track member.clientKey) { <option [value]="member.clientKey">{{ memberName(member) }}</option> }
                  </select>
                  <span class="fixed-share">حصة واحدة</span>
                }
                <select class="control period-select" aria-label="نقل إلى شهر" [ngModel]="entry.value.periodNumber"
                  (ngModelChange)="patch(entry.index, { periodNumber: +$event })">
                  @for (target of periods(); track target) { <option [value]="target">شهر {{ target }}</option> }
                </select>
                <button class="icon-btn danger" type="button" aria-label="حذف التوزيع" (click)="remove.emit(entry.index)">×</button>
              </div>
            }
          </article>
        }
      </div>
    </section>
  `,
  styles: [`
    .section-heading,.section-heading>div,.schedule-actions,.legend,.period-card header,.payout-row{display:flex;align-items:center;gap:.75rem}.section-heading{justify-content:space-between;margin-bottom:1rem}.section-heading h2{margin:0;font-size:1.15rem}.section-heading p{margin:.2rem 0 0;color:var(--ink-500);font-size:.9rem}.section-index{display:grid;place-items:center;width:2rem;height:2rem;border-radius:.65rem;background:var(--brand-700);color:#fff;font-weight:800}.legend{padding:.65rem .8rem;background:var(--surface-muted,#f7f5ef);border-radius:.7rem;flex-wrap:wrap;font-size:.82rem;color:var(--ink-600)}.dot{display:inline-block;width:.55rem;height:.55rem;border-radius:50%;margin-inline-end:.3rem}.dot--member{background:var(--brand-600)}.dot--reserve{background:#bd8b17}.dot--advance{background:#6b57a8}.period-grid{display:grid;gap:.8rem;margin-top:1rem}.period-card{border:1px solid var(--line);border-radius:.9rem;padding:.8rem}.period-card header{justify-content:space-between;margin-bottom:.6rem}.period-card header small{color:var(--ink-500)}.payout-row{display:grid;grid-template-columns:7.5rem minmax(11rem,1fr) 7rem 7rem auto;padding:.6rem 0;border-top:1px solid var(--line)}.recipient-badge{font-weight:750}.payout-row--virtual .recipient-badge{color:#765716}.shares{max-width:7rem}.fixed-share{font-size:.85rem;color:var(--ink-500)}.empty-period{width:100%;border:1px dashed var(--line);background:transparent;border-radius:.7rem;padding:.75rem;color:var(--brand-700);cursor:pointer}.danger{color:var(--danger-700,#a12b2b)}@media(max-width:800px){.section-heading{align-items:flex-start;flex-direction:column}.schedule-actions{width:100%;overflow:auto}.payout-row{grid-template-columns:1fr auto}.payout-row .recipient-badge{grid-column:1/3}.period-select{width:100%}.legend{align-items:flex-start;flex-direction:column}}
    .payout-row>*{min-width:0}
    @media(max-width:800px){.schedule-actions{width:100%;display:grid;grid-template-columns:1fr;overflow:visible}.schedule-actions .btn{width:100%}.payout-row{grid-template-columns:1fr}.payout-row .recipient-badge{grid-column:auto}.payout-row .control,.period-select,.shares{width:100%;max-width:none}}
  `],
})
export class AssociationEditorScheduleComponent {
  @Input({ required: true }) members: readonly EditorMember[] = [];
  @Input({ required: true }) payouts: readonly EditorPayout[] = [];
  @Input({ required: true }) people: readonly PersonDto[] = [];
  @Input() months = 1;
  @Output() add = new EventEmitter<'Membership' | 'ReserveSlot' | 'AdvanceSlot'>();
  @Output() addAt = new EventEmitter<number>();
  @Output() change = new EventEmitter<ScheduleChange>();
  @Output() remove = new EventEmitter<number>();

  protected periods(): readonly number[] { return Array.from({ length: Math.max(1, this.months) }, (_, index) => index + 1); }
  protected entriesFor(period: number): readonly { index: number; value: EditorPayout }[] {
    return this.payouts.map((value, index) => ({ value, index })).filter((entry) => entry.value.periodNumber === period);
  }
  protected memberName(member: EditorMember): string {
    return member.fullName ?? this.people.find((person) => person.id === member.personId)?.fullName ?? 'عضو';
  }
  protected typeLabel(entry: EditorPayout): string {
    if (entry.recipientType === 'ReserveSlot') return 'بدون اسم';
    if (entry.recipientType === 'AdvanceSlot') return 'سلف';
    return 'عضو';
  }
  protected patch(index: number, patch: Partial<EditorPayout>): void {
    this.change.emit({ index, value: { ...this.payouts[index], ...patch } });
  }
  protected addToPeriod(period: number): void { this.addAt.emit(period); }
}
