import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ExternalAuthService } from '../../core/auth';

const LABELS: Record<string, string> = {
  google: 'Continue with Google',
  github: 'Continue with GitHub',
};

@Component({
  selector: 'tm-external-login-buttons',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule],
  template: `
    @if (service.providers().length > 0) {
      <div class="my-4 flex items-center gap-3 text-xs uppercase tracking-wide text-slate-400">
        <span class="h-px flex-1 bg-slate-200"></span>
        or
        <span class="h-px flex-1 bg-slate-200"></span>
      </div>
      <div class="flex flex-col gap-2">
        @for (provider of service.providers(); track provider) {
          <button
            mat-stroked-button
            type="button"
            class="!rounded-lg"
            (click)="service.beginLogin(provider, '/boards')"
          >
            {{ label(provider) }}
          </button>
        }
      </div>
    }
  `,
})
export class ExternalLoginButtonsComponent implements OnInit {
  protected readonly service = inject(ExternalAuthService);

  ngOnInit(): void {
    void this.service.loadProviders();
  }

  protected label(provider: string): string {
    return LABELS[provider] ?? `Continue with ${provider.charAt(0).toUpperCase()}${provider.slice(1)}`;
  }
}
