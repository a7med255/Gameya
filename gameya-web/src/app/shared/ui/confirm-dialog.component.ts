import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { ConfirmService } from '../../core/services/confirm.service';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (confirm.requestState(); as request) {
      <div class="dialog-backdrop" (click)="confirm.answer(false)" (keydown.escape)="confirm.answer(false)">
        <section class="dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" aria-describedby="confirm-message" (click)="$event.stopPropagation()">
          <h2 id="confirm-title">{{ request.title }}</h2>
          <p id="confirm-message" class="muted">{{ request.message }}</p>
          <div class="inline" style="justify-content:flex-end;margin-top:1.25rem">
            <button #cancelButton class="btn btn--secondary" type="button" (click)="confirm.answer(false)">رجوع</button>
            <button class="btn" [class.btn--danger]="request.danger" [class.btn--primary]="!request.danger" type="button" (click)="confirm.answer(true)">
              {{ request.confirmLabel || 'تأكيد' }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly confirm = inject(ConfirmService);
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelButton');

  constructor() {
    effect(() => {
      if (this.confirm.requestState()) window.setTimeout(() => this.cancelButton()?.nativeElement.focus());
    });
  }
}
