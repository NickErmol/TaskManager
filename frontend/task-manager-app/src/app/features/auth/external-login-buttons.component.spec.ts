import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ExternalAuthService } from '../../core/auth';
import { ExternalLoginButtonsComponent } from './external-login-buttons.component';

describe('ExternalLoginButtonsComponent', () => {
  const providers = signal<string[]>([]);
  const serviceStub = {
    providers,
    loadProviders: jest.fn().mockResolvedValue(undefined),
    beginLogin: jest.fn(),
  };

  beforeEach(() => {
    providers.set([]);
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [ExternalLoginButtonsComponent],
      providers: [{ provide: ExternalAuthService, useValue: serviceStub }],
    });
  });

  it('renders nothing when no providers are configured', () => {
    const fixture = TestBed.createComponent(ExternalLoginButtonsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(0);
  });

  it('renders a labelled button per provider and starts the flow on click', () => {
    providers.set(['google', 'fake']);
    const fixture = TestBed.createComponent(ExternalLoginButtonsComponent);
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons.length).toBe(2);
    expect(buttons[0].textContent).toContain('Continue with Google');
    expect(buttons[1].textContent).toContain('Continue with Fake');

    buttons[1].click();
    expect(serviceStub.beginLogin).toHaveBeenCalledWith('fake', '/boards');
  });
});
