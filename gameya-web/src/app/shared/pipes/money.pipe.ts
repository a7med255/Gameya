import { Pipe, PipeTransform } from '@angular/core';
import { formatMoney } from '../utils/display.utils';

@Pipe({ name: 'money', standalone: true })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined): string { return formatMoney(value); }
}
