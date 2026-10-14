import { Injectable } from '@angular/core';

const AR_MESSAGES = {
  'nav.dashboard': 'الرئيسية',
  'nav.people': 'الأشخاص',
  'nav.associations': 'الجمعيات',
  'nav.recordPayment': 'تسجيل دفعة',
  'nav.payments': 'المدفوعات',
  'nav.replacements': 'الاستبدالات',
  'nav.settlements': 'التسويات',
  'nav.reports': 'التقارير',
  'nav.settings': 'الإعدادات',
} as const;

export type TranslationKey = keyof typeof AR_MESSAGES;

@Injectable({ providedIn: 'root' })
export class LocalizationService {
  readonly locale = 'ar-EG';
  readonly direction = 'rtl';

  translate(key: TranslationKey): string { return AR_MESSAGES[key]; }
}
