import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import {
  AssignTaskRequest,
  CreateTaskRequest,
  MoveTaskRequest,
  TaskDto,
  TaskFilter,
  UpdateTaskRequest,
} from '../models';

/// Mutating task endpoints require If-Match with the last observed RowVersion
/// (spec §4.3 optimistic concurrency); the server replies 409 + current task on conflict.
const ifMatch = (rowVersion: number): { 'If-Match': string } => ({ 'If-Match': `"${rowVersion}"` });

@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly http = inject(HttpClient);

  getTasks(filter: TaskFilter): Observable<TaskDto[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined) params = params.set(key, value);
    }
    return this.http.get<TaskDto[]>(apiUrl('/api/tasks'), { params });
  }

  getTask(id: string): Observable<TaskDto> {
    return this.http.get<TaskDto>(apiUrl(`/api/tasks/${id}`));
  }

  createTask(request: CreateTaskRequest): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl('/api/tasks'), request);
  }

  updateTask(id: string, request: UpdateTaskRequest, rowVersion: number): Observable<TaskDto> {
    return this.http.put<TaskDto>(apiUrl(`/api/tasks/${id}`), request, { headers: ifMatch(rowVersion) });
  }

  deleteTask(id: string): Observable<void> {
    return this.http.delete<void>(apiUrl(`/api/tasks/${id}`));
  }

  moveTask(id: string, request: MoveTaskRequest, rowVersion: number): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/move`), request, { headers: ifMatch(rowVersion) });
  }

  assignTask(id: string, request: AssignTaskRequest, rowVersion: number): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/assign`), request, { headers: ifMatch(rowVersion) });
  }

  addComment(id: string, body: string): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/comments`), { body });
  }

  attachLabel(id: string, labelId: string): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/labels/${labelId}`), null);
  }

  detachLabel(id: string, labelId: string): Observable<TaskDto> {
    return this.http.delete<TaskDto>(apiUrl(`/api/tasks/${id}/labels/${labelId}`));
  }

  addChecklistItem(id: string, title: string): Observable<TaskDto> {
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/checklist`), { title });
  }

  updateChecklistItem(
    id: string,
    itemId: string,
    patch: { title?: string; isDone?: boolean },
  ): Observable<TaskDto> {
    return this.http.put<TaskDto>(apiUrl(`/api/tasks/${id}/checklist/${itemId}`), patch);
  }

  deleteChecklistItem(id: string, itemId: string): Observable<TaskDto> {
    return this.http.delete<TaskDto>(apiUrl(`/api/tasks/${id}/checklist/${itemId}`));
  }

  uploadAttachment(id: string, file: File): Observable<TaskDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/attachments`), form);
  }

  deleteAttachment(id: string, attachmentId: string): Observable<TaskDto> {
    return this.http.delete<TaskDto>(apiUrl(`/api/tasks/${id}/attachments/${attachmentId}`));
  }

  downloadAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(apiUrl(`/api/tasks/${id}/attachments/${attachmentId}/content`), { responseType: 'blob' });
  }
}
