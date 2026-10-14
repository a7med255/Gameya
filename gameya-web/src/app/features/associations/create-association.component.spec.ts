import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { EditorMember, EditorPayout } from './association-editor.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { CreateAssociationComponent } from './create-association.component';

interface EditorHarness {
  members(): readonly EditorMember[];
  payouts(): readonly EditorPayout[];
  addExistingMember(personId: string): void;
  changeShares(clientKey: string, value: number): void;
  addPayout(type: 'AdvanceSlot'): void;
}

describe('CreateAssociationComponent', () => {
  it('suggests one receipt per member and does not redistribute later share increases', async () => {
    await TestBed.configureTestingModule({
      imports: [CreateAssociationComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        { provide: AssociationsApiService, useValue: {} },
        { provide: PeopleApiService, useValue: { list: () => of({ items: [{ id: 'p1', fullName: 'عضو', phoneNumber: '+2010', subscriberCode: 'ABCD2345', isActive: true, createdAtUtc: '' }], page: 1, pageSize: 100, totalCount: 1 }) } },
        ApiErrorService, NotificationService,
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CreateAssociationComponent);
    const editor = fixture.componentInstance as unknown as EditorHarness;

    editor.addExistingMember('p1');
    const key = editor.members()[0].clientKey;
    editor.changeShares(key, 2);

    expect(editor.payouts().filter((payout) => payout.memberClientKey === key)).toHaveLength(1);
    expect(editor.payouts()[0].shareCount).toBe(1);
  });

  it('creates advance as a virtual payout and never as a member', async () => {
    await TestBed.configureTestingModule({
      imports: [CreateAssociationComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        { provide: AssociationsApiService, useValue: {} },
        { provide: PeopleApiService, useValue: { list: () => of({ items: [], page: 1, pageSize: 100, totalCount: 0 }) } },
        ApiErrorService, NotificationService,
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CreateAssociationComponent);
    const editor = fixture.componentInstance as unknown as EditorHarness;

    editor.addPayout('AdvanceSlot');

    expect(editor.members()).toHaveLength(0);
    expect(editor.payouts()[0]).toMatchObject({ recipientType: 'AdvanceSlot', memberClientKey: null, shareCount: 1 });
  });
});
