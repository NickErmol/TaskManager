import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'truncate', standalone: true })
export class TruncatePipe implements PipeTransform {
  transform(value: string | null | undefined, maxLength = 80): string {
    if (value == null) return '';
    return value.length <= maxLength ? value : `${value.slice(0, maxLength).trimEnd()}…`;
  }
}
