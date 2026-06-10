import { Injectable } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import {
  AddMemberRequest,
  BoardDetailDto,
  BoardDto,
  CreateBoardRequest,
  LabelDto,
  UpdateBoardRequest,
} from '../models';

// Step 7a skeleton — HTTP calls land in Step 7b.
@Injectable({ providedIn: 'root' })
export class BoardsApiService {
  getBoards(): Observable<BoardDto[]> {
    return EMPTY;
  }

  getBoard(id: string): Observable<BoardDetailDto> {
    return EMPTY;
  }

  createBoard(request: CreateBoardRequest): Observable<BoardDto> {
    return EMPTY;
  }

  updateBoard(id: string, request: UpdateBoardRequest): Observable<BoardDto> {
    return EMPTY;
  }

  deleteBoard(id: string): Observable<void> {
    return EMPTY;
  }

  addMember(boardId: string, request: AddMemberRequest): Observable<BoardDto> {
    return EMPTY;
  }

  removeMember(boardId: string, userId: string): Observable<void> {
    return EMPTY;
  }

  getLabels(boardId: string): Observable<LabelDto[]> {
    return EMPTY;
  }
}
