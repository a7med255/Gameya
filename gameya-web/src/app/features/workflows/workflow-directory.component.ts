import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AssociationSummaryDto, PersonDto } from '../../core/models/api.models';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-workflow-directory', standalone: true, imports: [RouterLink, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page"><header class="page-header"><div><h1 class="page-title">{{ mode === 'replacements' ? 'الاستبدالات' : 'التسويات' }}</h1><p class="page-subtitle">{{ mode === 'replacements' ? 'اختار الجمعية اللي عايز تستبدل عضو فيها. العضو القديم هيفضل محفوظ في التاريخ.' : 'اختار الشخص علشان تشوف التسويات والأرصدة التاريخية الخاصة بيه.' }}</p></div></header>
      @if (mode === 'replacements') { <div class="directory-grid">@for (association of associations(); track association.id) { <a class="card directory-card" [routerLink]="['/owner/associations', association.id]"><div class="split"><strong>{{ association.name }}</strong><app-status-badge [status]="association.status" /></div><p class="muted">{{ association.memberCount }} عضو</p><span class="btn btn--secondary btn--block">فتح الاستبدالات</span></a> }</div> }
      @else { <div class="directory-grid">@for (person of people(); track person.id) { <a class="card directory-card" [routerLink]="['/owner/people', person.id]"><div class="split"><strong>{{ person.fullName }}</strong><span class="code">{{ person.subscriberCode }}</span></div><p class="muted" dir="ltr">{{ person.phoneNumber }}</p><span class="btn btn--secondary btn--block">عرض التسويات</span></a> }</div> }
    </div>
  `,
  styles: [`.directory-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:1rem}.directory-card:hover{border-color:var(--brand-600)}@media(max-width:1000px){.directory-grid{grid-template-columns:repeat(2,1fr)}}@media(max-width:650px){.directory-grid{grid-template-columns:1fr}}`],
})
export class WorkflowDirectoryComponent {
  protected readonly mode = inject(ActivatedRoute).snapshot.data['mode'] as 'replacements' | 'settlements'; protected readonly associations = signal<readonly AssociationSummaryDto[]>([]); protected readonly people = signal<readonly PersonDto[]>([]);
  constructor() { if (this.mode === 'replacements') inject(AssociationsApiService).list('', '', 1, 100).subscribe({ next: (result) => this.associations.set(result.items) }); else inject(PeopleApiService).list('', 1, 100).subscribe({ next: (result) => this.people.set(result.items) }); }
}
