import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { readableHttpError } from '../../core/http-error';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class RegisterPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
    termsAccepted: [false, Validators.requiredTrue],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.error.set(null);
    this.submitting.set(true);
    this.auth
      .register({ displayName: value.displayName, email: value.email, password: value.password })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/account']),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Account creation failed. Please try again.')),
      });
  }
}
