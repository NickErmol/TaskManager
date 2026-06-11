import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BoardsApiService } from './boards-api.service';
import { apiUrl } from './api-base';
import { makeBoard, makeBoardDetail, makeLabel } from '../../testing/factories';

describe('BoardsApiService', () => {
  let service: BoardsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BoardsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getBoards() issues GET /api/boards', () => {
    const boards = [makeBoard(), makeBoard()];
    let result: unknown;

    service.getBoards().subscribe((r) => (result = r));

    const req = http.expectOne(apiUrl('/api/boards'));
    expect(req.request.method).toBe('GET');
    req.flush(boards);
    expect(result).toEqual(boards);
  });

  it('getBoard() issues GET /api/boards/{id}', () => {
    const detail = makeBoardDetail();
    let result: unknown;

    service.getBoard(detail.id).subscribe((r) => (result = r));

    const req = http.expectOne(apiUrl(`/api/boards/${detail.id}`));
    expect(req.request.method).toBe('GET');
    req.flush(detail);
    expect(result).toEqual(detail);
  });

  it('createBoard() issues POST /api/boards with the request body', () => {
    const created = makeBoard({ name: 'Sprint 1' });

    service.createBoard({ name: 'Sprint 1', description: 'June' }).subscribe();

    const req = http.expectOne(apiUrl('/api/boards'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Sprint 1', description: 'June' });
    req.flush(created);
  });

  it('createLabel() issues POST /api/boards/{id}/labels with the request body', () => {
    const label = makeLabel({ name: 'bug', color: '#ef4444' });

    service.createLabel(label.boardId, { name: 'bug', color: '#ef4444' }).subscribe();

    const req = http.expectOne(apiUrl(`/api/boards/${label.boardId}/labels`));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'bug', color: '#ef4444' });
    req.flush(label);
  });

  it('deleteLabel() issues DELETE /api/boards/{id}/labels/{labelId}', () => {
    service.deleteLabel('board-1', 'label-1').subscribe();

    const req = http.expectOne(apiUrl('/api/boards/board-1/labels/label-1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
