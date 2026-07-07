import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ExternalAuthService } from './external-auth.service';

describe('ExternalAuthService', () => {
  let service: ExternalAuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ExternalAuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the provider list', async () => {
    const load = service.loadProviders();
    http.expectOne((r) => r.url.endsWith('/api/auth/external/providers')).flush(['google', 'fake']);
    await load;
    expect(service.providers()).toEqual(['google', 'fake']);
  });

  it('falls back to an empty list when the endpoint fails', async () => {
    const load = service.loadProviders();
    http.expectOne((r) => r.url.endsWith('/api/auth/external/providers'))
      .flush(null, { status: 500, statusText: 'boom' });
    await load;
    expect(service.providers()).toEqual([]);
  });

  it('beginLogin navigates to the challenge URL with an encoded returnUrl', () => {
    const navigate = jest.fn();
    (service as unknown as { navigate: (url: string) => void }).navigate = navigate;

    service.beginLogin('google', '/boards/42');

    expect(navigate).toHaveBeenCalledWith(
      expect.stringContaining('/api/auth/external/google?returnUrl=%2Fboards%2F42'),
    );
  });
});
