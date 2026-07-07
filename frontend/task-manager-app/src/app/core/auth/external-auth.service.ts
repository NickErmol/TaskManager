import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { apiUrl } from '../http';

/**
 * External OAuth providers (spec §13.6). The list comes from the backend so the
 * UI only offers providers that actually have credentials configured.
 */
@Injectable({ providedIn: 'root' })
export class ExternalAuthService {
  private readonly http = inject(HttpClient);

  readonly providers = signal<string[]>([]);

  // Seam for tests — window.location.assign is unmockable in jsdom.
  protected navigate = (url: string): void => window.location.assign(url);

  async loadProviders(): Promise<void> {
    try {
      const list = await firstValueFrom(
        this.http.get<string[]>(apiUrl('/api/auth/external/providers')),
      );
      this.providers.set(list ?? []);
    } catch {
      this.providers.set([]);
    }
  }

  beginLogin(provider: string, returnUrl = '/boards'): void {
    this.navigate(
      apiUrl(`/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`),
    );
  }
}
