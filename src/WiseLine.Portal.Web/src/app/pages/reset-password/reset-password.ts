import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { readableHttpError } from '../../core/http-error';

@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPasswordPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  protected readonly linkValid = this.email.length > 0 && this.token.length > 0;
  protected readonly submitting = signal(false);
  protected readonly completed = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
    confirmPassword: ['', [Validators.required]],
  });

  protected submit(): void {
    if (!this.linkValid || this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    if (value.newPassword !== value.confirmPassword) {
      this.error.set('The passwords do not match.');
      return;
    }

    this.error.set(null);
    this.submitting.set(true);
    this.auth
      .resetPassword({ email: this.email, token: this.token, newPassword: value.newPassword })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => this.completed.set(true),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'The reset link is invalid or has expired.')),
      });
  }
}
