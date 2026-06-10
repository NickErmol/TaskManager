import { Pipe, PipeTransform } from '@angular/core';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 1000 * 60 * 60 * 24 * 365],
  ['month', 1000 * 60 * 60 * 24 * 30],
  ['day', 1000 * 60 * 60 * 24],
  ['hour', 1000 * 60 * 60],
  ['minute', 1000 * 60],
];

@Pipe({ name: 'relativeTime', standalone: true })
export class RelativeTimePipe implements PipeTransform {
  private readonly formatter = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

  transform(value: string | Date | null | undefined): string {
    if (value == null) return '';
    const elapsed = new Date(value).getTime() - Date.now();
    for (const [unit, ms] of UNITS) {
      if (Math.abs(elapsed) >= ms) return this.formatter.format(Math.round(elapsed / ms), unit);
    }
    return 'just now';
  }
}
