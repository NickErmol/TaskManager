import { Injectable } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import {
  AssignTaskRequest,
  CreateTaskRequest,
  MoveTaskRequest,
  TaskDto,
  TaskFilter,
  UpdateTaskRequest,
} from '../models';

// Step 7a skeleton — HTTP calls (and If-Match headers) land in Step 7b.
@Injectable({ providedIn: 'root' })
export class TasksApiService {
  getTasks(filter: TaskFilter): Observable<TaskDto[]> {
    return EMPTY;
  }

  getTask(id: string): Observable<TaskDto> {
    return EMPTY;
  }

  createTask(request: CreateTaskRequest): Observable<TaskDto> {
    return EMPTY;
  }

  updateTask(id: string, request: UpdateTaskRequest, rowVersion: number): Observable<TaskDto> {
    return EMPTY;
  }

  deleteTask(id: string): Observable<void> {
    return EMPTY;
  }

  moveTask(id: string, request: MoveTaskRequest, rowVersion: number): Observable<TaskDto> {
    return EMPTY;
  }

  assignTask(id: string, request: AssignTaskRequest, rowVersion: number): Observable<TaskDto> {
    return EMPTY;
  }

  addComment(id: string, body: string): Observable<TaskDto> {
    return EMPTY;
  }
}
