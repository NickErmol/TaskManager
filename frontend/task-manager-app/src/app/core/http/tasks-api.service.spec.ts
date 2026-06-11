import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TasksApiService } from './tasks-api.service';
import { apiUrl } from './api-base';
import { makeTask } from '../../testing/factories';

describe('TasksApiService', () => {
  let service: TasksApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TasksApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('createTask() issues POST /api/tasks with the request body', () => {
    const task = makeTask({ title: 'Write tests' });

    service
      .createTask({ boardId: task.boardId, title: 'Write tests', priority: 'High' })
      .subscribe();

    const req = http.expectOne(apiUrl('/api/tasks'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ boardId: task.boardId, title: 'Write tests', priority: 'High' });
    req.flush(task);
  });

  it('moveTask() issues POST /api/tasks/{id}/move with an If-Match header carrying the RowVersion', () => {
    const task = makeTask({ rowVersion: 7 });

    service.moveTask(task.id, { newStatus: 'InProgress', position: 2 }, task.rowVersion).subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/move`));
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"7"');
    expect(req.request.body).toEqual({ newStatus: 'InProgress', position: 2 });
    req.flush(task);
  });

  it('assignTask() issues POST /api/tasks/{id}/assign with an If-Match header', () => {
    const task = makeTask({ rowVersion: 3 });
    const assignee = '11111111-1111-1111-1111-111111111111';

    service.assignTask(task.id, { assigneeId: assignee }, task.rowVersion).subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/assign`));
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"3"');
    expect(req.request.body).toEqual({ assigneeId: assignee });
    req.flush(task);
  });

  it('updateTask() issues PUT /api/tasks/{id} with an If-Match header', () => {
    const task = makeTask({ rowVersion: 5 });

    service.updateTask(task.id, { title: 'Renamed', priority: 'Low' }, task.rowVersion).subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}`));
    expect(req.request.method).toBe('PUT');
    expect(req.request.headers.get('If-Match')).toBe('"5"');
    req.flush(task);
  });

  it('attachLabel() issues POST /api/tasks/{id}/labels/{labelId} without If-Match', () => {
    const task = makeTask();

    service.attachLabel(task.id, 'label-1').subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/labels/label-1`));
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(task);
  });

  it('detachLabel() issues DELETE /api/tasks/{id}/labels/{labelId}', () => {
    const task = makeTask();

    service.detachLabel(task.id, 'label-1').subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/labels/label-1`));
    expect(req.request.method).toBe('DELETE');
    req.flush(task);
  });

  it('addChecklistItem() issues POST /api/tasks/{id}/checklist without If-Match', () => {
    const task = makeTask();

    service.addChecklistItem(task.id, 'Write tests').subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist`));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ title: 'Write tests' });
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(task);
  });

  it('updateChecklistItem() issues PUT /api/tasks/{id}/checklist/{itemId}', () => {
    const task = makeTask();

    service.updateChecklistItem(task.id, 'item-1', { isDone: true }).subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist/item-1`));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ isDone: true });
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(task);
  });

  it('deleteChecklistItem() issues DELETE /api/tasks/{id}/checklist/{itemId}', () => {
    const task = makeTask();

    service.deleteChecklistItem(task.id, 'item-1').subscribe();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/checklist/item-1`));
    expect(req.request.method).toBe('DELETE');
    req.flush(task);
  });
});
