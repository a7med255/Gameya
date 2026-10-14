import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AssociationDetailsDto, PayoutDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { AssociationDetailsComponent } from './association-details.component';

interface DetailsHarness { isPayoutEditable(data: AssociationDetailsDto, payout: PayoutDto): boolean; }

describe('AssociationDetailsComponent', () => {
  const completedPayout: PayoutDto = { id: 'payout', periodId: 'period', periodNumber: 1, recipientType: 'Membership', membershipId: 'member', beneficiaryPersonId: null, shareCount: 1, value: 10000, status: 'Completed' };
  const association: AssociationDetailsDto = { id: 'a1', name: 'جمعية', startDate: '2026-01-01', endDate: '2026-12-01', numberOfMonths: 12, monthlyContribution: 1000, status: 'Active', ownerId: 'owner', memberships: [], periods: [{ id: 'period', periodNumber: 1, periodDate: '2026-01-01', dueDate: '2026-01-10', status: 'Closed' }], payouts: [completedPayout] };

  it('locks a completed historical payout', async () => {
    await TestBed.configureTestingModule({ imports: [AssociationDetailsComponent], providers: [
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'a1' } } } },
      { provide: AssociationsApiService, useValue: { get: () => of(association) } },
      { provide: PeopleApiService, useValue: { list: () => of({ items: [], page: 1, pageSize: 100, totalCount: 0 }) } },
      ApiErrorService, NotificationService, ConfirmService,
    ] }).compileComponents();
    const fixture = TestBed.createComponent(AssociationDetailsComponent);
    fixture.detectChanges();
    expect((fixture.componentInstance as unknown as DetailsHarness).isPayoutEditable(association, completedPayout)).toBe(false);
  });
});
