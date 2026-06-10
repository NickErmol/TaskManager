import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ActivityItemDto, UserSummaryDto } from '../../core/models';

// Smart component — Step 7a skeleton; ngx-charts dashboard lands in Step 7b.
@Component({
  selector: 'tm-analytics-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ``,
})
export class AnalyticsDashboardComponent {
  readonly summary = signal<UserSummaryDto | null>(null);
  readonly activity = signal<ActivityItemDto[]>([]);
  readonly trendSeries = signal<{ name: string; series: { name: string; value: number }[] }[]>([]);
}
