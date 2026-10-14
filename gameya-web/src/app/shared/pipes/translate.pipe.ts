import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocalizationService, TranslationKey } from '../../core/services/localization.service';

@Pipe({ name: 'translate', standalone: true, pure: true })
export class TranslatePipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);
  transform(key: TranslationKey): string { return this.localization.translate(key); }
}
