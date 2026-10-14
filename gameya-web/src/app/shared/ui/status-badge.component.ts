import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ASSOCIATION_STATUS_LABELS, MEMBERSHIP_STATUS_LABELS, PAYOUT_STATUS_LABELS } from '../../core/constants/app.constants';
import { AssociationStatus, MembershipStatus, PayoutStatus } from '../../core/models/api.models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge ' + tone()">{{ label() }}</span>`,
})
export class StatusBadgeComponent {
  readonly status = input.required<string>();
  readonly label = computed(() => {
    const value = this.status();
    return ASSOCIATION_STATUS_LABELS[value as AssociationStatus]
      ?? MEMBERSHIP_STATUS_LABELS[value as MembershipStatus]
      ?? PAYOUT_STATUS_LABELS[value as PayoutStatus]
      ?? ({ Paid: 'مدفوع', Outstanding: 'متأخر', Upcoming: 'قادم', Closed: 'مغلق', Due: 'مستحق', Pending: 'قيد التسوية' } as Record<string, string>)[value]
      ?? value;
  });
  readonly tone = computed(() => {
    const value = this.status();
    if (['Active', 'Completed', 'Paid', 'Closed'].includes(value)) return 'badge--success';
    if (['Cancelled', 'Outstanding', 'Exited'].includes(value)) return 'badge--danger';
    if (['Paused', 'Due', 'Pending'].includes(value)) return 'badge--warning';
    return 'badge--info';
  });
}
