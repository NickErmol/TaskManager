import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import {
  AddMemberRequest,
  BoardDetailDto,
  BoardDto,
  CreateBoardRequest,
  LabelDto,
  UpdateBoardRequest,
} from '../models';

@Injectable({ providedIn: 'root' })
export class BoardsApiService {
  private readonly http = inject(HttpClient);

  getBoards(): Observable<BoardDto[]> {
    return this.http.get<BoardDto[]>(apiUrl('/api/boards'));
  }

  getBoard(id: string): Observable<BoardDetailDto> {
    return this.http.get<BoardDetailDto>(apiUrl(`/api/boards/${id}`));
  }

  createBoard(request: CreateBoardRequest): Observable<BoardDto> {
    return this.http.post<BoardDto>(apiUrl('/api/boards'), request);
  }

  updateBoard(id: string, request: UpdateBoardRequest): Observable<BoardDto> {
    return this.http.put<BoardDto>(apiUrl(`/api/boards/${id}`), request);
  }

  deleteBoard(id: string): Observable<void> {
    return this.http.delete<void>(apiUrl(`/api/boards/${id}`));
  }

  addMember(boardId: string, request: AddMemberRequest): Observable<BoardDto> {
    return this.http.post<BoardDto>(apiUrl(`/api/boards/${boardId}/members`), request);
  }

  removeMember(boardId: string, userId: string): Observable<void> {
    return this.http.delete<void>(apiUrl(`/api/boards/${boardId}/members/${userId}`));
  }

  getLabels(boardId: string): Observable<LabelDto[]> {
    return this.http.get<LabelDto[]>(apiUrl(`/api/boards/${boardId}/labels`));
  }
}
