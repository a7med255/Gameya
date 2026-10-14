import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card" aria-busy="true" [attr.aria-label]="label()">
      @for (line of lines; track line) {
        <div class="skeleton skeleton-line" [style.width.%]="line"></div>
      }
    </div>
  `,
})
export class LoadingStateComponent {
  readonly label = input('جاري تحميل البيانات');
  protected readonly lines = [42, 94, 78, 88, 65];
}
