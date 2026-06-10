import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import {
  ActivityItemDto,
  BoardSummaryDto,
  CompletionTrendPointDto,
  UserSummaryDto,
} from '../models';

@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  private readonly http = inject(HttpClient);

  getBoardSummary(boardId: string): Observable<BoardSummaryDto> {
    return this.http.get<BoardSummaryDto>(apiUrl(`/api/analytics/boards/${boardId}/summary`));
  }

  getCompletionTrend(boardId: string): Observable<CompletionTrendPointDto[]> {
    return this.http.get<CompletionTrendPointDto[]>(apiUrl(`/api/analytics/boards/${boardId}/completion-trend`));
  }

  getMySummary(): Observable<UserSummaryDto> {
    return this.http.get<UserSummaryDto>(apiUrl('/api/analytics/users/me/summary'));
  }

  getMyActivity(): Observable<ActivityItemDto[]> {
    return this.http.get<ActivityItemDto[]>(apiUrl('/api/analytics/users/me/activity'));
  }
}
