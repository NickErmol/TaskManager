import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TaskDetailComponent } from './task-detail.component';
import { apiUrl } from '../../core/http/api-base';
import { makeLabel, makeTask } from '../../testing/factories';

describe('TaskDetailComponent', () => {
  let fixture: ComponentFixture<TaskDetailComponent>;
  let http: HttpTestingController;

  const task = makeTask({
    title: 'Fix the build',
    description: 'CI is red',
    priority: 'High',
    rowVersion: 5,
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: MAT_DIALOG_DATA, useValue: { task, boardLabels: [] } },
        { provide: MatDialogRef, useValue: { close: jest.fn() } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TaskDetailComponent);
    fixture.detectChanges();
  });

  it('pre-populates the form from the task', () => {
    const value = fixture.componentInstance.form.getRawValue();
    expect(value.title).toBe('Fix the build');
    expect(value.description).toBe('CI is red');
    expect(value.priority).toBe('High');
  });

  it('saves via PUT /api/tasks/{id} with If-Match carrying the current RowVersion', () => {
    fixture.componentInstance.form.patchValue({ title: 'Fix the build now' });

    fixture.componentInstance.save();

    const req = http.expectOne(apiUrl(`/api/tasks/${task.id}`));
    expect(req.request.method).toBe('PUT');
    expect(req.request.headers.get('If-Match')).toBe('"5"');
    expect(req.request.body).toMatchObject({ title: 'Fix the build now' });
    req.flush({ ...task, rowVersion: 6 });
  });

  it('uses the RowVersion returned by a label toggle, not the stale one from dialog open', async () => {
    // Arrange: a label that is NOT yet attached to the task
    const label = makeLabel({ boardId: task.boardId });
    const taskWithLabel: typeof task = { ...task, labelIds: [label.id], rowVersion: 2 };

    // Re-configure with a board that has the label so toggleLabel can be called
    await TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [TaskDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: MAT_DIALOG_DATA, useValue: { task: { ...task, rowVersion: 1 }, boardLabels: [label] } },
        { provide: MatDialogRef, useValue: { close: jest.fn() } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TaskDetailComponent);
    fixture.detectChanges();

    // Act: toggle the label (server bumps rowVersion 1 → 2)
    const togglePromise = fixture.componentInstance.toggleLabel(label.id);
    const attachReq = http.expectOne(apiUrl(`/api/tasks/${task.id}/labels/${label.id}`));
    expect(attachReq.request.method).toBe('POST');
    attachReq.flush(taskWithLabel);
    await togglePromise;

    // Act: save — should use the fresh rowVersion (2), not the stale one (1)
    fixture.componentInstance.save();
    const saveReq = http.expectOne((r) => r.url === apiUrl(`/api/tasks/${task.id}`) && r.method === 'PUT');
    expect(saveReq.request.headers.get('If-Match')).toBe('"2"');
    saveReq.flush({ ...taskWithLabel, rowVersion: 3 });
  });
});
