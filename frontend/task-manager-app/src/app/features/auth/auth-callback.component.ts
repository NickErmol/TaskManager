import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthStore } from '../../core/auth';

@Component({
  selector: 'tm-auth-callback',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="tm-auth-bg">
      <mat-spinner diameter="48" />
    </div>
  `,
})
export class AuthCallbackComponent implements OnInit {
  private readonly store = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    const raw = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/boards';
    const returnUrl = raw.startsWith('/') && !raw.startsWith('//') ? raw : '/boards';
    void this.store.completeExternalLogin(returnUrl);
  }
}
