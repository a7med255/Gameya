import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmService } from '../services/confirm.service';

export interface DirtyAwareComponent { hasUnsavedChanges(): boolean; }

export const unsavedChangesGuard: CanDeactivateFn<DirtyAwareComponent> = (component) => {
  if (!component.hasUnsavedChanges()) return true;
  return inject(ConfirmService).ask({
    title: 'عندك تعديلات مش محفوظة',
    message: 'لو خرجت دلوقتي هتفقد البيانات اللي دخلتها.',
    confirmLabel: 'الخروج بدون حفظ',
    danger: true,
  });
};
