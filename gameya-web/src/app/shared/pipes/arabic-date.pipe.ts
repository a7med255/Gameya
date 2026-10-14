import { Pipe, PipeTransform } from '@angular/core';
import { formatDate } from '../utils/display.utils';

@Pipe({ name: 'arabicDate', standalone: true })
export class ArabicDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, includeTime = false): string { return formatDate(value, includeTime); }
}
