import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import {
  ActivityItemDto,
  BoardActivityItemDto,
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

  getBoardActivity(boardId: string, count = 50): Observable<BoardActivityItemDto[]> {
    return this.http.get<BoardActivityItemDto[]>(
      apiUrl(`/api/analytics/boards/${boardId}/activity`),
      { params: new HttpParams().set('count', count) },
    );
  }
}
