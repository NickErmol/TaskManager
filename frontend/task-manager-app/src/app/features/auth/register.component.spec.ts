import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RegisterComponent } from './register.component';
import { AuthStore } from '../../core/auth';

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let store: InstanceType<typeof AuthStore>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    store = TestBed.inject(AuthStore);
    jest.spyOn(store, 'register').mockResolvedValue();
    fixture.detectChanges();
  });

  const setValue = (name: string, value: string): void => {
    const input = fixture.nativeElement.querySelector(`input[formControlName="${name}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  const submitButton = (): HTMLButtonElement =>
    fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;

  const fillValid = (): void => {
    setValue('email', 'nick@example.com');
    setValue('displayName', 'Nick');
    setValue('password', 'Passw0rd');
  };

  it('renders email, display name and password fields', () => {
    for (const name of ['email', 'displayName', 'password']) {
      expect(fixture.nativeElement.querySelector(`input[formControlName="${name}"]`)).toBeTruthy();
    }
  });

  it.each([
    ['short7A', 'too short'],
    ['lowercase1only', 'no uppercase'],
    ['NoDigitsHere', 'no digit'],
  ])('keeps submit disabled for password "%s" (%s)', (password) => {
    fillValid();
    setValue('password', password);
    expect(submitButton().disabled).toBe(true);
  });

  it.each([
    ['A', 'too short'],
    ['x'.repeat(51), 'too long'],
  ])('keeps submit disabled for display name "%s" (%s)', (displayName) => {
    fillValid();
    setValue('displayName', displayName);
    expect(submitButton().disabled).toBe(true);
  });

  it('submits the registration to AuthStore.register when valid', () => {
    fillValid();
    expect(submitButton().disabled).toBe(false);

    submitButton().click();
    expect(store.register).toHaveBeenCalledWith({
      email: 'nick@example.com',
      displayName: 'Nick',
      password: 'Passw0rd',
    });
  });
});
