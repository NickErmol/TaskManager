import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { apiUrl } from '../http/api-base';
import { UserNameService } from './user-name.service';

describe('UserNameService', () => {
  let service: UserNameService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(UserNameService);
    http = TestBed.inject(HttpTestingController);
  });

  it('resolves a display name and caches it (one HTTP call for repeat ids)', async () => {
    const p1 = service.resolve('user-1');
    const req = http.expectOne(apiUrl('/api/users/user-1'));
    req.flush({ id: 'user-1', email: 'a@b.c', displayName: 'Alice', avatarUrl: null });
    await expect(p1).resolves.toBe('Alice');

    const p2 = service.resolve('user-1');
    http.expectNone(apiUrl('/api/users/user-1'));
    await expect(p2).resolves.toBe('Alice');
  });

  it('falls back to "Someone" when the lookup fails', async () => {
    const p = service.resolve('ghost');
    http.expectOne(apiUrl('/api/users/ghost')).flush('nope', { status: 404, statusText: 'Not Found' });
    await expect(p).resolves.toBe('Someone');
  });
});
