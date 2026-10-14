import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PersonDto } from '../../core/models/api.models';
import { EditorMember, EditorPayout, MemberSharesChange } from './association-editor.models';

@Component({
  selector: 'app-association-editor-members',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="editor-section card" aria-labelledby="members-title">
      <div class="section-heading">
        <div><span class="section-index">٢</span><h2 id="members-title">الأعضاء والحصص</h2></div>
        <span class="badge">{{ members.length }} أعضاء</span>
      </div>

      <div class="member-adder">
        <div class="field grow">
          <label for="personSearch">بحث بالاسم أو الهاتف أو كود المشترك</label>
          <input id="personSearch" class="control" type="search" [(ngModel)]="personSearch"
            placeholder="اكتب للبحث في الأشخاص المسجلين">
          <label for="existingPerson">إضافة شخص مسجل</label>
          <select id="existingPerson" class="control" [(ngModel)]="selectedPersonId">
            <option value="">اختر شخصًا…</option>
            @for (person of availablePeople(); track person.id) {
              <option [value]="person.id">{{ person.fullName }} — {{ person.phoneNumber }}</option>
            }
          </select>
        </div>
        <button class="btn btn--secondary" type="button" [disabled]="!selectedPersonId"
          (click)="addSelected()">إضافة</button>
      </div>

      <details class="inline-create">
        <summary>+ إنشاء شخص جديد داخل الجمعية</summary>
        <div class="inline-create__form">
          <div class="field"><label for="newPersonName">الاسم الكامل</label><input id="newPersonName" class="control" [(ngModel)]="newName"></div>
          <div class="field"><label for="newPersonPhone">رقم الهاتف</label><input id="newPersonPhone" class="control" dir="ltr" [(ngModel)]="newPhone"></div>
          <button class="btn btn--secondary" type="button" [disabled]="!newName.trim() || !newPhone.trim()"
            (click)="addInline()">إضافة للقائمة</button>
        </div>
      </details>

      @if (members.length === 0) {
        <div class="empty-inline">ابدأ بإضافة الأعضاء. الشخص الافتراضي «سلف» لا يُضاف هنا.</div>
      } @else {
        <div class="member-list" aria-label="ترتيب أعضاء الجمعية">
          @for (member of members; track member.clientKey; let index = $index) {
            <article class="member-card" draggable="true" (dragstart)="draggedIndex = index"
              (dragover)="$event.preventDefault()" (drop)="dropAt(index)">
              <span class="drag-handle" aria-hidden="true">⠿</span>
              <div class="member-identity">
                <strong>{{ displayName(member) }}</strong>
                <small>{{ member.personId ? phoneFor(member.personId) : member.phoneNumber }}</small>
              </div>
              <div class="field compact">
                <label [for]="'shares-' + index">الحصص</label>
                <input class="control" [id]="'shares-' + index" type="number" min="0.5" step="0.5"
                  [ngModel]="member.shareCount" (ngModelChange)="sharesChange.emit({ clientKey: member.clientKey, shareCount: +$event })">
              </div>
              <div class="member-money"><small>الالتزام الشهري</small><strong>{{ monthlyValue(member.shareCount) }}</strong></div>
              <div class="member-schedule">
                <small>أدوار الاستلام: {{ payoutPeriods(member.clientKey) }}</small>
                <strong>{{ payoutValue(member.clientKey) }}</strong>
              </div>
              <div class="row-actions">
                <button class="icon-btn" type="button" aria-label="تحريك لأعلى" [disabled]="index === 0" (click)="move.emit({ from: index, to: index - 1 })">↑</button>
                <button class="icon-btn" type="button" aria-label="تحريك لأسفل" [disabled]="index === members.length - 1" (click)="move.emit({ from: index, to: index + 1 })">↓</button>
                <button class="icon-btn danger" type="button" aria-label="حذف العضو" (click)="remove.emit(member.clientKey)">×</button>
              </div>
            </article>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .section-heading,.section-heading>div,.member-adder,.inline-create__form,.member-card,.row-actions{display:flex;align-items:center;gap:.75rem}.section-heading{justify-content:space-between;margin-bottom:1rem}.section-heading h2{margin:0;font-size:1.15rem}.section-index{display:grid;place-items:center;width:2rem;height:2rem;border-radius:.65rem;background:var(--brand-700);color:#fff;font-weight:800}.grow{flex:1}.member-adder{align-items:end}.inline-create{margin:1rem 0;border:1px dashed var(--line);border-radius:.8rem;padding:.8rem}.inline-create summary{cursor:pointer;color:var(--brand-700);font-weight:700}.inline-create__form{align-items:end;margin-top:.8rem}.inline-create__form .field{flex:1}.member-list{display:grid;gap:.65rem}.member-card{border:1px solid var(--line);border-radius:.9rem;padding:.8rem;background:#fff}.member-card:hover{border-color:var(--brand-500);box-shadow:0 6px 18px rgb(15 86 74 / .08)}.drag-handle{cursor:grab;color:var(--ink-500);font-size:1.3rem}.member-identity{min-width:10rem;flex:1}.member-identity small,.member-money small,.member-schedule small{display:block;color:var(--ink-500)}.compact{width:7rem}.member-money,.member-schedule{min-width:8rem}.row-actions{gap:.25rem}.danger{color:var(--danger-700,#a12b2b)}.empty-inline{padding:1.2rem;border-radius:.8rem;background:var(--surface-muted,#f7f5ef);color:var(--ink-500);text-align:center}@media(max-width:760px){.member-adder,.inline-create__form{align-items:stretch;flex-direction:column}.member-card{display:grid;grid-template-columns:auto 1fr auto}.compact,.member-money,.member-schedule{width:auto}.row-actions{grid-column:2/4}.drag-handle{display:none}}
  `],
})
export class AssociationEditorMembersComponent {
  @Input({ required: true }) members: readonly EditorMember[] = [];
  @Input({ required: true }) people: readonly PersonDto[] = [];
  @Input() payouts: readonly EditorPayout[] = [];
  @Input() contribution = 0;
  @Input() months = 0;
  @Output() addExisting = new EventEmitter<string>();
  @Output() addNew = new EventEmitter<{ fullName: string; phoneNumber: string }>();
  @Output() remove = new EventEmitter<string>();
  @Output() move = new EventEmitter<{ from: number; to: number }>();
  @Output() sharesChange = new EventEmitter<MemberSharesChange>();
  protected selectedPersonId = '';
  protected personSearch = '';
  protected newName = '';
  protected newPhone = '';
  protected draggedIndex: number | null = null;

  protected availablePeople(): readonly PersonDto[] {
    const used = new Set(this.members.flatMap((member) => member.personId ? [member.personId] : []));
    const query = this.normalize(this.personSearch);
    return this.people.filter((person) => !used.has(person.id)
      && (!query || [person.fullName, person.phoneNumber, person.subscriberCode]
        .some((value) => this.normalize(value).includes(query))));
  }
  protected displayName(member: EditorMember): string {
    return member.fullName ?? this.people.find((person) => person.id === member.personId)?.fullName ?? 'عضو';
  }
  protected phoneFor(personId: string): string {
    return this.people.find((person) => person.id === personId)?.phoneNumber ?? '';
  }
  protected monthlyValue(shares: number): string {
    const amount = Math.round(this.contribution * 100) * Math.round(shares * 2) / 200;
    return new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 }).format(amount);
  }
  protected payoutPeriods(clientKey: string): string {
    const periods = this.payouts.filter((payout) => payout.recipientType === 'Membership'
      && payout.memberClientKey === clientKey).map((payout) => payout.periodNumber).sort((a, b) => a - b);
    return periods.length ? periods.join('، ') : 'لم يحدد';
  }
  protected payoutValue(clientKey: string): string {
    const shares = this.payouts.filter((payout) => payout.recipientType === 'Membership'
      && payout.memberClientKey === clientKey).reduce((sum, payout) => sum + payout.shareCount, 0);
    return new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 })
      .format(shares * this.contribution * this.months);
  }
  protected addSelected(): void {
    if (!this.selectedPersonId) return;
    this.addExisting.emit(this.selectedPersonId);
    this.selectedPersonId = '';
  }
  protected addInline(): void {
    if (!this.newName.trim() || !this.newPhone.trim()) return;
    this.addNew.emit({ fullName: this.newName.trim(), phoneNumber: this.newPhone.trim() });
    this.newName = '';
    this.newPhone = '';
  }
  protected dropAt(index: number): void {
    if (this.draggedIndex === null || this.draggedIndex === index) return;
    this.move.emit({ from: this.draggedIndex, to: index });
    this.draggedIndex = null;
  }
  private normalize(value: string): string {
    return value.trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar');
  }
}
