import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { NgxChartsModule } from '@swimlane/ngx-charts';
import { firstValueFrom } from 'rxjs';
import {
  ActivityItemDto,
  BoardDto,
  UserSummaryDto,
} from '../../core/models';
import { AnalyticsApiService } from '../../core/http/analytics-api.service';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { RelativeTimePipe } from '../../shared/pipes';
import { EmptyStateComponent } from '../../shared/components';

interface TrendSeries {
  name: string;
  series: { name: string; value: number }[];
}

// Smart component: personal stats + per-board completion trend (ngx-charts).
@Component({
  selector: 'tm-analytics-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatSelectModule, NgxChartsModule, RelativeTimePipe, EmptyStateComponent],
  template: `
    <main class="mx-auto max-w-5xl p-6">
      <h1 class="mb-6 text-2xl font-semibold text-slate-800">Your analytics</h1>

      <div class="mb-8 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div data-testid="stat-created" class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <p class="text-sm text-slate-500">Tasks created</p>
          <p class="text-3xl font-semibold text-slate-800">{{ summary()?.tasksCreated ?? 0 }}</p>
        </div>
        <div data-testid="stat-completed" class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <p class="text-sm text-slate-500">Tasks completed</p>
          <p class="text-3xl font-semibold text-slate-800">{{ summary()?.tasksCompleted ?? 0 }}</p>
        </div>
        <div data-testid="stat-assigned" class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <p class="text-sm text-slate-500">Tasks assigned to you</p>
          <p class="text-3xl font-semibold text-slate-800">{{ summary()?.tasksAssigned ?? 0 }}</p>
        </div>
      </div>

      <section class="mb-8 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 class="font-medium text-slate-800">Completion trend (last 30 days)</h2>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Board</mat-label>
            <mat-select [value]="selectedBoardId()" (selectionChange)="selectBoard($event.value)">
              @for (board of boards(); track board.id) {
                <mat-option [value]="board.id">{{ board.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        @if (trendSeries().length > 0) {
          <div data-testid="trend-chart" class="h-72">
            <ngx-charts-line-chart
              [results]="trendSeries()"
              [xAxis]="true"
              [yAxis]="true"
              [autoScale]="true"
            />
          </div>
        } @else {
          <tm-empty-state icon="show_chart" message="No completion data yet." />
        }
      </section>

      <section class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <h2 class="mb-3 font-medium text-slate-800">Recent activity</h2>
        @if (activity().length > 0) {
          <ul class="flex flex-col gap-2">
            @for (item of activity(); track $index) {
              <li data-testid="activity-item" class="flex items-center justify-between text-sm">
                <span class="text-slate-700">{{ item.eventType }}</span>
                <span class="text-slate-400">{{ item.occurredAt | relativeTime }}</span>
              </li>
            }
          </ul>
        } @else {
          <tm-empty-state icon="history" message="No activity yet." />
        }
      </section>
    </main>
  `,
})
export class AnalyticsDashboardComponent implements OnInit {
  private readonly analyticsApi = inject(AnalyticsApiService);
  private readonly boardsApi = inject(BoardsApiService);

  readonly summary = signal<UserSummaryDto | null>(null);
  readonly activity = signal<ActivityItemDto[]>([]);
  readonly boards = signal<BoardDto[]>([]);
  readonly selectedBoardId = signal<string | null>(null);
  readonly trendSeries = signal<TrendSeries[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const [summary, activity, boards] = await Promise.all([
      firstValueFrom(this.analyticsApi.getMySummary()).catch(() => null),
      firstValueFrom(this.analyticsApi.getMyActivity()).catch(() => [] as ActivityItemDto[]),
      firstValueFrom(this.boardsApi.getBoards()).catch(() => [] as BoardDto[]),
    ]);
    this.summary.set(summary);
    this.activity.set(activity);
    this.boards.set(boards);
    if (boards.length > 0) await this.selectBoard(boards[0].id);
  }

  async selectBoard(boardId: string): Promise<void> {
    this.selectedBoardId.set(boardId);
    try {
      const trend = await firstValueFrom(this.analyticsApi.getCompletionTrend(boardId));
      this.trendSeries.set([
        { name: 'Completed', series: trend.map((p) => ({ name: p.date, value: p.completed })) },
      ]);
    } catch {
      this.trendSeries.set([]);
    }
  }
}
