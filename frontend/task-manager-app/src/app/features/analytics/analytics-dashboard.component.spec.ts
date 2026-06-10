import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AnalyticsDashboardComponent } from './analytics-dashboard.component';
import { AnalyticsApiService } from '../../core/http/analytics-api.service';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { makeActivity, makeBoard, makeTrend, makeUserSummary } from '../../testing/factories';

describe('AnalyticsDashboardComponent', () => {
  let fixture: ComponentFixture<AnalyticsDashboardComponent>;

  const summary = makeUserSummary({ tasksCreated: 12, tasksCompleted: 7, tasksAssigned: 4 });
  const board = makeBoard({ name: 'Sprint 1' });

  const analyticsApi = {
    getMySummary: jest.fn().mockReturnValue(of(summary)),
    getMyActivity: jest.fn().mockReturnValue(of([makeActivity(), makeActivity()])),
    getCompletionTrend: jest.fn().mockReturnValue(of(makeTrend(30))),
    getBoardSummary: jest.fn(),
  };
  const boardsApi = { getBoards: jest.fn().mockReturnValue(of([board])) };

  beforeEach(async () => {
    jest.clearAllMocks();
    analyticsApi.getMySummary.mockReturnValue(of(summary));
    analyticsApi.getMyActivity.mockReturnValue(of([makeActivity(), makeActivity()]));
    analyticsApi.getCompletionTrend.mockReturnValue(of(makeTrend(30)));
    boardsApi.getBoards.mockReturnValue(of([board]));

    await TestBed.configureTestingModule({
      imports: [AnalyticsDashboardComponent],
      providers: [
        provideNoopAnimations(),
        { provide: AnalyticsApiService, useValue: analyticsApi },
        { provide: BoardsApiService, useValue: boardsApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AnalyticsDashboardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders the personal stats from the summary endpoint', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(analyticsApi.getMySummary).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-testid="stat-created"]')?.textContent).toContain('12');
    expect(fixture.nativeElement.querySelector('[data-testid="stat-completed"]')?.textContent).toContain('7');
    expect(fixture.nativeElement.querySelector('[data-testid="stat-assigned"]')?.textContent).toContain('4');
    expect(text).toBeTruthy();
  });

  it('loads the completion trend of the first board into the chart series', () => {
    fixture.detectChanges(); // re-render after the async trend load settled
    expect(analyticsApi.getCompletionTrend).toHaveBeenCalledWith(board.id);

    const series = fixture.componentInstance.trendSeries();
    expect(series).toHaveLength(1);
    expect(series[0].series).toHaveLength(30);
    expect(fixture.nativeElement.querySelector('[data-testid="trend-chart"]')).toBeTruthy();
  });

  it('renders the recent activity timeline', () => {
    expect(analyticsApi.getMyActivity).toHaveBeenCalled();
    const items = fixture.nativeElement.querySelectorAll('[data-testid="activity-item"]');
    expect(items).toHaveLength(2);
  });
});
