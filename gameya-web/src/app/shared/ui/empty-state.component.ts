import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="state card">
      <div class="state__content">
        <div class="state__icon" aria-hidden="true">{{ icon() }}</div>
        <h2>{{ title() }}</h2>
        <p class="muted">{{ message() }}</p>
        @if (actionLabel()) {
          <button class="btn btn--primary" type="button" (click)="action.emit()">{{ actionLabel() }}</button>
        }
      </div>
    </section>
  `,
})
export class EmptyStateComponent {
  readonly icon = input('○');
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly actionLabel = input('');
  readonly action = output<void>();
}
