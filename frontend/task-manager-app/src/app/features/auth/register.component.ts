import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthStore } from '../../core/auth';

// Spec §4.2 validation: password min 8 chars, ≥1 digit, ≥1 uppercase; display name 2–50.
const PASSWORD_PATTERN = /^(?=.*\d)(?=.*[A-Z]).{8,}$/;

@Component({
  selector: 'tm-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="flex min-h-screen items-center justify-center bg-slate-50 p-4">
      <mat-card class="w-full max-w-md p-6">
        <h1 class="mb-6 text-2xl font-semibold">Create account</h1>

        <form [formGroup]="form" (ngSubmit)="submit()" class="flex flex-col gap-2">
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="email" />
            @if (form.controls.email.hasError('email')) {
              <mat-error>Enter a valid email address</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Display name</mat-label>
            <input matInput formControlName="displayName" autocomplete="name" />
            @if (form.controls.displayName.invalid && form.controls.displayName.touched) {
              <mat-error>Display name must be 2–50 characters</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            @if (form.controls.password.hasError('pattern')) {
              <mat-error>At least 8 characters with one digit and one uppercase letter</mat-error>
            }
          </mat-form-field>

          @if (store.error(); as error) {
            <p class="mb-2 text-sm text-red-600" role="alert">{{ error }}</p>
          }

          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || store.isLoading()">
            @if (store.isLoading()) {
              <mat-spinner diameter="20" class="mx-auto" />
            } @else {
              Create account
            }
          </button>
        </form>

        <p class="mt-4 text-sm text-slate-600">
          Already registered? <a routerLink="/login" class="text-blue-600 hover:underline">Sign in</a>
        </p>
      </mat-card>
    </div>
  `,
})
export class RegisterComponent {
  protected readonly store = inject(AuthStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
  });

  protected submit(): void {
    if (this.form.invalid) return;
    void this.store.register(this.form.getRawValue());
  }
}
