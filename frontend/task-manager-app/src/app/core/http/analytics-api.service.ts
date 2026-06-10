import { Injectable } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import {
  ActivityItemDto,
  BoardSummaryDto,
  CompletionTrendPointDto,
  UserSummaryDto,
} from '../models';

// Step 7a skeleton — HTTP calls land in Step 7b.
@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  getBoardSummary(boardId: string): Observable<BoardSummaryDto> {
    return EMPTY;
  }

  getCompletionTrend(boardId: string): Observable<CompletionTrendPointDto[]> {
    return EMPTY;
  }

  getMySummary(): Observable<UserSummaryDto> {
    return EMPTY;
  }

  getMyActivity(): Observable<ActivityItemDto[]> {
    return EMPTY;
  }
}
