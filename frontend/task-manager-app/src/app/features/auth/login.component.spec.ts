import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { LoginComponent } from './login.component';
import { AuthStore } from '../../core/auth';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let store: InstanceType<typeof AuthStore>;

  const configure = async (errorCode: string | null = null): Promise<void> => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(errorCode === null ? {} : { error: errorCode }),
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    store = TestBed.inject(AuthStore);
    jest.spyOn(store, 'login').mockResolvedValue();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await configure();
  });

  const setValue = (selector: string, value: string): void => {
    const input = fixture.nativeElement.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  const submitButton = (): HTMLButtonElement =>
    fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;

  it('renders email and password fields', () => {
    expect(fixture.nativeElement.querySelector('input[formControlName="email"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[formControlName="password"]')).toBeTruthy();
  });

  it('disables submit while the form is invalid', () => {
    expect(submitButton().disabled).toBe(true);
    setValue('input[formControlName="email"]', 'not-an-email');
    setValue('input[formControlName="password"]', 'Passw0rd!');
    expect(submitButton().disabled).toBe(true);
  });

  it('submits credentials to AuthStore.login when valid', () => {
    setValue('input[formControlName="email"]', 'nick@example.com');
    setValue('input[formControlName="password"]', 'Passw0rd!');
    expect(submitButton().disabled).toBe(false);

    submitButton().click();
    expect(store.login).toHaveBeenCalledWith({ email: 'nick@example.com', password: 'Passw0rd!' });
  });

  it('shows a message when redirected back with error=email-unverified', async () => {
    await configure('email-unverified');
    expect(fixture.nativeElement.textContent).toContain(
      'Your account with that provider has no verified email address, so we could not sign you in.',
    );
  });
});
