import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { AuthStore } from '../../core/auth';
import { AuthCallbackComponent } from './auth-callback.component';

describe('AuthCallbackComponent', () => {
  const storeStub = { completeExternalLogin: jest.fn().mockResolvedValue(undefined) };

  const setup = (returnUrl: string | null) => {
    TestBed.configureTestingModule({
      imports: [AuthCallbackComponent],
      providers: [
        { provide: AuthStore, useValue: storeStub },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(returnUrl === null ? {} : { returnUrl }),
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(AuthCallbackComponent);
    fixture.detectChanges();
  };

  beforeEach(() => jest.clearAllMocks());

  it('completes the external login with the given returnUrl', () => {
    setup('/boards/7');
    expect(storeStub.completeExternalLogin).toHaveBeenCalledWith('/boards/7');
  });

  it('defaults a missing returnUrl to /boards', () => {
    setup(null);
    expect(storeStub.completeExternalLogin).toHaveBeenCalledWith('/boards');
  });

  it('rejects a non-relative returnUrl', () => {
    setup('https://evil.example.com');
    expect(storeStub.completeExternalLogin).toHaveBeenCalledWith('/boards');
  });
});
